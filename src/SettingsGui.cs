using System;
using System.Collections.Generic;
using AutoFingering.Game;
using AutoFingering.Viewers;
using UnityEngine;
using UnityModManagerNet;

namespace AutoFingering
{
    using AutoFingering.Core;

    // 다른 모드와 같은 모양이 되도록 UMM 기본 스킨을 그대로 쓴다.
    internal static class SettingsGui
    {
        private const float LabelWidth = 180f;
        private const float SliderWidth = 200f;
        private const float ValueWidth = 72f;
        private const float ProbeInterval = 1f;

        private static readonly int[] MaxKeyChoices = { 4, 6, 8, 10, 12, 16, SolverOptions.AllKeys };

        private static List<ViewerAdapter> probed = new List<ViewerAdapter>();
        private static float nextProbeTime;
        private static bool rolesOpen;
        private static bool rolesOpenRequested;
        private static Layout layout;
        private static string layoutSource = "";
        private static string viewerNames = "";

        private static bool Korean
        {
            get
            {
                UiLanguage language = Main.Settings.Language;
                return language == UiLanguage.Korean
                    || (language == UiLanguage.Game && RDString.language == SystemLanguage.Korean);
            }
        }

        public static void OnHide(UnityModManager.ModEntry modEntry)
        {
            SpeedRunner.Reset();
        }

        public static void Draw(UnityModManager.ModEntry modEntry)
        {
            // IMGUI는 Layout과 그 뒤 이벤트의 컨트롤 수가 같아야 하므로 슬롯 목록과 펼침 상태는 Layout 때만 바꾼다.
            if (Event.current.type == EventType.Layout)
            {
                // 뷰어 조회는 리플렉션이라 프레임마다 돌리지 않는다. 창을 연 채 뷰어 모드를 켜고 끌 수 있어 1초마다 다시 본다.
                if (Time.unscaledTime >= nextProbeTime)
                {
                    nextProbeTime = Time.unscaledTime + ProbeInterval;
                    probed = ViewerHub.Probe();
                }

                layout = ViewerHub.PreviewLayout(probed, out layoutSource, out viewerNames);
                rolesOpen = rolesOpenRequested && layout != null;
            }

            Settings settings = Main.Settings;
            DrawLanguage(settings);
            DrawRollStyle(settings);
            DrawHand(settings);
            DrawBase(settings);
            DrawMaxKeys(settings);
            DrawMaxSpeed(settings);
            DrawRoles(settings);
            DrawStatus();
        }

        private static string T(string korean, string english)
        {
            return Korean ? korean : english;
        }

        private static void DrawLanguage(Settings settings)
        {
            BeginRow(T("언어", "Language"));
            if (Choice(T("게임 설정", "Follow game"), settings.Language == UiLanguage.Game)) settings.Language = UiLanguage.Game;
            if (Choice("한국어", settings.Language == UiLanguage.Korean)) settings.Language = UiLanguage.Korean;
            if (Choice("English", settings.Language == UiLanguage.English)) settings.Language = UiLanguage.English;
            EndRow();
        }

        private static void DrawRollStyle(Settings settings)
        {
            BeginRow(T("롤 방식", "Roll style"));
            if (Choice(T("안밖", "Outward"), settings.Style == RollStyle.Outward)) settings.Style = RollStyle.Outward;
            if (Choice(T("밖안", "Inward"), settings.Style == RollStyle.Inward)) settings.Style = RollStyle.Inward;
            if (Choice(T("계단", "Stair"), settings.Style == RollStyle.Stair)) settings.Style = RollStyle.Stair;
            if (Choice(T("역계단", "Reverse stair"), settings.Style == RollStyle.ReverseStair)) settings.Style = RollStyle.ReverseStair;
            EndRow();
        }

        private static void DrawHand(Settings settings)
        {
            BeginRow(T("주손", "Main hand"));
            if (Choice(T("오른손", "Right"), settings.MainHand == Hand.Right)) settings.MainHand = Hand.Right;
            if (Choice(T("왼손", "Left"), settings.MainHand == Hand.Left)) settings.MainHand = Hand.Left;
            EndRow();
        }

