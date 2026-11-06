using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoFingering.Game
{
    using AutoFingering.Core;

    // UMM 창이 열려 있어도 게임은 키 입력을 그대로 처리한다. 에디터에서는 글자 키가 타일을 고치고
    // 커스텀 레벨 선택에서는 키가 레벨을 시작하므로, 연타해도 잃을 것이 없는 메인 메뉴에서만 잰다.
    // 메인 메뉴에서도 연타하면 행성이 걸어가 포털을 타므로 재는 동안 게임의 입력 스위치를 끈다.
    internal static class SpeedRunner
    {
        public enum Status { Idle, Waiting, Measuring, Done, TooFew, NeedMainMenu, Interrupted }

        private static readonly KeyCode[] Keys = KeyboardKeys();

        private static SpeedTest test;
        private static bool playerWasResponsive;

        public static Status Current { get; private set; }

        public static double ResultBpm { get; private set; }

        public static bool Running => Current == Status.Waiting || Current == Status.Measuring;

        public static double Remaining => test.Remaining(Time.unscaledTime);

        public static double LiveBpm => test.LiveBpm();

        public static void Start()
        {
            if (!InMainMenu)
            {
                Current = Status.NeedMainMenu;
                return;
            }

            test = new SpeedTest();
            Current = Status.Waiting;
            playerWasResponsive = ADOBase.controller.playerOne.responsive;
            ADOBase.playerManager.SetAllPlayerResponsive(false);
        }

        // 진행 중이면 멈추고, 끝난 테스트의 문구도 지운다. 값을 직접 바꾼 뒤에는 이전 결과가 틀린 말이 되기 때문이다.
        public static void Reset()
        {
            Finish(Status.Idle);
        }

        public static void Tick()
        {
            if (!Running) return;

            if (!InMainMenu)
            {
                Finish(Status.Interrupted);
                return;
            }

            double now = Time.unscaledTime;
            foreach (KeyCode key in Keys)
            {
                if (Input.GetKeyDown(key)) test.Press((int)key, now);
            }

            if (test.Started) Current = Status.Measuring;
            if (!test.IsOver(now)) return;

            double bpm = test.Bpm();
            if (bpm <= 0)
            {
                Finish(Status.TooFew);
                return;
            }

            Main.Settings.SetTwoKeyBpm(bpm);
            ResultBpm = bpm;
            Finish(Status.Done);
        }

        // 측정이 끝나는 모든 길이 여기를 지난다. 스위치가 꺼진 채 남으면 메뉴에서 아무것도 할 수 없다.
        // 다른 화면으로 넘어갔으면 그 화면의 플레이어는 새것이고 스위치도 그 화면 몫이므로 건드리지 않는다.
        private static void Finish(Status status)
        {
            if (Running && InMainMenu) ADOBase.playerManager.SetAllPlayerResponsive(playerWasResponsive);
            Current = status;
        }

        private static bool InMainMenu => ADOBase.sceneName == GCNS.sceneLevelSelect && ADOBase.controller != null;

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
