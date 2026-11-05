using System;
using System.Threading;

namespace AutoFingering.Core
{
    public static class FingeringSolver
    {
        private const double BurstGap = 0.4;
        private const double BusyCost = 1000;
        private const double OnBeatTolerance = 0.02;
        private const double BurstOffStartCost = 0.001;

        // 긴 틈 뒤 리셋은 부손 시작의 정박 비용(0.1)을 반으로 나눈 값보다 커야 한다. 0.05면 느린 곧은 타일이
        // 매번 주손 검지로 리셋해 한 키만 켜지고, 150 BPM 근처에서는 교대와 뒤섞인다.
        private const double BurstReset = 0.06;

        // 게임의 entryTime은 누적 합이라 정확한 배수에서도 오차가 난다. 경계값에서 tier가 흔들리지 않게 비교에 여유를 둔다.
        private const double TimeEpsilon = 1e-9;

        // 리셋 부채: 리셋을 거듭한 경로는 다음 리셋이 비싸진다. 없으면 롱노트 하나로 표 순서를 잃은 경로가
        // 표 순서로 돌아오지 못한 채 연타 끝까지 리셋을 반복한다. 자연 진행으로 천천히 갚고 긴 틈에서 0이 된다.
        // 대가: 쉼 없이 빨라지는 구간에서는 리셋이 같은 손가락 비용보다 비싸져 위반이 드물게 생긴다.
        // 위반 한 번보다 스타일 순서를 끝까지 잃는 쪽이 더 나빠 이쪽을 택했다.
        private const double DebtPerReset = 1;
        private const double DebtRepayPerStep = 0.1;

        // 이 간격 안에서 이어지고 전체 폭도 이 안이면 사람은 한 번에 누른다(동타).
        // 폭이 넘으면 묶음 전체가 빠른 연타다. 연타를 잘라 동타로 보면 안 된다.
        private const double ChordGap = 0.012;
        private const double ChordSpan = 0.035;

        // Floor는 손가락을 더 쓰는 기준이고, 이 비율까지는 같은 손가락으로 실제로 칠 수 있다.
        // 동타를 다시 치는 간격과 엄지·손바닥 키를 꺼내는 기준, 위반 집계가 모두 이 값을 쓴다.
        private const double HardFloorRatio = 0.75;

        // 네 손가락으로 칠 수 있는데 아래 키를 쓰는 경로를 막는다. 같은 손가락 비용의 최댓값(약 62)보다 작으면 뚫린다.
        private const double NeedlessExtraKeyCost = 70;

        // 동타에서 아래 키 하나의 값. 약지·새끼를 빼고 아래 키를 넣는 조합을 막을 만큼 크고,
        // 손가락 수를 넘는 동타에서 꼭 필요한 아래 키를 막지 않을 만큼 작다.
        private const double ChordExtraKeyCost = 1;
        private const double ChordOffHandTieBreak = 0.001;

        // 두 간격이 이 비율 안이면 같은 간격으로 본다.
        private const double SameGapRatio = 0.1;

        // Finger 열거형 순서: Index, Middle, Ring, Pinky, Thumb1, Thumb2, Palm1, Palm2
        private static readonly double[] WeakFingerCost = { 0, 0.05, 0.2, 0.3, 0.25, 0.3, 0.6, 0.7 };

        public static Solution Solve(
            PressEvent[] events,
            Layout layout,
            SolverOptions options,
            CancellationToken token = default(CancellationToken))
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            Tier[] tiers = StyleCycle.BuildTiers(layout, options);
            var result = new Solution { Slot = new byte[events.Length] };
            if (events.Length == 0) return result;
            if (tiers.Length == 0)
            {
                // 활성 키가 하나도 없으면 풀 문제가 없다. 호출자는 NoSlot을 "불 안 켬"으로 처리한다.
                for (int i = 0; i < result.Slot.Length; i++) result.Slot[i] = Solution.NoSlot;
                return result;
            }

            StateTable states = StateTable.Build(tiers, layout);
            int[] chordSize = FindChords(events);
            int finalState;
            byte[] back = RunViterbi(events, chordSize, layout.Slots.Length, tiers, states, options, token, out finalState);
            Backtrack(back, finalState, states, result.Slot);
            result.FloorViolations = CountViolations(events, result.Slot, layout.Slots.Length, options.Floor * HardFloorRatio);
            return result;
        }