        private static void DrawBase(Settings settings)
        {
            BeginRow(T("기반", "Base"));
            if (Choice(T("2키", "2-key"), settings.Base == 2)) settings.Base = 2;
            if (Choice(T("4키", "4-key"), settings.Base == 4)) settings.Base = 4;
            EndRow();
        }

        private static void DrawMaxKeys(Settings settings)
        {
            BeginRow(T("최대 사용 키", "Max keys used"));
            foreach (int keys in MaxKeyChoices)
            {
                string label = keys == SolverOptions.AllKeys ? T("전체", "All") : keys.ToString();
                if (Choice(label, settings.MaxKeys == keys)) settings.MaxKeys = keys;
            }

            EndRow();
        }

        // 직접 지정을 입력칸이 아닌 슬라이더와 버튼으로 둔다. 에디터는 Backspace로 타일을 지우므로 키보드를 쓰면 안 된다.
        // 슬라이더는 한 칸이 약 7 BPM이라 정확한 값은 -, + 버튼으로 맞춘다.
        private static void DrawMaxSpeed(Settings settings)
        {
            BeginRow(T("2키 기반 최대 속도", "2-key max speed"));
            string action = SpeedRunner.Running ? T("취소", "Cancel") : T("테스트", "Test");
            if (GUILayout.Button(action, GUILayout.ExpandWidth(false)))
            {
                if (SpeedRunner.Running) SpeedRunner.Reset();
                else SpeedRunner.Start();
            }

            float current = (float)settings.GetTwoKeyBpm();
            float value = GUILayout.HorizontalSlider(
                current, (float)Settings.MinTwoKeyBpm, (float)Settings.MaxTwoKeyBpm, GUILayout.Width(SliderWidth));
            double wanted = value != current ? Math.Round(value) : current;
            if (GUILayout.Button("-", GUILayout.ExpandWidth(false))) wanted = Math.Round(settings.GetTwoKeyBpm()) - 1;
            if (GUILayout.Button("+", GUILayout.ExpandWidth(false))) wanted = Math.Round(settings.GetTwoKeyBpm()) + 1;
            if (wanted != current)
            {
                settings.SetTwoKeyBpm(wanted);
                SpeedRunner.Reset();
            }

            GUILayout.Label(settings.GetTwoKeyBpm().ToString("0") + " BPM", GUILayout.Width(ValueWidth));
            GUILayout.Label(SpeedTestMessage(settings));
            EndRow();
        }

        private static string SpeedTestMessage(Settings settings)
        {
            switch (SpeedRunner.Current)
            {
                case SpeedRunner.Status.Waiting:
                    return T("두 키를 번갈아 5초 동안 최대한 빠르게 치세요. 첫 입력부터 잽니다.",
                        "Alternate two keys as fast as you can for 5 seconds. Timing starts at your first press.");
                case SpeedRunner.Status.Measuring:
                    return string.Format(T("남은 시간 {0:0.0}초, 현재 {1:0} BPM", "{0:0.0} s left, now {1:0} BPM"),
                        SpeedRunner.Remaining, SpeedRunner.LiveBpm);
                case SpeedRunner.Status.Done:
                    // 슬라이더 범위를 벗어난 측정값은 끝값으로 잘린다. 잰 값을 그대로 쓴 것처럼 보이면 안 된다.
                    double applied = settings.GetTwoKeyBpm();
                    return Math.Abs(SpeedRunner.ResultBpm - applied) < 0.5
                        ? string.Format(T("측정 결과 {0:0} BPM을 적용했습니다.", "Applied the measured {0:0} BPM."), applied)
                        : string.Format(T("측정 결과 {0:0} BPM은 범위 밖이라 {1:0} BPM을 적용했습니다.", "Measured {0:0} BPM is out of range, so {1:0} BPM was applied."),
                            SpeedRunner.ResultBpm, applied);
                case SpeedRunner.Status.TooFew:
                    return T("두 키를 번갈아 더 쳐 주세요. 값은 바꾸지 않았습니다.", "Alternate two keys for longer. The value was not changed.");
                case SpeedRunner.Status.NeedMainMenu:
                    return T("메인 메뉴에서 테스트할 수 있습니다.", "The test runs from the main menu.");
                case SpeedRunner.Status.Interrupted:
                    return T("화면이 바뀌어 취소했습니다. 값은 바꾸지 않았습니다.", "The screen changed, so the test stopped. The value was not changed.");
                default:
                    return "";
            }
        }

