using System.Collections.Generic;
using AutoFingering.Game;
using AutoFingering.Viewers;
using UnityEngine;
using UnityModManagerNet;

namespace AutoFingering
{
    using AutoFingering.Core;

    internal static class Palette
    {
        public static readonly Color32 Base = new Color32(0x0B, 0x0E, 0x11, 0xFF);
        public static readonly Color32 Surface = new Color32(0x18, 0x1A, 0x20, 0xFF);
        public static readonly Color32 Card = new Color32(0x1E, 0x23, 0x29, 0xFF);
        public static readonly Color32 Elevated = new Color32(0x2B, 0x31, 0x39, 0xFF);
        public static readonly Color32 Border = new Color32(0x2B, 0x31, 0x39, 0xFF);
        public static readonly Color32 Text = new Color32(0xEA, 0xEC, 0xEF, 0xFF);
        public static readonly Color32 Secondary = new Color32(0xB7, 0xBD, 0xC6, 0xFF);
        public static readonly Color32 Tertiary = new Color32(0x84, 0x8E, 0x9C, 0xFF);
        public static readonly Color32 Disabled = new Color32(0x5E, 0x66, 0x73, 0xFF);
        public static readonly Color32 Primary = new Color32(0xFC, 0xD5, 0x35, 0xFF);
    }

    internal static class SettingsGui
    {
        private const float LabelWidth = 180f;
        private const float SliderWidth = 200f;
        private const float ValueWidth = 64f;
        private const int FontSize = 12;
        private const float RowHeight = 20f;

        private static readonly int[] MaxKeyChoices = { 4, 6, 8, 10, 12, 16, SolverOptions.AllKeys };
        private static readonly int[] BuiltInChoices = { 4, 8, 12, 16 };

        private static List<ViewerAdapter> probed = new List<ViewerAdapter>();
        private static bool rolesOpen;
        private static bool rolesOpenRequested;
        private static Layout layout;
        private static string layoutSource = "";
        private static string viewerNames = "";

        private static GUIStyle labelStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle idleStyle;
        private static GUIStyle disabledStyle;
        private static GUIStyle selectedStyle;
        private static GUIStyle activeRollStyle;
        private static GUIStyle sliderStyle;
        private static GUIStyle thumbStyle;
        private static Texture2D cardTexture;

        private static bool Korean
        {
            get
            {
                UiLanguage language = Main.Settings.Language;
                return language == UiLanguage.Korean
                    || (language == UiLanguage.Game && RDString.language == SystemLanguage.Korean);
            }
        }

        // 뷰어 조회는 리플렉션이라 프레임마다 돌리지 않고 창을 열 때만 한다.
        public static void OnShow(UnityModManager.ModEntry modEntry)
        {
            probed = ViewerHub.Probe();
        }

        public static void Draw(UnityModManager.ModEntry modEntry)
        {
            EnsureStyles();

            // IMGUI는 Layout과 그 뒤 이벤트의 컨트롤 수가 같아야 하므로 슬롯 목록과 펼침 상태는 Layout 때만 바꾼다.
            if (layout == null || Event.current.type == EventType.Layout)
            {
                rolesOpen = rolesOpenRequested;
                layout = ViewerHub.PreviewLayout(probed, out layoutSource, out viewerNames);
            }

            Settings settings = Main.Settings;
            DrawLanguage(settings);
            DrawRollStyle(settings);
            DrawHand(settings);
            DrawBase(settings);
            DrawMaxKeys(settings);
            DrawFloor(settings);
            DrawSplit(settings);
            DrawRoles(settings);
            DrawBuiltIn(settings);
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
            RollChoice(settings, RollStyle.Outward, T("안밖", "Outward"));
            RollChoice(settings, RollStyle.Inward, T("밖안", "Inward"));
            RollChoice(settings, RollStyle.Stair, T("계단", "Stair"));
            RollChoice(settings, RollStyle.ReverseStair, T("역계단", "Reverse stair"));
            EndRow();
        }