        private static byte[] RunViterbi(
            PressEvent[] events,
            int[] chordSize,
            int slots,
            Tier[] tiers,
            StateTable states,
            SolverOptions options,
            CancellationToken token,
            out int finalState)
        {
            int n = events.Length;
            int count = states.Count;
            double floor = options.Floor;
            double comfort = floor * (options.Base == 4 ? 2 : 1);
            double hardFloor = floor * HardFloorRatio;
            bool[] extraKeysNeeded = FindExtraKeyNeed(events, chordSize, FingerKeyCount(tiers), hardFloor);
            double[,] excess = BuildExcessTable(tiers);

            // ponytail: 역추적표가 N*S 바이트다(100만 이벤트, 16키면 약 61MB). 더 긴 레벨이 필요하면 체크포인트 역추적으로 바꾼다.
            var back = new byte[checked(n * count)];
            var cost = new double[count];
            var nextCost = new double[count];
            // 시간은 누적되므로 float면 긴 레벨에서 ms 단위 오차가 Floor 판정에 섞인다. 그래서 double로 둔다.
            var busy = new double[count * slots];
            var nextBusy = new double[count * slots];
            for (int i = 0; i < busy.Length; i++) busy[i] = double.NegativeInfinity;

            var debt = new double[count];
            var nextDebt = new double[count];
            int best = 0;
            for (int i = 0; i < n; i++)
            {
                if ((i & 0xFFF) == 0) token.ThrowIfCancellationRequested();

                PressEvent press = events[i];
                bool first = i == 0;
                double gap = first ? double.PositiveInfinity : press.Time - events[i - 1].Time;
                int chord = chordSize[i];

                // 동타의 첫 음은 손을 새로 놓는 순간이다. 긴 틈처럼 시작 위치로 돌아가야 매번 같은 손가락 묶음을 잡는다.
                bool chordStart = chord > 0 && (first || chordSize[i - 1] == 0 || gap > ChordGap + TimeEpsilon);
                bool burst = chordStart || gap >= BurstGap - TimeEpsilon;
                double reset = first ? 0 : burst ? BurstReset : ResetCost(gap, floor);
                bool onBeat = IsOnBeat(press.Beat);
                bool ternary = HasTernaryNeighbor(events, i) && !RepeatsEveryTwoNotes(events, i);
                bool binary = !ternary && !RepeatsEveryThreeNotes(events, i);
                // 동타는 손가락을 돌려 쓸 수 없으니 같은 손가락으로 다시 친다. 기반은 리듬 개념이라 동타에는 적용하지 않는다.
                double eventFloor = chord > 0 ? hardFloor : floor;
                double eventComfort = chord > 0 ? hardFloor : comfort;
                int needed = NeededTier(events, i, tiers, eventComfort);
                double occupiedUntil = press.ReleaseTime >= 0 ? press.ReleaseTime : press.Time;

                double nextBestCost = double.PositiveInfinity;
                int nextBest = 0;
                for (int s = 0; s < count; s++)
                {
                    int slot = states.Slot[s];
                    int natural = first ? s : states.Previous[s];
                    int pred = natural;

                    // 같은 손가락 비용까지 합쳐 비교해야 표 순서로 돌아오는 경로가 살아남는다.
                    // 비용 없이 비교하면 잠시 2.0 넘게 뒤처진 경로가 다음 이벤트에 덮어써진다.
                    // 동타의 첫 음에서는 앞 묶음에서 이어 가는 경로에 이득을 주지 않는다. 이득이 있으면 값이 같은 두 조합을 번갈아 고른다.
                    double total = cost[pred]
                        + (chordStart ? reset : 0)
                        + SameFingerCost(press.Time, busy[pred * slots + slot], eventFloor, hardFloor, eventComfort);
                    if (!first)
                    {
                        double viaReset = cost[best]
                            + (chordStart ? reset : reset * (1 + debt[best]))
                            + SameFingerCost(press.Time, busy[best * slots + slot], eventFloor, hardFloor, eventComfort);
                        if (viaReset < total || (viaReset == total && best < pred))
                        {
                            pred = best;
                            total = viaReset;
                        }
                    }

                    if (!extraKeysNeeded[i] && states.UsesExtraKeys[s]) total += NeedlessExtraKeyCost;
                    total += excess[states.TierIndex[s], needed];
                    total += states.WeakCost[s];

                    // 동타 안의 음은 한 번에 누르므로 시작 위치, 박자, 리듬 단위는 의미가 없다. 어떤 손가락을 쓰는지만 본다.
                    if (chord > 0)
                    {
                        if (states.IsExtraKey[s]) total += ChordExtraKeyCost;

                        // 값이 같은 조합(왼손 약지를 뺄지 오른손 약지를 뺄지)이 묶음마다 바뀌지 않도록 주손 쪽으로 동률을 깬다.
                        if (states.IsOffHand[s]) total += ChordOffHandTieBreak;
                    }
                    else
                    {
                        if (burst) total += states.BurstCost[s];
                        if (onBeat) total += states.BeatCost[s];
                        if (ternary) total += states.TernaryMismatch[s];
                        else if (binary) total += states.BinaryMismatch[s];
                    }

                    nextDebt[s] = burst
                        ? 0
                        : pred != natural
                            ? debt[pred] + DebtPerReset
                            : Math.Max(0, debt[pred] - DebtRepayPerStep);

                    nextCost[s] = total;
                    back[i * count + s] = (byte)pred;
                    Array.Copy(busy, pred * slots, nextBusy, s * slots, slots);
                    nextBusy[s * slots + slot] = occupiedUntil;

                    // 동률이면 낮은 상태 번호가 남아 같은 입력은 항상 같은 결과를 낸다.
                    if (total < nextBestCost)
                    {
                        nextBestCost = total;
                        nextBest = s;
                    }
                }

                double[] swapCost = cost;
                cost = nextCost;
                nextCost = swapCost;
                double[] swapBusy = busy;
                busy = nextBusy;
                nextBusy = swapBusy;
                double[] swapDebt = debt;
                debt = nextDebt;
                nextDebt = swapDebt;
                best = nextBest;
            }

            finalState = best;
            return back;
        }

