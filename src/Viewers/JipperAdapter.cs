using System;
using System.Reflection;
using UnityEngine;

namespace AutoFingering.Viewers
{
    // 1.6.0은 Work(int[], bool, long)로 직접 넣고, 1.4.9처럼 그 메서드가 없으면 CheckKey 폴링으로 넣는다.
    // 게임이나 모드 버전 번호로 가르지 않는다. assembly_cache가 낡아 있을 수 있고 중간 버전은 확인하지 못했기 때문이다.
    internal sealed class JipperAdapter : ViewerAdapter
    {
        private const string ModId = "JipperResourcePack";
        private const string ViewerTypeName = "JipperResourcePack.KeyViewerContents.KeyViewer";
        private const string CountTypeName = "JipperResourcePack.KeyViewerContents.KeyCountData";
        private const int GhostIndexStart = 36;

        private static readonly Type[] WorkArgs = { typeof(int[]), typeof(bool), typeof(long) };

        private FieldInfo instanceField;
        private object viewer;
        private Func<bool> isActive;
        private Func<int[], bool, long, bool> work;
        private Func<long> ticks;
        private MethodInfo checkKey;

        private FieldInfo keysField;
        private FieldInfo countInstance;
        private FieldInfo countArray;
        private FieldInfo countTotal;
        private MethodInfo countSave;

        private int[][] singleIndex;
        private int[] counted;
        private bool[] lit;

        public override string Name => work != null ? "Jipper (event)" : "Jipper (poll)";

        public override bool TryBind()
        {
            Type type = FindType(ModId, ViewerTypeName);
            if (type == null) return false;

            instanceField = type.GetField("Instance", PublicStatic);
            if (instanceField == null) return Missing("KeyViewer.Instance");
            viewer = instanceField.GetValue(null);
            if (viewer == null) return false;

            // Instance는 생성자에서 채워지므로 기능이 꺼져 있어도 null이 아니다. Active까지 봐야 한다.
            MethodInfo activeGetter = type.GetProperty("Active", PublicInstance)?.GetGetMethod();
            if (activeGetter == null) return Missing("Feature.Active");
            isActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), viewer, activeGetter);
            if (!isActive()) return false;

