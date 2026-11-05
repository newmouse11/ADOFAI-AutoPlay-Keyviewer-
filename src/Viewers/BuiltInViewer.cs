using System;
using UnityEngine;
using UnityEngine.UI;

namespace AutoFingering.Viewers
{
    // 연결된 뷰어가 하나도 없을 때만 쓰는 자체 뷰어.
    internal sealed class BuiltInViewer : ViewerAdapter
    {
        private const float CellSize = 32f;
        private const float CellGap = 2f;
        private const float PanelPadding = 4f;
        private const int TopRowKeys = 8;
        private const int LabelSize = 12;

        private static readonly string[] FontNames = { "IBM Plex Sans", "Noto Sans KR", "Malgun Gothic", "Arial" };

        // 12키 이후의 아랫줄은 배열 순서와 키보드 위치가 다르다(왼쪽 손날이 배열 끝에 있다).
        private static readonly int[] BottomOrder16 = { 12, 13, 8, 9, 10, 11, 14, 15 };

        private static readonly KeyCode[] Preset16 =
        {
            KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon,
            KeyCode.C, KeyCode.V, KeyCode.N, KeyCode.M, KeyCode.Z, KeyCode.X, KeyCode.Comma, KeyCode.Period,
        };

        private static readonly string[] PresetLabels16 =
        {
            "A", "S", "D", "F", "J", "K", "L", ";", "C", "V", "N", "M", "Z", "X", ",", ".",
        };

        private static Font font;
        private static BuiltInViewer shown;

        private GameObject root;
        private RectTransform panel;
        private Image[] fills;
        private Text[] texts;

        // 설정의 프리셋으로 만든다.
        public BuiltInViewer()
        {
            int keys = Main.Settings.BuiltInKeys;
            if (keys == 4)
            {
                KeyCodes = new[] { (int)KeyCode.D, (int)KeyCode.F, (int)KeyCode.J, (int)KeyCode.K };
                Labels = new[] { "D", "F", "J", "K" };
                return;
            }

            int count = keys == 12 || keys == 16 ? keys : 8;
            KeyCodes = new int[count];
            Labels = new string[count];
            for (int i = 0; i < count; i++)
            {
                KeyCodes[i] = (int)Preset16[i];
                Labels[i] = PresetLabels16[i];
            }
        }

        // 뷰어가 시도 도중 떨어졌을 때 이미 풀어 둔 배치를 그대로 보여 주기 위해 쓴다.
        public BuiltInViewer(int[] keyCodes, string[] labels)
        {
            KeyCodes = keyCodes;
            Labels = labels;
        }

        public override string Name => "Built-in";

        public override bool TryBind()
        {
            return true;
        }

        public override void Begin()
        {
            if (font == null) font = Font.CreateDynamicFontFromOSFont(FontNames, LabelSize);

            root = new GameObject("AutoFingering.Viewer");
            UnityEngine.Object.DontDestroyOnLoad(root);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            int count = KeyCodes.Length;
            int top = Math.Min(count, TopRowKeys);
            int bottom = count - top;
            float width = RowWidth(Math.Max(top, bottom)) + PanelPadding * 2;
            float height = RowWidth(bottom > 0 ? 2 : 1) + PanelPadding * 2;

            panel = NewImage("Panel", root.transform, Palette.Surface).rectTransform;
            panel.sizeDelta = new Vector2(width, height);

            fills = new Image[count];
            texts = new Text[count];
            for (int i = 0; i < top; i++)
            {
                AddKey(i, PanelPadding + i * (CellSize + CellGap), 0);
            }

            float bottomStart = (width - RowWidth(bottom)) / 2;
            for (int i = 0; i < bottom; i++)
            {
                int index = count == BottomOrder16.Length * 2 ? BottomOrder16[i] : top + i;
                AddKey(index, bottomStart + i * (CellSize + CellGap), 1);
            }

            shown = this;
            ApplyPlacement();
        }

        public override void Press(int index)
        {
            fills[index].color = Palette.Primary;
            texts[index].color = Palette.Base;
        }

        public override void Release(int index)
        {
            fills[index].color = Palette.Card;
            texts[index].color = Palette.Text;
        }

        public override void End()
        {
            if (shown == this) shown = null;
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        public static void ApplyPlacement()
        {
            if (shown == null || shown.panel == null) return;

            Settings settings = Main.Settings;
            var anchor = new Vector2(settings.BuiltInX, settings.BuiltInY);
            shown.panel.anchorMin = anchor;
            shown.panel.anchorMax = anchor;
            shown.panel.anchoredPosition = Vector2.zero;
            shown.panel.localScale = Vector3.one * settings.BuiltInScale;
        }

        private static float RowWidth(int cells)
        {
            return cells * CellSize + Math.Max(cells - 1, 0) * CellGap;
        }

        private void AddKey(int index, float x, int row)
        {
            RectTransform outline = NewImage("Key", panel, Palette.Border).rectTransform;
            outline.anchorMin = new Vector2(0f, 1f);
            outline.anchorMax = new Vector2(0f, 1f);
            outline.pivot = new Vector2(0f, 1f);
            outline.sizeDelta = new Vector2(CellSize, CellSize);
            outline.anchoredPosition = new Vector2(x, -(PanelPadding + row * (CellSize + CellGap)));

            Image fill = NewImage("Fill", outline, Palette.Card);
            Stretch(fill.rectTransform, 1f);
            fills[index] = fill;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(outline, false);
            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = LabelSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Palette.Text;
            label.raycastTarget = false;
            // 프리셋 밖 배치(대체 표시)에서는 "RightShift" 같은 긴 이름이 올 수 있다.
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 6;
            label.resizeTextMaxSize = LabelSize;
            label.text = Labels[index];
            Stretch(label.rectTransform, 1f);
            texts[index] = label;
        }

        private static Image NewImage(string name, Transform parent, Color32 color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
