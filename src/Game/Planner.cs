using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFingering.Game
{
    using AutoFingering.Core;

    internal sealed class Plan
    {
        public ulong Key;
        public TilePlan Tiles;
        public byte[] Slot;
        // 이벤트별로 같은 슬롯을 다음에 쓰기까지 남은 실제 초. 없으면 양의 무한대.
        public float[] Gap;
        public int Violations;
    }

    internal static class Planner
    {
        // 키에 설정과 배치가 들어 있어 설정을 바꾸면 자연히 빗나간다.
        // ponytail: 재시작 대비 캐시는 마지막 계획 하나뿐이다. 두 레벨을 번갈아 돌려 재계산이 거슬리면 LRU로 늘린다.
        private static volatile Plan latest;
        private static volatile string failure;
        private static CancellationTokenSource running;
        private static Task runningTask;
        private static ulong runningKey;

        public static Plan Latest => latest;

        public static bool Solving => runningTask != null && !runningTask.IsCompleted;

        public static Plan Get(ulong key)
        {
            Plan plan = latest;
            return plan != null && plan.Key == key ? plan : null;
        }

        // 층 데이터는 Unity 객체라 메인 스레드에서 값으로 복사한 뒤 넘긴다.
        public static ulong Request(Layout layout)
        {
            List<scrFloor> floors = ADOBase.lm.listFloors;
            var tiles = new TileInfo[floors.Count];
            for (int i = 0; i < tiles.Length; i++)
            {
                scrFloor floor = floors[i];
                tiles[i] = new TileInfo
                {
                    SeqId = floor.seqID,
                    EntryTime = floor.entryTime,
                    AngleLength = floor.angleLength,
                    ExtraBeats = floor.extraBeats,
                    MidSpin = floor.midSpin,
                    HoldLength = floor.holdLength,
                    TapsNeeded = floor.tapsNeeded,
                    Auto = floor.auto,
                };
            }

            double pitch = ADOBase.conductor.song.pitch;
            bool hitOnce = Persistence.multiTapTileBehavior == MultitapTileBehavior.HitOnce;
            SolverOptions options = Main.Settings.ToOptions();
            ulong key = Hash(tiles, pitch, hitOnce, options, layout);

            if (Solving && runningKey == key) return key;

            // 캐시가 맞아도 다른 키로 도는 계산은 멈춘다. 놔두면 끝날 때 latest를 덮어써 지금 쓰는 계획이 밀려난다.
            Cancel();
            if (Get(key) != null) return key;

            if (!(pitch > 0))
            {
                // 음높이가 0이면 시간이 정의되지 않는다. 불을 켜지 않을 뿐 게임은 계속 간다.
                Log.Once("plan", "pitch " + pitch + " 레벨은 운지를 계산하지 않음");
                return key;
            }

            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            running = source;
            runningKey = key;
            runningTask = Task.Run(() => Solve(tiles, pitch, hitOnce, layout, options, key, token));
            return key;
        }

        public static void Cancel()
        {
            running?.Cancel();
            running = null;
            // 끝나 가는 취소 작업을 진행 중으로 보면 같은 키의 다음 요청이 영영 계산되지 않는다.
            runningTask = null;
        }

        // UMM 로거의 history는 List라 스레드 안전하지 않다. 작업 스레드는 문자열만 남기고 기록은 메인 스레드가 한다.
        public static void ReportFailure()
        {
            string message = failure;
            if (message == null) return;
            failure = null;
            Log.Once("solver", message);
        }

        private static void Solve(
            TileInfo[] tiles,
            double pitch,
            bool hitOnce,
            Layout layout,
            SolverOptions options,
            ulong key,
            CancellationToken token)
        {
            try
            {
                TilePlan tilePlan = TileClassifier.Build(tiles, pitch, hitOnce);
                Solution solution = FingeringSolver.Solve(tilePlan.Events, layout, options, token);
                var plan = new Plan
                {
                    Key = key,
                    Tiles = tilePlan,
                    Slot = solution.Slot,
                    Gap = KeyScheduler.BuildGaps(tilePlan.Events, solution.Slot, layout.Slots.Length),
                    Violations = solution.FloorViolations,
                };

                if (!token.IsCancellationRequested) latest = plan;
            }
            catch (OperationCanceledException)
            {
                // 새 시도가 시작되어 필요 없어진 계산이다. 결과를 버리는 것이 곧 처리다.
                return;
            }
            catch (Exception e)
            {
                failure = "운지 계산 실패 (타일 " + tiles.Length + "개, 슬롯 " + layout.Slots.Length + "개): " + e;
            }
        }

        private static ulong Hash(TileInfo[] tiles, double pitch, bool hitOnce, SolverOptions options, Layout layout)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, tiles.Length);
            foreach (TileInfo tile in tiles)
            {
                h = Mix(h, BitConverter.DoubleToInt64Bits(tile.EntryTime));
                h = Mix(h, BitConverter.DoubleToInt64Bits(tile.AngleLength));
                h = Mix(h, BitConverter.DoubleToInt64Bits(tile.ExtraBeats));
                h = Mix(h, tile.HoldLength);
                h = Mix(h, tile.TapsNeeded);
                h = Mix(h, (tile.MidSpin ? 1 : 0) | (tile.Auto ? 2 : 0));
            }

            h = Mix(h, BitConverter.DoubleToInt64Bits(pitch));
            h = Mix(h, hitOnce ? 1 : 0);
            h = Mix(h, (long)options.Style);
            h = Mix(h, (long)options.MainHand);
            h = Mix(h, options.Base);
            h = Mix(h, options.MaxKeys);
            h = Mix(h, BitConverter.DoubleToInt64Bits(options.Floor));
            foreach (KeySlot slot in layout.Slots)
            {
                h = Mix(h, slot.KeyCode);
                h = Mix(h, (long)slot.Hand);
                h = Mix(h, (long)slot.Finger);
                h = Mix(h, slot.Enabled ? 1 : 0);
            }

            return h;
        }

        private static ulong Mix(ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
                return hash ^ (hash >> 29);
            }
        }
    }
}