            MethodInfo workMethod = type.GetMethod(
                "Work", BindingFlags.NonPublic | BindingFlags.Instance, null, WorkArgs, null);
            if (workMethod != null)
            {
                MethodInfo ticksGetter = type.GetProperty("CurrentTicks", PublicStatic)?.GetGetMethod();
                if (ticksGetter == null || workMethod.ReturnType != typeof(bool)) return Missing("KeyViewer.CurrentTicks");
                work = (Func<int[], bool, long, bool>)Delegate.CreateDelegate(
                    typeof(Func<int[], bool, long, bool>), viewer, workMethod);
                ticks = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), ticksGetter);
            }
            else
            {
                checkKey = type.GetMethod("CheckKey", PrivateStatic, null, KeyCodeArg, null);
                if (checkKey == null) return Missing("KeyViewer.Work or CheckKey");
            }

            keysField = type.GetField("Keys", PublicInstance);
            if (keysField == null) return Missing("KeyViewer.Keys");
            if (!BindCounts(type)) return Missing("KeyCountData");

            var codes = type.GetMethod("GetKeyCode", PrivateStatic)?.Invoke(null, null) as KeyCode[];
            if (codes == null || codes.Length == 0) return Missing("KeyViewer.GetKeyCode");
            // Work는 36 이상의 인덱스를 고스트 레인으로 해석해 표시와 통계가 어긋난다.
            if (codes.Length > GhostIndexStart) return Missing("KeyViewer.GetKeyCode length");
            SetKeys(codes);
            return true;
        }

        public override void Begin()
        {
            singleIndex = new int[KeyCodes.Length][];
            for (int i = 0; i < singleIndex.Length; i++) singleIndex[i] = new[] { i };
            counted = new int[KeyCodes.Length];
            lit = new bool[KeyCodes.Length];
            if (checkKey != null) PollKeys.Patch(checkKey);
        }

        public override void Press(int index)
        {
            if (!IsLive()) return;

            if (work == null)
            {
                PollKeys.Set(KeyCodes[index], true);
                counted[index]++;
            }
            else
            {
                lit[index] = true;
                // Work는 실제로 숫자를 올렸을 때만 true를 준다. 진짜 키를 누르고 있어 걸러진 경우는 빼야 맞다.
                if (work(singleIndex[index], true, ticks())) counted[index]++;
            }
        }

        public override void Release(int index)
        {
            if (work == null)
            {
                PollKeys.Set(KeyCodes[index], false);
            }
            else if (IsLive())
            {
                lit[index] = false;
                work(singleIndex[index], false, ticks());
            }
        }

        public override void End()
        {
            if (work == null)
            {
                foreach (int code in KeyCodes) PollKeys.Set(code, false);
            }
            else
            {
                ReleaseLitKeys();
            }

            RemoveCountedPresses();
        }

        // 실패로 어댑터가 빠지면 이후의 떼기가 오지 않는다. Work의 _keyState가 켜진 채 남으면
        // 플레이어가 그 키를 실제로 누를 때까지 불이 안 꺼진다. 통계 복원은 여기가 실패해도 계속해야 한다.
        private void ReleaseLitKeys()
        {
            try
            {
                if (!IsLive()) return;
                for (int i = 0; i < lit.Length; i++)
                {
                    if (!lit[i]) continue;
                    lit[i] = false;
                    work(singleIndex[i], false, ticks());
                }
            }
            catch (Exception e)
            {
                Log.Once("jipper release", e);
            }
        }

        // FeatureReset이 Instance를 바꾸거나 기능이 꺼지면 묶어 둔 대리자는 낡은 객체를 가리킨다.
        private bool IsLive()
        {
            return ReferenceEquals(instanceField.GetValue(null), viewer) && isActive();
        }

        private bool BindCounts(Type viewerType)
        {
            Type data = viewerType.Assembly.GetType(CountTypeName, false);
            if (data == null) return false;

            countInstance = data.GetField("Instance", PublicStatic);
            countArray = data.GetField("Count", PublicInstance);
            countTotal = data.GetField("TotalCount", PublicInstance);
            countSave = data.GetMethod("Save", PublicInstance);
            return countInstance != null && countArray != null && countTotal != null && countSave != null;
        }

        // 자동 재생의 입력이 플레이어의 누적 키 통계를 오염시키지 않게 되돌린다.
        // 1.6.0은 Work가 저장하지 않아서 진짜 키를 누르지 않는 한 파일에 쓰이기 전에 복원된다.
        // 1.4.9는 폴링 스레드가 저장까지 하므로 복원 전에 게임을 끄면 남을 수 있다.
        private void RemoveCountedPresses()
        {
            object data = countInstance.GetValue(null);
            if (data == null) return;

            var counts = (int[])countArray.GetValue(data);
            int total = (int)countTotal.GetValue(data);
            bool changed = false;
            for (int i = 0; i < counted.Length; i++)
            {
                if (counted[i] == 0) continue;
                int target = CountIndex(i);
                counts[target] = Math.Max(0, counts[target] - counted[i]);
                total -= counted[i];
                changed = true;
            }

            if (!changed) return;

            countTotal.SetValue(data, Math.Max(0, total));
            countSave.Invoke(data, null);
            RefreshLabels(counts);
        }

        // Total은 뷰어 Update가 다시 그리지만 키별 숫자는 다음 입력까지 부푼 값으로 남는다.
        private void RefreshLabels(int[] counts)
        {
            var keys = keysField.GetValue(viewer) as Array;
            if (keys == null) return;

            for (int i = 0; i < counted.Length; i++)
            {
                if (counted[i] == 0) continue;
                object key = keys.GetValue(i);
                object value = key?.GetType().GetField("Value", PublicInstance)?.GetValue(key);
                PropertyInfo text = value?.GetType().GetProperty("Text", PublicInstance);
                text?.SetValue(value, counts[CountIndex(i)].ToString(), null);
            }
        }

        // Key10 스타일에서 9번 키는 Count[10]에 쌓인다 (Jipper KeyViewer.cs 1.6.0:2051, 1.4.9:1727). 배열 길이 10은 Key10뿐이다.
        private int CountIndex(int index)
        {
            return KeyCodes.Length == 10 && index == 9 ? 10 : index;
        }
    }
}
