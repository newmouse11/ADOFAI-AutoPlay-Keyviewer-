using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoFingering.Game
{
    using AutoFingering.Core;

    // UMM 창이 열려 있어도 게임은 키 입력을 그대로 처리한다. 에디터에서는 글자 키가 타일을 고치고
    // 커스텀 레벨 선택에서는 키가 레벨을 시작하므로, 연타해도 잃을 것이 없는 메인 메뉴에서만 잰다.
    internal static class SpeedRunner
    {
        public enum Status { Idle, Waiting, Measuring, Done, TooFew, NeedMainMenu, Interrupted }

        private static readonly KeyCode[] Keys = KeyboardKeys();

        private static SpeedTest test;

        public static Status Current { get; private set; }

        public static double ResultKps { get; private set; }

        public static bool Running => Current == Status.Waiting || Current == Status.Measuring;

        public static double Remaining => test.Remaining(Time.unscaledTime);

        public static double LiveKps => test.LiveKps();

        public static void Start()
        {
            if (!InMainMenu)
            {
                Current = Status.NeedMainMenu;
                return;
            }

            test = new SpeedTest();
            Current = Status.Waiting;
        }

        // 진행 중이면 멈추고, 끝난 테스트의 문구도 지운다. 값을 직접 바꾼 뒤에는 이전 결과가 틀린 말이 되기 때문이다.
        public static void Reset()
        {
            Current = Status.Idle;
        }

        public static void Tick()
        {
            if (!Running) return;

            if (!InMainMenu)
            {
                Current = Status.Interrupted;
                return;
            }

            double now = Time.unscaledTime;
            foreach (KeyCode key in Keys)
            {
                if (Input.GetKeyDown(key)) test.Press((int)key, now);
            }

            if (test.Started) Current = Status.Measuring;
            if (!test.IsOver(now)) return;

            double kps = test.Kps();
            if (kps <= 0)
            {
                Current = Status.TooFew;
                return;
            }

            Main.Settings.SetTwoKeyKps(kps);
            ResultKps = kps;
            Current = Status.Done;
        }

        private static bool InMainMenu => ADOBase.sceneName == GCNS.sceneLevelSelect;

        // 마우스와 조이스틱은 빼야 테스트 버튼을 누른 클릭이 입력으로 잡히지 않는다. Esc는 메뉴를 여는 키라 뺀다.
        private static KeyCode[] KeyboardKeys()
        {
            var keys = new List<KeyCode>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key != KeyCode.None && key != KeyCode.Escape && key < KeyCode.Mouse0) keys.Add(key);
            }

            return keys.ToArray();
        }
    }
}