        private static void RollChoice(Settings settings, RollStyle style, string label)
        {
            // 노란색은 선택된 롤 방식과 내장 뷰어의 눌린 키에만 쓴다.
            GUIStyle look = settings.Style == style ? activeRollStyle : idleStyle;
            if (GUILayout.Button(label, look, GUILayout.ExpandWidth(false))) settings.Style = style;
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

        private static void DrawFloor(Settings settings)
        {
            BeginRow(T("같은 손가락 최소 간격", "Same-finger minimum interval"));
            float value = GUILayout.HorizontalSlider(
                settings.FloorMs, Settings.MinFloorMs, Settings.MaxFloorMs, sliderStyle, thumbStyle, GUILayout.Width(SliderWidth));
            settings.FloorMs = Mathf.RoundToInt(value);
            GUILayout.Label(settings.FloorMs + " ms", mutedStyle, GUILayout.Width(ValueWidth));
            EndRow();
        }

        private static void DrawSplit(Settings settings)
        {
            BeginRow(T("3연타 나누기", "Triplet split"));
            if (Choice(T("주손 두 손가락", "Main hand two fingers"), settings.Split == TripletSplit.MainTwoFingers))
            {
                settings.Split = TripletSplit.MainTwoFingers;
            }

            if (Choice(T("부손 두 손가락", "Off hand two fingers"), settings.Split == TripletSplit.OffTwoFingers))
            {
                settings.Split = TripletSplit.OffTwoFingers;
            }

            EndRow();
        }

        private static void DrawRoles(Settings settings)
        {
            string title = T("키 역할 편집", "Key role editor");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((rolesOpen ? "- " : "+ ") + title, rolesOpen ? selectedStyle : idleStyle, GUILayout.ExpandWidth(false)))
            {
                rolesOpenRequested = !rolesOpen;
            }

            if (rolesOpen && Choice(T("기본값으로", "Reset"), false))
            {
                int keyCount = layout.Slots.Length;
                settings.RoleOverrides.RemoveAll(item => item.KeyCount == keyCount);
            }

            GUILayout.EndHorizontal();
            if (!rolesOpen) return;

            for (int slot = 0; slot < layout.Slots.Length; slot++)
            {
                KeySlot key = layout.Slots[slot];
                BeginRow(key.Label);
                if (GUILayout.Button(RoleText(key), key.Enabled ? idleStyle : disabledStyle, GUILayout.Width(SliderWidth)))
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

        private static void DrawBuiltIn(Settings settings)
        {
            BeginRow(T("내장 뷰어 키", "Built-in viewer keys"));
            foreach (int keys in BuiltInChoices)
            {
                if (Choice(keys.ToString(), settings.BuiltInKeys == keys)) settings.BuiltInKeys = keys;
            }

            EndRow();

            float x = Slider(T("내장 뷰어 X", "Built-in viewer X"), settings.BuiltInX, 0f, 1f);
            float y = Slider(T("내장 뷰어 Y", "Built-in viewer Y"), settings.BuiltInY, 0f, 1f);
            float scale = Slider(T("내장 뷰어 크기", "Built-in viewer scale"), settings.BuiltInScale, 0.5f, 3f);
            if (x != settings.BuiltInX || y != settings.BuiltInY || scale != settings.BuiltInScale)
            {
                settings.BuiltInX = x;
                settings.BuiltInY = y;
                settings.BuiltInScale = scale;
                BuiltInViewer.ApplyPlacement();
            }
        }

        private static float Slider(string title, float value, float min, float max)
        {
            BeginRow(title);
            float result = GUILayout.HorizontalSlider(value, min, max, sliderStyle, thumbStyle, GUILayout.Width(SliderWidth));
            GUILayout.Label(result.ToString("0.00"), mutedStyle, GUILayout.Width(ValueWidth));
            EndRow();
            return result;
        }

        private static void DrawStatus()
        {
            Plan plan = Planner.Latest;
            string violations = Planner.Solving
                ? T("계산 중", "solving")
                : plan != null ? plan.Violations.ToString() : "-";
            string status = T("연결된 뷰어: ", "Viewers: ") + viewerNames
                + T(" | 배치: ", " | Layout: ") + layoutSource
                + T(" | 키 ", " | Keys: ") + layout.Slots.Length
                + T(" | 최소 간격 위반: ", " | Floor violations: ") + violations;
            GUILayout.Label(status, mutedStyle);
        }

        private static void BeginRow(string title)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, labelStyle, GUILayout.Width(LabelWidth), GUILayout.Height(RowHeight));
        }

        private static void EndRow()
        {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static bool Choice(string label, bool selected)
        {
            return GUILayout.Button(label, selected ? selectedStyle : idleStyle, GUILayout.ExpandWidth(false));
        }

        // 텍스처는 씬 전환의 UnloadUnusedAssets에 지워질 수 있어 HideAndDontSave로 만들고, 그래도 사라지면 다시 만든다.
        private static void EnsureStyles()
        {
            if (labelStyle != null && cardTexture != null) return;

            cardTexture = Box(Palette.Card, Palette.Border);
            Texture2D elevated = Box(Palette.Elevated, Palette.Border);
            Texture2D primary = Box(Palette.Primary, Palette.Primary);
            Texture2D thumb = Box(Palette.Secondary, Palette.Secondary);

            labelStyle = Text(Palette.Text);
            mutedStyle = Text(Palette.Tertiary);
            idleStyle = Button(cardTexture, Palette.Secondary, elevated, Palette.Text);
            disabledStyle = Button(cardTexture, Palette.Disabled, elevated, Palette.Text);
            selectedStyle = Button(elevated, Palette.Text, elevated, Palette.Text);
            activeRollStyle = Button(primary, Palette.Base, primary, Palette.Base);

            sliderStyle = new GUIStyle { border = new RectOffset(1, 1, 1, 1), fixedHeight = 16f, margin = new RectOffset(0, 4, 4, 4) };
            sliderStyle.normal.background = cardTexture;
            thumbStyle = new GUIStyle { border = new RectOffset(1, 1, 1, 1), fixedWidth = 10f, fixedHeight = 16f };
            thumbStyle.normal.background = thumb;
        }

        private static GUIStyle Text(Color32 color)
        {
            var style = new GUIStyle { fontSize = FontSize, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 4, 0, 0) };
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle Button(Texture2D normal, Color32 normalText, Texture2D hover, Color32 hoverText)
        {
            var style = new GUIStyle
            {
                fontSize = FontSize,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(8, 8, 0, 0),
                margin = new RectOffset(0, 2, 1, 1),
                fixedHeight = 24f,
            };
            style.normal.background = normal;
            style.normal.textColor = normalText;
            style.hover.background = hover;
            style.hover.textColor = hoverText;
            style.active.background = hover;
            style.active.textColor = hoverText;
            return style;
        }

        // 가운데가 면, 둘레가 1픽셀 테두리인 9분할 텍스처. 모서리 반경은 0이다.
        private static Texture2D Box(Color32 fill, Color32 border)
        {
            var texture = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    texture.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
