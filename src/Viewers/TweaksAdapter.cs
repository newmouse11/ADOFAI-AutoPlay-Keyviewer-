using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AutoFingering.Viewers
{
    // AdofaiTweaks의 공개 재생 API를 쓴다. 메인 스레드 전용이고, OnStartInputs가 실제 키 폴링을 멈추므로
    // OnEndInputs를 빠뜨리면 레벨이 끝난 뒤에도 실제 입력이 표시되지 않는다.
    internal sealed class TweaksAdapter : ViewerAdapter
    {
        private Action start;
        private Action finish;
        private Action<KeyCode> pressed;
        private Action<KeyCode> released;

        public override string Name => "AdofaiTweaks";

        public override bool TryBind()
        {
            Type replay = FindType("AdofaiTweaks", "AdofaiTweaks.Tweaks.KeyViewer.ReplayInput");
            if (replay == null) return false;

            PropertyInfo viewerProperty = replay.GetProperty("KeyViewer", PrivateStatic);
            if (viewerProperty == null) return Missing("ReplayInput.KeyViewer");

            // 트윅이 꺼져 있으면 내부 KeyViewer가 null이다.
            var viewer = viewerProperty.GetValue(null, null) as UnityEngine.Object;
            if (viewer == null) return false;

            object profile = viewer.GetType().GetProperty("Profile", PublicInstance)?.GetValue(viewer, null);
            var keys = profile?.GetType().GetProperty("ActiveKeys", PublicInstance)?.GetValue(profile, null)
                as List<KeyCode>;
            if (keys == null) return Missing("Profile.ActiveKeys");
            if (keys.Count == 0) return false;

            MethodInfo onStart = replay.GetMethod("OnStartInputs", PublicStatic);
            MethodInfo onEnd = replay.GetMethod("OnEndInputs", PublicStatic);
            MethodInfo onPressed = replay.GetMethod("OnKeyPressed", PublicStatic);
            MethodInfo onReleased = replay.GetMethod("OnKeyReleased", PublicStatic);
            if (onStart == null || onEnd == null || onPressed == null || onReleased == null)
            {
                return Missing("ReplayInput.On*");
            }

            start = (Action)Delegate.CreateDelegate(typeof(Action), onStart);
            finish = (Action)Delegate.CreateDelegate(typeof(Action), onEnd);
            pressed = (Action<KeyCode>)Delegate.CreateDelegate(typeof(Action<KeyCode>), onPressed);
            released = (Action<KeyCode>)Delegate.CreateDelegate(typeof(Action<KeyCode>), onReleased);

            SetKeys(keys.ToArray());
            return true;
        }

        public override void Begin()
        {
            start();
        }

        public override void Press(int index)
        {
            pressed((KeyCode)KeyCodes[index]);
        }

        public override void Release(int index)
        {
            released((KeyCode)KeyCodes[index]);
        }

        public override void End()
        {
            finish();
        }
    }
}