        // 끊김 없이 이어지고 간격이 짧을수록 가파른 곡선이어야 한다. 경계에 턱이 있으면 2ms 모자란 연타에서
        // 일부러 같은 키를 겹쳐 쳐서 타이밍을 미는 경로가 오히려 싸져 말이 안 되는 타법이 나온다.
        private static double SameFingerCost(double time, double occupiedUntil, double floor, double hardFloor, double comfort)
        {
            if (time < occupiedUntil) return BusyCost;

            double dt = time - occupiedUntil;
            if (dt < hardFloor - TimeEpsilon) return 2.25 + 80 * (hardFloor - dt) / floor;
            if (dt < floor - TimeEpsilon) return 1 + 5 * (floor - dt) / floor;
            if (dt < comfort - TimeEpsilon) return 0.5 * (comfort - dt) / comfort;
            return 0;
        }

        private static double ResetCost(double gap, double floor)
        {
            return gap >= floor - TimeEpsilon ? 0.6 : 2.0;
        }

        // 이어지는 짧은 간격의 묶음을 찾아 이벤트별 묶음 크기를 돌려준다. 동타가 아니면 0이다.
        private static int[] FindChords(PressEvent[] events)
        {
            var sizes = new int[events.Length];
            int start = 0;
            for (int i = 1; i <= events.Length; i++)
            {
                if (i < events.Length && events[i].Time - events[i - 1].Time <= ChordGap + TimeEpsilon) continue;

                int count = i - start;
                if (count >= 2 && events[i - 1].Time - events[start].Time <= ChordSpan + TimeEpsilon)
                {
                    for (int j = start; j < i; j++) sizes[j] = count;
                }

                start = i;
            }

            return sizes;
        }

        // 엄지·손바닥 키 없이 쓸 수 있는 가장 큰 tier의 키 수. 없으면 0이다.
        private static int FingerKeyCount(Tier[] tiers)
        {
            int keys = 0;
            foreach (Tier tier in tiers)
            {
                if (!tier.UsesExtraKeys && tier.K > keys) keys = tier.K;
            }

            return keys;
        }

