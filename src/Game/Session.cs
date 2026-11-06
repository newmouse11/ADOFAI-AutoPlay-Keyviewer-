using System.Collections.Generic;
using AutoFingering.Viewers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoFingering.Game
{
    using AutoFingering.Core;

    // 한 번의 시도(attempt) 수명을 관리한다. scrFloor 참조는 시도 사이에 재사용되거나 사라지므로 seq만 들고 있는다.
    internal static class Session
    {
        public static bool AutoSnapshot;

        private static bool active;
        private static bool ended;
        private static Scene scene;
        private static ulong planKey;
        private static Plan plan;
        private static KeyScheduler scheduler;

        public static void OnAttemptStart(scrController controller)
        {
            if (!controller.gameworld) return;

            Clear();
            ended = false;
            if (RDC.auto) Begin();
        }

        public static void OnLevelEnded()
        {
            ended = true;
        }

        public static void OnFail()
        {
            ended = true;
            Clear();
        }

        public static void OnLanding(scrPlanet planet, scrFloor floor)
        {
            if (!AutoSnapshot) return;

            scrController controller = ADOBase.controller;
            if (controller == null || planet.player != controller.playerOne) return;

            // 자유 이동 격자 타일은 부모의 seqID를 공유하지만 listFloors에는 없다. 동일성 비교로 걸러낸다.
            List<scrFloor> floors = ADOBase.lm.listFloors;
            int seq = floor.seqID;
            if (seq < 0 || seq >= floors.Count || floors[seq] != floor) return;

            if (!active)
            {
                // 에디터에서는 플레이 도중에도 Otto 버튼으로 자동을 켤 수 있다. 다음 시도까지 기다리지 않고 지금 준비한다.
                if (!ended && controller.gameworld) Begin();
                return;
            }

            // 한번 잡은 계획은 시도가 끝날 때까지 쥐고 있는다. 다른 계산이 캐시를 덮어써도 켜 둔 홀드를 끌 수 있어야 한다.
            if (plan == null) plan = Planner.Get(planKey);
            if (plan == null || seq >= plan.Tiles.FirstEvent.Length) return;

            SyncClock();

            int release = plan.Tiles.ReleaseEvent[seq];
            if (release >= 0 && plan.Slot[release] != Solution.NoSlot)
            {
                scheduler.ReleaseHold(plan.Slot[release], release);
            }

            int first = plan.Tiles.FirstEvent[seq];
            int end = first + plan.Tiles.EventCount[seq];
            for (int e = first; e < end; e++)
            {
                byte slot = plan.Slot[e];
                if (slot == Solution.NoSlot) continue;
                scheduler.Press(slot, e, plan.Tiles.Events[e].ReleaseTime >= 0, plan.Gap[e]);
            }
        }

        public static void Tick()
        {
            Planner.ReportFailure();
            if (!active) return;

            // controller 게터는 없을 때 FindAnyObjectByType을 돌리므로 active일 때만 읽는다.
            if (!RDC.auto || ADOBase.controller == null || SceneManager.GetActiveScene() != scene)
            {
                Clear();
                return;
            }

            SyncClock();
            scheduler.Tick();

            // 연습 모드는 포털을 앞당기므로 뗄 착지가 안 오는 홀드가 남을 수 있다. 탭만 끝나면 함께 정리한다.
            if (ended && scheduler.TapsDrained) Clear();
        }

        public static void Clear()
        {
            if (!active) return;

            active = false;
            plan = null;
            scheduler.Reset();
            ViewerHub.End();
        }

        private static void Begin()
        {
            scene = SceneManager.GetActiveScene();
            Layout layout = ViewerHub.Bind();
            scheduler = new KeyScheduler(layout.Slots.Length, ViewerHub.Fire);
            active = true;
            planKey = Planner.Request(layout);
        }

        private static void SyncClock()
        {
            scheduler.SetClock(Time.time, Time.frameCount, Time.unscaledDeltaTime);
        }
    }
}