        private static void DrawRoles(Settings settings)
        {
            if (layout == null) return;

            string title = T("키 역할 편집", "Key role editor");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((rolesOpen ? "- " : "+ ") + title, GUILayout.ExpandWidth(false)))
            {
                rolesOpenRequested = !rolesOpen;
            }

            if (rolesOpen && GUILayout.Button(T("기본값으로", "Reset"), GUILayout.ExpandWidth(false)))
            {
                int keyCount = layout.Slots.Length;
                settings.RoleOverrides.RemoveAll(item => item.KeyCount == keyCount);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (!rolesOpen) return;

            for (int slot = 0; slot < layout.Slots.Length; slot++)
            {
                KeySlot key = layout.Slots[slot];
                BeginRow(key.Label);
                if (GUILayout.Button(RoleText(key), GUILayout.Width(SliderWidth)))
                {
                    CycleRole(settings, slot, key);
                }

                EndRow();
            }
        }

        private static string RoleText(KeySlot key)
        {
            if (!key.Enabled) return T("사용 안 함", "Not used");

            string hand = key.Hand == Hand.Left ? T("왼손", "Left") : T("오른손", "Right");
            return hand + " " + FingerName(key.Finger);
        }

        private static string FingerName(Finger finger)
        {
            switch (finger)
            {
                case Finger.Index: return T("검지", "index");
                case Finger.Middle: return T("중지", "middle");
                case Finger.Ring: return T("약지", "ring");
                case Finger.Pinky: return T("소지", "pinky");
                case Finger.Thumb1: return T("엄지 1", "thumb 1");
                case Finger.Thumb2: return T("엄지 2", "thumb 2");
                case Finger.Palm1: return T("손날 1", "palm 1");
                default: return T("손날 2", "palm 2");
            }
        }

        // 왼손 8개, 오른손 8개, 사용 안 함을 차례로 돈다.
        private static void CycleRole(Settings settings, int slot, KeySlot key)
        {
            const int FingersPerHand = 8;
            const int NotUsed = FingersPerHand * 2;

            int state = key.Enabled ? (int)key.Hand * FingersPerHand + (int)key.Finger : NotUsed;
            state = (state + 1) % (NotUsed + 1);

            int keyCount = layout.Slots.Length;
            RoleOverride item = settings.RoleOverrides.Find(o => o.KeyCount == keyCount && o.Slot == slot);
            if (item == null)
            {
                item = new RoleOverride { KeyCount = keyCount, Slot = slot };
                settings.RoleOverrides.Add(item);
            }

            item.Enabled = state != NotUsed;
            item.Hand = state == NotUsed ? key.Hand : (Hand)(state / FingersPerHand);
            item.Finger = state == NotUsed ? key.Finger : (Finger)(state % FingersPerHand);
        }

        private static void DrawStatus()
        {
            if (layout == null)
            {
                GUILayout.Label(T("연결된 키 뷰어가 없어 키를 표시하지 않습니다. Jipper 같은 키 뷰어 모드를 켜 주세요.",
                    "No key viewer is bound, so no keys are shown. Turn on a key viewer mod such as Jipper."));
                return;
            }

            Plan plan = Planner.Latest;
            string overSpeed = Planner.Solving
                ? T("계산 중", "solving")
                : plan != null ? plan.Violations.ToString() : "-";
            string status = T("연결된 뷰어: ", "Viewers: ") + viewerNames
                + T(" | 배치: ", " | Layout: ") + layoutSource
                + T(" | 키 ", " | Keys: ") + layout.Slots.Length
                + T(" | 최대 속도 초과: ", " | Over max speed: ") + overSpeed;
            GUILayout.Label(status);
        }

        private static void BeginRow(string title)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(LabelWidth));
        }

        private static void EndRow()
        {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static bool Choice(string label, bool selected)
        {
            return GUILayout.Toggle(selected, label, GUILayout.ExpandWidth(false));
        }
    }
}