        // 동타는 손가락 수를 넘을 때만, 연타는 네 손가락을 다 돌려도 실제로 칠 수 없을 때만 아래 키를 꺼낸다.
        // 경계에서 앞뒤로 네 손가락 한 바퀴만큼 넓힌다. 그 타에서 바로 끊으면 방금 친 손가락을 곧바로 다시 치게 된다.
        private static bool[] FindExtraKeyNeed(PressEvent[] events, int[] chordSize, int fingerKeys, double hardFloor)
        {
            var need = new bool[events.Length];
            if (fingerKeys == 0)
            {
                for (int i = 0; i < need.Length; i++) need[i] = true;
                return need;
            }

            var core = new bool[events.Length];
            for (int i = 0; i < core.Length; i++)
            {
                core[i] = chordSize[i] > 0
                    ? chordSize[i] > fingerKeys
                    : i >= fingerKeys && events[i].Time - events[i - fingerKeys].Time < hardFloor - TimeEpsilon;
            }

            int until = -1;
            for (int i = 0; i < need.Length; i++)
            {
                if (core[i]) until = i + fingerKeys;
                need[i] = core[i] || (chordSize[i] == 0 && i <= until);
            }

            until = need.Length;
            for (int i = need.Length - 1; i >= 0; i--)
            {
                if (core[i]) until = i - fingerKeys;
                need[i] = need[i] || (chordSize[i] == 0 && i >= until);
            }

            return need;
        }

        private static bool SameGap(double a, double b)
        {
            return Math.Abs(a - b) <= SameGapRatio * Math.Max(a, b);
        }

        // 갤럽처럼 세 음 단위로 반복하는 리듬은 3키가 정박을 주손 검지에 붙들어 준다. 이때만 3키 벌점을 면제한다.
        // 고르지 않다는 이유만으로 면제하면 점음표 쌍까지 3키로 가서 정박이 세 손가락을 돈다.
        // 맨 앞과 맨 뒤 이벤트는 양쪽 간격을 볼 수 없어 벌점을 물리지 않는다.
        private static bool RepeatsEveryThreeNotes(PressEvent[] events, int i)
        {
            if (i == 0 || i + 1 >= events.Length) return true;

            double before = events[i].Time - events[i - 1].Time;
            double after = events[i + 1].Time - events[i].Time;
            if (SameGap(before, after) || i + 3 >= events.Length) return false;
            return SameGap(before, events[i + 3].Time - events[i + 2].Time);
        }

        // 셔플처럼 두 음 단위로 반복하는 리듬은 박의 1/3 위치에 있어도 4키가 손 짝을 지킨다. 3연음 벌점을 물리면 안 된다.
        private static bool RepeatsEveryTwoNotes(PressEvent[] events, int i)
        {
            if (i == 0 || i + 2 >= events.Length) return false;

            double before = events[i].Time - events[i - 1].Time;
            double after = events[i + 1].Time - events[i].Time;
            return !SameGap(before, after) && SameGap(before, events[i + 2].Time - events[i + 1].Time);
        }

        private static bool IsOnBeat(double beat)
        {
            double frac = beat - Math.Floor(beat);
            return frac <= OnBeatTolerance || frac >= 1 - OnBeatTolerance;
        }

        // 각도는 3연음을 박의 1/3, 2/3 위치로 드러낸다. 이웃까지 보는 이유는 정박 음표 자신은 위치만으로 구분되지 않아서다.
        private static bool HasTernaryNeighbor(PressEvent[] events, int i)
        {
            if (IsTernaryPosition(events[i].Beat)) return true;
            if (i > 0 && IsTernaryPosition(events[i - 1].Beat)) return true;
            return i + 1 < events.Length && IsTernaryPosition(events[i + 1].Beat);
        }

        private static bool IsTernaryPosition(double beat)
        {
            if (IsOnBeat(beat)) return false;
            double third = beat * 3;
            return Math.Abs(third - Math.Round(third)) <= OnBeatTolerance * 3;
        }

        private static int NeededTier(PressEvent[] events, int i, Tier[] tiers, double comfort)
        {
            for (int t = 0; t < tiers.Length; t++)
            {
                int k = tiers[t].K;
                if (i - k < 0 || events[i].Time - events[i - k].Time >= comfort - TimeEpsilon) return t;
            }

            return tiers.Length - 1;
        }

