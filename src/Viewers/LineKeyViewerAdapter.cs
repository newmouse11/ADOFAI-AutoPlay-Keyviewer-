using System;
using System.Reflection;
using UnityEngine;

namespace AutoFingering.Viewers
{
    internal sealed class LineKeyViewerAdapter : ViewerAdapter
    {
        private MethodInfo checkKey;

        public override string Name => "LineKeyViewer";

        public override bool TryBind()
        {
            Type main = FindType("LineKeyViewer", "LineKeyViewer.Main");
            if (main == null) return false;

            FieldInfo viewerField = main.GetField("KeyViewer", PublicStatic);
            if (viewerField == null) return Missing("Main.KeyViewer");

            // 비활성화하면 null이 되고, 파괴된 Unity 객체도 == 연산으로 null 취급해야 하므로 형변환해서 비교한다.
            var viewer = viewerField.GetValue(null) as UnityEngine.Object;
            if (viewer == null) return false;

            object setting = main.GetField("Setting", PublicStatic)?.GetValue(null);
            var codes = setting?.GetType().GetField("KeyCodes", PublicInstance)?.GetValue(setting) as KeyCode[];
            if (codes == null || codes.Length == 0) return Missing("Setting.KeyCodes");

            checkKey = main.Assembly.GetType("LineKeyViewer.KeyInputManager", false)
                ?.GetMethod("CheckKey", PrivateStatic, null, KeyCodeArg, null);
            if (checkKey == null) return Missing("KeyInputManager.CheckKey");

            SetKeys(codes);
            return true;
        }

        public override void Begin()
        {
            PollKeys.Patch(checkKey);
        }

        public override void Press(int index)
        {
            PollKeys.Set(KeyCodes[index], true);
        }

        public override void Release(int index)
        {
            PollKeys.Set(KeyCodes[index], false);
        }

        public override void End()
        {
            foreach (int code in KeyCodes) PollKeys.Set(code, false);
        }
    }
}
