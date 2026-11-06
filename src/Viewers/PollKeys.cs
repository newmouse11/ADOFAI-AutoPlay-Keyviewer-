using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace AutoFingering.Viewers
{
    // Jipper 1.4.9와 LineKeyViewer는 자기 스레드에서 private static CheckKey(KeyCode)를 돈다.
    // 상태를 직접 쓰면 다음 폴링이 되돌려 버리므로, 뷰어가 묻는 지점에서 눌렸다고 답한다.
    internal static class PollKeys
    {
        // ponytail: Unity KeyCode와 Windows VK(4096+255)까지만 담는다. Linux/Mac 네이티브 코드가 필요하면 넓힌다.
        private const int Size = 4352;

        private static readonly bool[] Held = new bool[Size];
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();

        public static void Patch(MethodInfo checkKey)
        {
            if (Patched.Contains(checkKey)) return;

            // 패치가 성공한 뒤에 기록한다. 먼저 기록하면 실패가 다음 시도부터 묻혀 어댑터가 빈 껍데기로 남는다.
            Main.Harmony.Patch(checkKey, new HarmonyMethod(typeof(PollKeys), nameof(CheckKeyPrefix)));
            Patched.Add(checkKey);
        }

        public static void Set(int code, bool down)
        {
            if (code >= 0 && code < Size) Volatile.Write(ref Held[code], down);
        }

        // UnpatchAll 뒤에는 다음 바인딩이 다시 패치해야 한다.
        public static void Forget()
        {
            Patched.Clear();
        }

        // 뷰어 스레드에서 호출된다. 눌리지 않았으면 원본으로 넘겨 실제 키 입력은 그대로 표시한다.
        public static bool CheckKeyPrefix(KeyCode keyCode, ref bool __result)
        {
            int code = (int)keyCode;
            if (code >= 0 && code < Size && Volatile.Read(ref Held[code]))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}