        private static double[,] BuildExcessTable(Tier[] tiers)
        {
            var table = new double[tiers.Length, tiers.Length];
            for (int have = 0; have < tiers.Length; have++)
            {
                for (int need = 0; need < have; need++)
                {
                    // Math.Log2는 .NET Framework 4.8에 없다.
                    table[have, need] = 0.15 * Math.Log((double)tiers[have].K / tiers[need].K, 2);
                }
            }

            return table;
        }

        private static void Backtrack(byte[] back, int finalState, StateTable states, byte[] output)
        {
            int state = finalState;
            for (int i = output.Length - 1; i >= 0; i--)
            {
                output[i] = states.Slot[state];
                state = back[i * states.Count + state];
            }
        }

        private static int CountViolations(PressEvent[] events, byte[] chosen, int slots, double hardFloor)
        {
            var occupiedUntil = new double[slots];
            for (int i = 0; i < slots; i++) occupiedUntil[i] = double.NegativeInfinity;

            int violations = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Time - occupiedUntil[chosen[i]] < hardFloor - TimeEpsilon) violations++;
                occupiedUntil[chosen[i]] = events[i].ReleaseTime >= 0 ? events[i].ReleaseTime : events[i].Time;
            }

            return violations;
        }

        // 상태별로 미리 계산해 두면 이벤트당 O(S*slots) 루프에서 tier 객체를 건드리지 않는다.
        private sealed class StateTable
        {
            public int Count;
            public byte[] Previous;
            public byte[] Slot;
            public int[] TierIndex;
            public bool[] UsesExtraKeys;
            public bool[] IsExtraKey;
            public bool[] IsOffHand;
            public double[] WeakCost;
            public double[] BeatCost;
            public double[] BurstCost;
            public double[] TernaryMismatch;
            public double[] BinaryMismatch;

            public static StateTable Build(Tier[] tiers, Layout layout)
            {
                int count = 0;
                foreach (Tier tier in tiers) count += tier.K;

                var table = new StateTable
                {
                    Count = count,
                    Previous = new byte[count],
                    Slot = new byte[count],
                    TierIndex = new int[count],
                    UsesExtraKeys = new bool[count],
                    IsExtraKey = new bool[count],
                    IsOffHand = new bool[count],
                    WeakCost = new double[count],
                    BeatCost = new double[count],
                    BurstCost = new double[count],
                    TernaryMismatch = new double[count],
                    BinaryMismatch = new double[count],
                };

                int start = 0;
                for (int t = 0; t < tiers.Length; t++)
                {
                    Tier tier = tiers[t];
                    for (int phase = 0; phase < tier.K; phase++)
                    {
                        int s = start + phase;
                        byte slot = tier.Cycle[phase];
                        bool mainStart = phase == 0;
                        bool offStart = phase == tier.OffStart;

                        table.Previous[s] = (byte)(start + (phase + tier.K - 1) % tier.K);
                        table.Slot[s] = slot;
                        table.TierIndex[s] = t;
                        table.UsesExtraKeys[s] = tier.UsesExtraKeys;
                        table.IsExtraKey[s] = layout.Slots[slot].Finger > Finger.Pinky;
                        table.IsOffHand[s] = phase >= tier.OffStart;
                        table.WeakCost[s] = WeakFingerCost[(int)layout.Slots[slot].Finger];
                        table.BeatCost[s] = mainStart ? 0 : offStart ? 0.1 : 0.3;
                        // 정박만 있는 연타는 주손 시작과 부손 시작의 합계가 정확히 같다. 주손에서 시작하도록 동률만 깬다.
                        table.BurstCost[s] = mainStart ? 0 : offStart ? BurstOffStartCost : 0.3;

                        // 곧은 타일 연타에서 3키가 뽑히면 손이 짝을 잃고, 3연음에서 4키가 뽑히면 정박이 사이클 위를 떠돈다.
                        // 정박 비용은 곧은 연타에서 오히려 3키를 편들어서 고칠 수 없다. 그래서 리듬 불일치를 따로 매긴다.
                        table.BinaryMismatch[s] = tier.K == 3 ? 0.25 : 0;
                        table.TernaryMismatch[s] = tier.K > 2 && tier.K % 3 != 0 ? 0.25 : 0;
                    }

                    start += tier.K;
                }

                return table;
            }
        }
    }
}
