using System;
using System.Collections.Generic;
using System.Threading;

namespace AutoFingering.Core
{
    // 세계 1위 지퍼의 키뷰어를 프레임 단위로 읽어 얻은 타법을 따른다. 사람은 음마다 손가락을 고르지 않고 "손 단위 묶음"으로 친다.
    // 왼손 묶음과 오른손 묶음이 번갈아 오고, 묶음 안에서는 스타일 방향으로 굴리며, 2~5동타는 한 손에 담는다.
    // 그래서 입력열을 묶음으로 나누는 문제로 푼다. 묶음의 손가락 모양은 길이로 정해지므로 같은 리듬은 같은 모양이 되어 외울 수 있다.
    //
    // 비용 값은 지퍼의 플레이(REX 7,040타, First Town 4키기반·2키기반 각 1,875타)와 맞춘 것이다.
    // 값을 바꾸면 합성 데이터만 보지 말고 실제 맵으로 재현해 동타와 아래 키를 눈으로 확인한다.
    public static class FingeringSolver
    {
        private const double ChordGap = 0.012;
        private const double ChordSpan = 0.035;

        // 게임의 entryTime은 누적 합이라 정확히 100 ms인 간격도 1e-8초쯤 흔들린다. 경계 비교에 여유가 없으면
        // 같은 리듬인데 음마다 판정이 갈려 모양이 뒤죽박죽이 된다. 시각 비교는 전부 이 여유를 둔다.
        private const double TimeEpsilon = 1e-6;
        private const double CostEpsilon = 1e-6;
        private const double PulseBinShift = 0.123;
        private const int BothHands = 2;
        private const int BigChord = 6;

        // Floor의 이 비율 아래로는 같은 손가락으로 실제로 칠 수 없다.
        private const double HardFloorRatio = 0.75;

        private const double GridTolerance = 0.002;
        private const double GridEpsilon = 0.003;
        private const int GridMisses = 4;

        private const double BelowHard = 30;
        private const double OverlapHold = BelowHard * 3;
        private const double BelowFloor = 2;

        // 동타 모양은 최소 간격 근처에서도 유지한다. 돌려 쓰면 묶음마다 모양이 바뀌어 외울 수 없다.
        private const double ChordBelowFloor = 0.2;
        private const double Restrike = 1.5;

        // 기반은 같은 손가락을 얼마나 여유 있게 다시 치는지다. 지퍼의 4키기반은 약 265 ms, 2키기반은 약 147 ms까지 내려간다.
        private const double Comfort4 = 2.4;
        private const double Comfort2 = 1.7;
        private const double FastComfort = 1.0;
        private const double Chain4 = 2.25;
        private const double Chain2 = 1.1;
        private const double Rung4 = 1.0;
        private const double Rung2 = 0.67;

        private const double HandRepeat = 0.6;
        private const double SlowRepeat4 = 0.3;
        private const double EndOffMain = 1.5;
        private const double StartOffMain = 0.6;
        private const double Boundary = 1;
        private const double MaxInnerGap = 0.1;
        private const double WindowChange = 0.6;
        private const double WindowCap = 0.4;
        private const double GraceGap = 0.03;
        private const double ParityMiss = 2;
        private const double HalfMin = 0.55;
        private const double ModeSwitch = 4;
        private const double HalfRent = 1;

        // ponytail: 상태를 묶음당 이만큼만 남긴다. 실제 맵에서는 결과가 달라지지 않았고, 극단적 맵에서 메모리와 시간을 막는다.
        private const int BeamWidth = 96;

        public static Solution Solve(
            PressEvent[] events,
            Layout layout,
            SolverOptions options,
            CancellationToken token = default(CancellationToken))
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (options == null) throw new ArgumentNullException(nameof(options));
            // 결과가 슬롯 번호를 byte로 저장하고 255는 NoSlot으로 예약돼 있다.
            if (layout.Slots.Length >= Solution.NoSlot)
            {
                throw new ArgumentException("슬롯이 너무 많음: " + layout.Slots.Length, nameof(layout));
            }

            var result = new Solution { Slot = new byte[events.Length] };
            if (events.Length == 0) return result;

            var hands = new HandSet(layout, options);
            if (hands.Total == 0)
            {
                // 활성 키가 하나도 없으면 풀 문제가 없다. 호출자는 NoSlot을 "불 안 켬"으로 처리한다.
                for (int i = 0; i < result.Slot.Length; i++) result.Slot[i] = Solution.NoSlot;
                return result;
            }

            var search = new RunSearch(events, hands, options, token);
            search.Solve(result.Slot);
            RepairHolds(events, result.Slot, hands);
            result.FloorViolations = CountViolations(events, result.Slot, layout.Slots.Length, options.Floor * HardFloorRatio);
            return result;
        }

        // 앞 홀드가 잡고 있는 손가락에 새 입력이 오면 같은 손의 빈 손가락으로 옮긴다. 묶음 탐색은 직전 두 묶음까지만 보기 때문이다.
        private static void RepairHolds(PressEvent[] events, byte[] slots, HandSet hands)
        {
            var heldUntil = new double[256];
            for (int i = 0; i < heldUntil.Length; i++) heldUntil[i] = double.NegativeInfinity;

            for (int e = 0; e < events.Length; e++)
            {
                double time = events[e].Time;
                byte slot = slots[e];
                if (heldUntil[slot] > time)
                {
                    int hand = hands.HandOf(slot);
                    int free = hands.FreeSlot(hand, heldUntil, time);
                    if (free < 0) free = hands.FreeSlot(1 - hand, heldUntil, time);
                    if (free >= 0) slot = (byte)free;
                    slots[e] = slot;
                }

                if (events[e].ReleaseTime >= 0) heldUntil[slot] = events[e].ReleaseTime;
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

        // 손 0은 주손, 1은 부손이다. 묶음 탐색은 이 좌표로만 해서 왼손·오른손 주손 설정이 정확히 거울이 된다.
        private sealed class HandSet
        {
            private static readonly Finger[] BaseOuterToInner = { Finger.Pinky, Finger.Ring, Finger.Middle, Finger.Index };
            private static readonly Finger[] ExtraOrder = { Finger.Thumb1, Finger.Thumb2, Finger.Palm1, Finger.Palm2 };

            private static readonly Finger[] ActivationOrder =
            {
                Finger.Index, Finger.Middle, Finger.Ring, Finger.Pinky,
                Finger.Thumb1, Finger.Thumb2, Finger.Palm1, Finger.Palm2,
            };

            private readonly int[][] baseKeys = new int[2][];
            private readonly int[][] extraKeys = new int[2][];
            private readonly int[][] byPriority = new int[2][];
            private readonly bool[] inward = new bool[2];
            private readonly Finger[] fingerOf;
            private readonly int[] handOf;
            private readonly Dictionary<int, int[]> shapes = new Dictionary<int, int[]>();

            public readonly int[] Capacity = new int[2];
            public readonly int[] BaseCount = new int[2];
            public readonly int Total;

            public HandSet(Layout layout, SolverOptions options)
            {
                Hand mainHand = options.MainHand;
                Hand offHand = mainHand == Hand.Left ? Hand.Right : Hand.Left;
                int[,] slotOf = ResolveSlots(layout);

                fingerOf = new Finger[layout.Slots.Length];
                handOf = new int[layout.Slots.Length];
                for (int i = 0; i < handOf.Length; i++)
                {
                    fingerOf[i] = layout.Slots[i].Finger;
                    handOf[i] = layout.Slots[i].Hand == mainHand ? 0 : 1;
                }

                var allowed = new bool[2, 8];
                int count = 0;
                foreach (Finger finger in ActivationOrder)
                {
                    // 키 수를 제한하면 힘 있는 손가락부터 남기고, 같은 손가락은 주손을 먼저 남긴다.
                    foreach (Hand hand in new[] { mainHand, offHand })
                    {
                        if (slotOf[(int)hand, (int)finger] < 0 || count >= options.MaxKeys) continue;
                        allowed[(int)hand, (int)finger] = true;
                        count++;
                    }
                }

                Total = count;
                Hand[] real = { mainHand, offHand };
                for (int h = 0; h < 2; h++)
                {
                    int r = (int)real[h];
                    baseKeys[h] = Pick(BaseOuterToInner, r, slotOf, allowed);
                    extraKeys[h] = Pick(ExtraOrder, r, slotOf, allowed);
                    byPriority[h] = Pick(ActivationOrder, r, slotOf, allowed);
                    BaseCount[h] = baseKeys[h].Length;
                    Capacity[h] = byPriority[h].Length;
                    inward[h] = options.Style == RollStyle.Inward
                        || (options.Style == RollStyle.Stair && real[h] == Hand.Left)
                        || (options.Style == RollStyle.ReverseStair && real[h] == Hand.Right);
                }
            }

            public int HandOf(int slot)
            {
                return handOf[slot];
            }

            public bool RollsInward(int hand)
            {
                return inward[hand];
            }

            public double FingerCost(int slot)
            {
                switch (fingerOf[slot])
                {
                    case Finger.Index: return 0;
                    case Finger.Middle: return 0.6;
                    case Finger.Ring: return 1;
                    case Finger.Pinky: return 1.2;
                    default: return 3;
                }
            }

            public int FreeSlot(int hand, double[] heldUntil, double time)
            {
                foreach (int slot in byPriority[hand])
                {
                    if (heldUntil[slot] <= time) return slot;
                }

                return -1;
            }

            public bool Fits(int hand, int total, bool hasChord, bool extraNeeded)
            {
                if (total <= BaseCount[hand]) return true;
                // 엄지와 손바닥 키만 있는 손은 그 키들이 곧 기본 손가락이다.
                return (hasChord || extraNeeded || BaseCount[hand] == 0) && total <= Capacity[hand];
            }

            public bool HasThumb(int hand)
            {
                return extraKeys[hand].Length > 0 && fingerOf[extraKeys[hand][0]] == Finger.Thumb1;
            }

            // 묶음의 손가락을 바깥에서 안쪽 순서로 돌려준다. 탐색은 항상 이 순서로 비용을 보고, 스타일은 마지막에 적용한다.
            public int[] Shape(int hand, int total, bool chordRun)
            {
                int key = hand * 1000 + total * 2 + (chordRun ? 1 : 0);
                int[] shape;
                if (!shapes.TryGetValue(key, out shape))
                {
                    shape = BuildShape(hand, total, chordRun);
                    shapes[key] = shape;
                }

                return shape;
            }

            private int[] BuildShape(int hand, int total, bool chordRun)
            {
                int[] keys = baseKeys[hand];
                int[] extras = extraKeys[hand];
                int baseCount = keys.Length;
                if (total <= baseCount)
                {
                    var inner = new int[total];
                    // 동타가 섞인 네 손가락 묶음은 새끼 대신 엄지가 돕는다: [ds][Lf]
                    if (total == 4 && chordRun && HasThumb(hand))
                    {
                        inner[0] = keys[baseCount - 3];
                        inner[1] = keys[baseCount - 2];
                        inner[2] = extras[0];
                        inner[3] = keys[baseCount - 1];
                        return inner;
                    }

                    Array.Copy(keys, baseCount - total, inner, 0, total);
                    return inner;
                }

                // 네 손가락을 넘으면 엄지, 손바닥 순서로 넓힌다. 엄지는 검지 바로 바깥, 손바닥은 새끼 바깥이다.
                int extraCount = Math.Min(total - baseCount, extras.Length);
                var palms = new List<int>();
                var thumbs = new List<int>();
                for (int i = 0; i < extraCount; i++)
                {
                    bool thumb = fingerOf[extras[i]] == Finger.Thumb1 || fingerOf[extras[i]] == Finger.Thumb2;
                    (thumb ? thumbs : palms).Add(extras[i]);
                }

                var shape = new List<int>(total);
                for (int i = palms.Count - 1; i >= 0; i--) shape.Add(palms[i]);
                for (int i = 0; i < baseCount - 1; i++) shape.Add(keys[i]);
                for (int i = thumbs.Count - 1; i >= 0; i--) shape.Add(thumbs[i]);
                if (baseCount > 0) shape.Add(keys[baseCount - 1]);
                return shape.ToArray();
            }

            private static int[] Pick(Finger[] order, int realHand, int[,] slotOf, bool[,] allowed)
            {
                var picked = new List<int>(order.Length);
                foreach (Finger finger in order)
                {
                    if (allowed[realHand, (int)finger]) picked.Add(slotOf[realHand, (int)finger]);
                }

                return picked.ToArray();
            }

            // 역할 수정 중에는 같은 역할이 겹칠 수 있다. 앞 슬롯만 쓴다.
            private static int[,] ResolveSlots(Layout layout)
            {
                var slotOf = new int[2, 8];
                for (int hand = 0; hand < 2; hand++)
                {
                    for (int finger = 0; finger < 8; finger++) slotOf[hand, finger] = -1;
                }

                for (int i = 0; i < layout.Slots.Length; i++)
                {
                    KeySlot slot = layout.Slots[i];
                    if (!slot.Enabled || slotOf[(int)slot.Hand, (int)slot.Finger] >= 0) continue;
                    slotOf[(int)slot.Hand, (int)slot.Finger] = i;
                }

                return slotOf;
            }
        }

        // 한 묶음이 어떤 손가락을 언제까지 쓰는지. 큰 동타는 양손 항목이 함께 들어 있어 Owner로 가른다.
        private sealed class RunView
        {
            public int Count;
            public readonly int[] Slot = new int[16];
            public readonly int[] Owner = new int[16];
            public readonly double[] When = new double[16];
            public readonly double[] Busy = new double[16];
            public readonly bool[] InChord = new bool[16];

            public void Add(int slot, int owner, double when, double busy, bool inChord)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Slot[i] != slot) continue;
                    Owner[i] = owner;
                    When[i] = when;
                    Busy[i] = busy;
                    InChord[i] = inChord;
                    return;
                }

                if (Count == Slot.Length) return;
                Slot[Count] = slot;
                Owner[Count] = owner;
                When[Count] = when;
                Busy[Count] = busy;
                InChord[Count] = inChord;
                Count++;
            }

            public bool HasOwner(int owner)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Owner[i] == owner) return true;
                }

                return false;
            }
        }

        private struct Candidate
        {
            public int Length;
            public int Hand;
            public double Own;
            public double MissWhole;
            public double MissHalf;
            public RunView View;
        }

        // 슬롯은 동시에 누르는 입력 묶음이다. 슬롯 열을 손 묶음으로 나누는 동적 계획법이다.
        // 상태는 (이번 손, 이번 슬롯 수, 앞 손, 앞 슬롯 수, 박동 창 모드)뿐이라 이벤트 수에 선형이다.
        private sealed class RunSearch
        {
            private const int HandStates = 4;
            private const int LengthStates = 9;
            private const int PairStates = HandStates * LengthStates;
            private const int StateCount = PairStates * PairStates * 2;
            private const int RingSize = 32;

            private readonly PressEvent[] events;
            private readonly HandSet hands;
            private readonly CancellationToken token;
            private readonly double floor;
            private readonly double hard;
            private readonly double comfort;
            private readonly double comfortFast;

            private readonly int n;
            private readonly int[] slotStart;
            private readonly int[] size;
            private readonly double[] onset;
            private readonly double[] busyUntil;
            private readonly bool[] isBig;
            private readonly bool[] chainStart;
            private readonly bool[] extraNeeded;
            private readonly double[] origin;
            private readonly double[] pulse;
            private readonly bool base4;
            private readonly int maxRun;

            private readonly RunView[,,] views;
            private readonly Candidate[] candidates;
            private readonly int[] winner;

            public RunSearch(PressEvent[] events, HandSet hands, SolverOptions options, CancellationToken token)
            {
                this.events = events;
                this.hands = hands;
                this.token = token;
                floor = options.Floor;
                hard = floor * HardFloorRatio;
                base4 = options.Base >= 4;
                comfort = floor * (base4 ? Comfort4 : Comfort2);
                comfortFast = Math.Min(comfort, floor * FastComfort);

                var starts = new List<int>();
                var sizes = new List<int>();
                FindSlots(starts, sizes);
                n = starts.Count;
                slotStart = new int[n + 1];
                size = new int[n];
                onset = new double[n];
                busyUntil = new double[n];
                for (int k = 0; k < n; k++)
                {
                    slotStart[k] = starts[k];
                    size[k] = sizes[k];
                    onset[k] = events[starts[k]].Time;
                    double busy = onset[k];
                    for (int e = starts[k]; e < starts[k] + sizes[k]; e++) busy = Math.Max(busy, events[e].ReleaseTime);
                    busyUntil[k] = busy;
                }

                slotStart[n] = events.Length;

                double chainGap = floor * (base4 ? Chain4 : Chain2);
                isBig = new bool[n];
                for (int k = 0; k < n; k++)
                {
                    isBig[k] = size[k] >= BigChord || !(hands.Fits(0, size[k], size[k] > 1, false) || hands.Fits(1, size[k], size[k] > 1, false));
                }

                chainStart = new bool[n];
                if (n > 0) chainStart[0] = true;
                for (int k = 1; k < n; k++)
                {
                    chainStart[k] = onset[k] - onset[k - 1] >= chainGap - TimeEpsilon || isBig[k] || isBig[k - 1];
                }

                // 여덟 손가락을 다 돌려도 실제로 못 치는 연타에서만 엄지와 손바닥을 연타에 쓴다.
                int baseKeys = hands.BaseCount[0] + hands.BaseCount[1];
                extraNeeded = new bool[n];
                for (int k = 0; k < n; k++)
                {
                    extraNeeded[k] = baseKeys > 0 && k + baseKeys < n && onset[k + baseKeys] - onset[k] < hard - TimeEpsilon;
                }

                origin = new double[n];
                pulse = new double[n];
                FindGridSections(floor * (base4 ? Rung4 : Rung2), chainGap);

                maxRun = Math.Min(LengthStates - 1, Math.Max(4, Math.Max(hands.Capacity[0], hands.Capacity[1])));
                views = new RunView[RingSize, 3, LengthStates];
                candidates = new Candidate[2 * maxRun + 1];
                winner = new int[n + 1];
            }

            public void Solve(byte[] output)
            {
                var layerCost = new double[LengthStates][];
                var layerPrev = new ushort[LengthStates][];
                var layerActive = new int[LengthStates][];
                var layerCount = new int[LengthStates];
                for (int i = 0; i < LengthStates; i++)
                {
                    layerCost[i] = new double[StateCount];
                    layerPrev[i] = new ushort[StateCount];
                    layerActive[i] = new int[StateCount];
                    for (int s = 0; s < StateCount; s++) layerCost[i][s] = double.PositiveInfinity;
                }

                var backIds = new ushort[n + 1][];
                var backPrev = new ushort[n + 1][];
                int start = Encode(-1, 0, -1, 0, 0);
                layerCost[0][start] = 0;
                layerActive[0][0] = start;
                layerCount[0] = 1;

                var keep = new int[StateCount];
                for (int i = 0; i < n; i++)
                {
                    if ((i & 0xFFF) == 0) token.ThrowIfCancellationRequested();

                    int layer = i % LengthStates;
                    double[] cost = layerCost[layer];
                    int[] active = layerActive[layer];
                    int count = Trim(cost, active, layerCount[layer], keep);

                    backIds[i] = new ushort[count];
                    backPrev[i] = new ushort[count];
                    for (int a = 0; a < count; a++)
                    {
                        backIds[i][a] = (ushort)active[a];
                        backPrev[i][a] = layerPrev[layer][active[a]];
                    }

                    int candidateCount = BuildCandidates(i);
                    bool halfAllowed = pulse[i] / 2 >= floor * HalfMin - TimeEpsilon;
                    for (int a = 0; a < count; a++)
                    {
                        int state = active[a];
                        Expand(i, state, cost[state], candidateCount, halfAllowed, layerCost, layerPrev, layerActive, layerCount);
                    }

                    for (int a = 0; a < layerCount[layer]; a++) cost[active[a]] = double.PositiveInfinity;
                    layerCount[layer] = 0;
                }

                int finalLayer = n % LengthStates;
                int best = -1;
                double bestCost = double.PositiveInfinity;
                for (int s = 0; s < StateCount; s++)
                {
                    if (layerCost[finalLayer][s] < bestCost - CostEpsilon)
                    {
                        bestCost = layerCost[finalLayer][s];
                        best = s;
                    }
                }

                Backtrack(best, layerPrev[finalLayer][best], backIds, backPrev, output);
            }

            private void Backtrack(int finalState, ushort finalPrev, ushort[][] backIds, ushort[][] backPrev, byte[] output)
            {
                int state = finalState;
                int prev = finalPrev;
                int i = n;
                while (i > 0)
                {
                    int hand = HandOfState(state);
                    int length = LengthOfState(state);
                    int runStart = i - length;
                    Assign(runStart, length, hand, output);

                    i = runStart;
                    state = prev;
                    if (i == 0) break;
                    prev = -1;
                    ushort[] ids = backIds[i];
                    for (int a = 0; a < ids.Length; a++)
                    {
                        if (ids[a] == state)
                        {
                            prev = backPrev[i][a];
                            break;
                        }
                    }
                }
            }

            private void Assign(int start, int length, int hand, byte[] output)
            {
                int first = slotStart[start];
                int end = slotStart[start + length];
                if (hand == BothHands)
                {
                    int[] fingers = BigChordFingers(start);
                    for (int e = first; e < end; e++) output[e] = (byte)fingers[(e - first) % fingers.Length];
                    return;
                }

                int total = end - first;
                bool hasChord = RunHasChord(start, length);
                int[] shape = hands.Shape(hand, total, length > 1 && hasChord);
                bool grace = length == 2 && !hasChord && onset[start + 1] - onset[start] < GraceGap - TimeEpsilon;
                for (int k = 0; k < total; k++)
                {
                    int index = hands.RollsInward(hand) ? k : total - 1 - k;
                    output[first + k] = (byte)shape[index % shape.Length];
                }

                // 거의 동타인 두 음은 스타일과 상관없이 검지가 박을 잡고 옆 손가락이 뒤따른다.
                if (grace && hands.FingerCost(output[first]) > hands.FingerCost(output[first + 1]))
                {
                    byte swap = output[first];
                    output[first] = output[first + 1];
                    output[first + 1] = swap;
                }
            }

            private void Expand(
                int i,
                int state,
                double baseCost,
                int candidateCount,
                bool halfAllowed,
                double[][] layerCost,
                ushort[][] layerPrev,
                int[][] layerActive,
                int[] layerCount)
            {
                int h1 = HandOfState(state);
                int a = LengthOfState(state);
                int h2 = PrevHandOfState(state);
                int b = PrevLengthOfState(state);
                int previousMode = state & 1;

                for (int c = 0; c < candidateCount; c++)
                {
                    int length = candidates[c].Length;
                    int hand = candidates[c].Hand;
                    double shared = baseCost + candidates[c].Own;
                    if (h1 >= 0 && i + length < n)
                    {
                        double width = onset[i + length] - onset[i];
                        double widthBefore = onset[i] - onset[i - a];
                        // 다중탭은 시각이 같은 입력이 이어져 폭이 둘 다 0일 수 있다. 0으로 나누면 NaN이 되어 경로가 통째로 사라진다.
                        double wider = Math.Max(width, widthBefore);
                        if (wider > 0 && width < WindowCap - TimeEpsilon && widthBefore < WindowCap - TimeEpsilon)
                        {
                            shared += WindowChange * Math.Abs(width - widthBefore) / wider;
                        }
                    }

                    if (hand == h1 && hand != BothHands)
                    {
                        shared += !chainStart[i] ? HandRepeat : base4 ? SlowRepeat4 : 0;
                    }

                    int target = (i + length) % LengthStates;
                    for (int mode = 0; mode < (halfAllowed ? 2 : 1); mode++)
                    {
                        double total = shared
                            + (mode == 0 ? candidates[c].MissWhole : candidates[c].MissHalf)
                            + (mode != previousMode ? ModeSwitch : 0);
                        if (h1 >= 0)
                        {
                            double soft = mode == 1 ? comfortFast : comfort;
                            if (hand == BothHands)
                            {
                                total += RestrikeForHand(i, candidates[c].View, 0, a, h1, b, h2, soft);
                                total += RestrikeForHand(i, candidates[c].View, 1, a, h1, b, h2, soft);
                            }
                            else
                            {
                                total += RestrikeForHand(i, candidates[c].View, hand, a, h1, b, h2, soft);
                            }
                        }

                        int next = Encode(hand, length, h1, a, mode);
                        // 같은 리듬이면 비용이 같아야 하는데 시각 오차로 미세하게 갈린다. 작은 차이로는 먼저 찾은 경로를 뒤집지 않는다.
                        if (total < layerCost[target][next] - CostEpsilon)
                        {
                            if (double.IsPositiveInfinity(layerCost[target][next]))
                            {
                                layerActive[target][layerCount[target]++] = next;
                            }

                            layerCost[target][next] = total;
                            layerPrev[target][next] = (ushort)state;
                        }
                    }
                }
            }

            private double RestrikeForHand(int i, RunView current, int want, int a, int h1, int b, int h2, double soft)
            {
                RunView older = ViewForHand(i - a, a, h1, want);
                if (older == null && h2 >= 0) older = ViewForHand(i - a - b, b, h2, want);
                if (older == null) return 0;

                double cost = 0;
                for (int x = 0; x < current.Count; x++)
                {
                    if (current.Owner[x] != want) continue;
                    for (int y = 0; y < older.Count; y++)
                    {
                        if (older.Slot[y] != current.Slot[x]) continue;
                        double gap = current.When[x] - older.Busy[y];
                        if (gap < -TimeEpsilon) cost += OverlapHold;
                        else if (gap < hard - TimeEpsilon) cost += BelowHard;
                        // 번갈아 가는 동타와 큰 동타만 봐 준다. 같은 손으로 이어 치는 [jk] [jk]는 번갈아 칠 수 있는데도 재타하는 것이다.
                        else if (gap < floor - TimeEpsilon) cost += current.InChord[x] && h1 != want ? ChordBelowFloor : BelowFloor;
                        else if (gap < soft - TimeEpsilon) cost += Restrike * (soft - gap) / soft;
                        break;
                    }
                }

                return cost;
            }

            private RunView ViewForHand(int start, int length, int hand, int want)
            {
                if (hand < 0) return null;
                RunView view = views[start & (RingSize - 1), hand, length];
                if (hand == BothHands) return view != null && view.HasOwner(want) ? view : null;
                return hand == want ? view : null;
            }

            private int BuildCandidates(int i)
            {
                int ring = i & (RingSize - 1);
                if (isBig[i])
                {
                    RunView view = ResetView(ring, BothHands, 1);
                    int[] take = SplitBigChord(size[i]);
                    for (int h = 0; h < 2; h++)
                    {
                        if (take[h] == 0) continue;
                        int[] shape = hands.Shape(h, take[h], false);
                        foreach (int slot in shape) view.Add(slot, h, onset[i], busyUntil[i], true);
                    }

                    candidates[0] = new Candidate { Length = 1, Hand = BothHands, View = view };
                    return 1;
                }

                // 번갈아 칠 때 부딪히는 앞뒤 간격을 둘 다 본다. 앞만 보면 쉼 직전의 동타가 빠른 간격을 놓친다.
                bool alternatePlayable = (i + 2 >= n || onset[i + 2] - onset[i] >= hard - TimeEpsilon)
                    && (i == 0 || i + 1 >= n || onset[i + 1] - onset[i - 1] >= hard - TimeEpsilon);
                int count = 0;
                for (int hand = 0; hand < 2; hand++)
                {
                    int total = 0;
                    bool hasChord = false;
                    for (int length = 1; length <= maxRun; length++)
                    {
                        int last = i + length - 1;
                        if (last >= n || isBig[last]) break;
                        if (length > 1 && onset[last] - onset[last - 1] > MaxInnerGap + TimeEpsilon) break;

                        total += size[last];
                        hasChord |= size[last] > 1;
                        if (!hands.Fits(hand, total, hasChord, extraNeeded[i])) break;
                        // 엄지 없이 동타를 네 손가락 묶음에 담으면 바깥 손가락으로 돌아가 모양이 흔들린다(;l jk). 번갈아 쳐도 되는 빠르기면 번갈아 간다.
                        if (length > 1 && hasChord && total >= 4 && !hands.HasThumb(hand) && alternatePlayable) break;

                        candidates[count++] = BuildRun(i, length, hand, total, hasChord, ring);
                    }
                }

                return count;
            }

            private Candidate BuildRun(int i, int length, int hand, int total, bool hasChord, int ring)
            {
                int[] shape = hands.Shape(hand, total, length > 1 && hasChord);
                bool grace = length == 2 && !hasChord && onset[i + 1] - onset[i] < GraceGap - TimeEpsilon;
                RunView view = ResetView(ring, hand, length);
                int picked = 0;
                double own = 0;
                for (int s = i; s < i + length; s++)
                {
                    for (int c = 0; c < size[s]; c++)
                    {
                        // Assign이 거의 동타인 두 음을 검지부터 누르므로 비용도 그 순서로 본다. 다르면 실제보다 간격이 길게 계산돼 못 치는 재타를 놓친다.
                        int slot = shape[(grace ? total - 1 - picked++ : picked++) % shape.Length];
                        own += hands.FingerCost(slot);
                        view.Add(slot, hand, onset[s], busyUntil[s], size[s] > 1);
                    }
                }

                if (length > 1)
                {
                    double widest = 0;
                    for (int k = i + 1; k < i + length; k++) widest = Math.Max(widest, onset[k] - onset[k - 1]);
                    if (i > 0 && onset[i] - onset[i - 1] < widest * 0.8 - TimeEpsilon)
                    {
                        own += Boundary * (widest - (onset[i] - onset[i - 1])) / widest;
                    }

                    if (i + length < n && onset[i + length] - onset[i + length - 1] < widest * 0.8 - TimeEpsilon)
                    {
                        own += Boundary * (widest - (onset[i + length] - onset[i + length - 1])) / widest;
                    }
                }

                double width = i + length < n ? onset[i + length] - onset[i] : pulse[i];
                double missWhole = 0;
                double missHalf = Math.Min(width, pulse[i]) / floor * HalfRent;
                for (int k = i; k < i + length; k++)
                {
                    // 박동 창이 번갈아 양손의 몫이다. 창에 맞지 않는 슬롯마다 비용을 문다.
                    if ((hand == 0) != (Side(onset[k], origin[i], pulse[i]) == 0)) missWhole += ParityMiss;
                    if ((hand == 0) != (Side(onset[k], origin[i], pulse[i] / 2) == 0)) missHalf += ParityMiss;
                }

                // 단타를 손당 2음씩 끊는 건 2키기반의 타법이다. 같은 곡을 지퍼는 44 ms 4키기반에서 8손가락으로, 37 ms 2키기반에서 d f k j로 쳤다.
                if (base4 && length == 2 && !hasChord) missHalf += BelowHard;

                if (hand != 0)
                {
                    int last = i + length - 1;
                    bool lastEndsChain = last + 1 >= n || chainStart[last + 1];
                    // 4키기반의 외톨이 음은 부손이어도 시작과 끝 비용을 물리지 않는다.
                    if (!(base4 && chainStart[i] && lastEndsChain && length == 1))
                    {
                        if (lastEndsChain) own += EndOffMain;
                        if (chainStart[i]) own += StartOffMain;
                    }
                }

                return new Candidate { Length = length, Hand = hand, Own = own, MissWhole = missWhole, MissHalf = missHalf, View = view };
            }

            private RunView ResetView(int ring, int hand, int length)
            {
                RunView view = views[ring, hand, length];
                if (view == null)
                {
                    view = new RunView();
                    views[ring, hand, length] = view;
                }

                view.Count = 0;
                return view;
            }

            private int[] BigChordFingers(int slot)
            {
                int[] take = SplitBigChord(size[slot]);
                var fingers = new List<int>();
                for (int h = 0; h < 2; h++)
                {
                    if (take[h] > 0) fingers.AddRange(hands.Shape(h, take[h], false));
                }

                return fingers.ToArray();
            }

            // 한 손에 안 들어가는 동타는 번갈아 나눈다. 주손이 먼저 받으므로 9동타의 엄지는 주손 쪽이다.
            private int[] SplitBigChord(int chord)
            {
                var take = new int[2];
                for (int k = 0; k < chord; k++)
                {
                    int h = k % 2;
                    if (take[h] >= hands.Capacity[h]) h = 1 - h;
                    if (take[h] < hands.Capacity[h]) take[h]++;
                }

                return take;
            }

            private bool RunHasChord(int start, int length)
            {
                for (int k = start; k < start + length; k++)
                {
                    if (size[k] > 1) return true;
                }

                return false;
            }

            private static int Side(double time, double origin, double unit)
            {
                long cell = (long)Math.Floor((time - origin + GridEpsilon) / unit);
                return (int)(((cell % 2) + 2) % 2);
            }

            private static int Encode(int hand, int length, int previousHand, int previousLength, int mode)
            {
                int current = (hand + 1) * LengthStates + length;
                int previous = (previousHand + 1) * LengthStates + previousLength;
                return (current * PairStates + previous) * 2 + mode;
            }

            private static int HandOfState(int state)
            {
                return state / 2 / PairStates / LengthStates - 1;
            }

            private static int LengthOfState(int state)
            {
                return state / 2 / PairStates % LengthStates;
            }

            private static int PrevHandOfState(int state)
            {
                return state / 2 % PairStates / LengthStates - 1;
            }

            private static int PrevLengthOfState(int state)
            {
                return state / 2 % PairStates % LengthStates;
            }

            // 넘치면 비용이 낮은 상태만 남긴다. 나머지는 삽입 순서를 유지해 같은 입력이 항상 같은 결과를 내게 한다.
            private static int Trim(double[] cost, int[] active, int count, int[] keep)
            {
                if (count <= BeamWidth) return count;

                Array.Copy(active, keep, count);
                var values = new double[count];
                for (int i = 0; i < count; i++) values[i] = cost[active[i]];
                Array.Sort(values);
                double limit = values[BeamWidth - 1];

                int kept = 0;
                for (int i = 0; i < count; i++)
                {
                    int state = keep[i];
                    if (cost[state] <= limit && kept < BeamWidth) active[kept++] = state;
                    else cost[state] = double.PositiveInfinity;
                }

                return kept;
            }

            private void FindSlots(List<int> starts, List<int> sizes)
            {
                int i = 0;
                int count = events.Length;
                while (i < count)
                {
                    int j = i;
                    while (j + 1 < count && events[j + 1].Time - events[j].Time <= ChordGap + TimeEpsilon) j++;
                    if (j > i && events[j].Time - events[i].Time <= ChordSpan + TimeEpsilon)
                    {
                        starts.Add(i);
                        sizes.Add(j - i + 1);
                    }
                    else
                    {
                        for (int k = i; k <= j; k++)
                        {
                            starts.Add(k);
                            sizes.Add(1);
                        }
                    }

                    i = j + 1;
                }
            }

            private static double RungOf(double gap, double low)
            {
                while (gap < low - TimeEpsilon) gap *= 2;
                while (gap >= 2 * low - TimeEpsilon) gap /= 2;
                return gap;
            }

            // 간격을 2배수 사다리로 접어 시간 가중 최빈값을 낸다. 시간이 가장 많이 쓰인 박동이다.
            private double PulseOf(int from, int to, double low, double fallback)
            {
                var weight = new SortedDictionary<int, double>();
                var total = new Dictionary<int, double>();
                for (int k = from + 1; k < to; k++)
                {
                    double gap = onset[k] - onset[k - 1];
                    if (gap <= ChordGap + TimeEpsilon || gap >= 1 - TimeEpsilon) continue;
                    double rung = RungOf(gap, low);
                    // ponytail: 2.5 ms 칸. 경계를 0.5에서 옮겨 160 BPM처럼 흔한 박자가 경계에 걸려 시각 오차로 칸이 갈리는 일을 막는다. 군집화는 실제 문제가 생기면 한다.
                    int key = (int)Math.Round(rung * 400 + PulseBinShift);
                    double w;
                    weight.TryGetValue(key, out w);
                    weight[key] = w + gap;
                    double sum;
                    total.TryGetValue(key, out sum);
                    total[key] = sum + gap * rung;
                }

                if (weight.Count == 0) return fallback;

                int best = 0;
                double bestWeight = double.NegativeInfinity;
                foreach (KeyValuePair<int, double> pair in weight)
                {
                    if (pair.Value > bestWeight)
                    {
                        bestWeight = pair.Value;
                        best = pair.Key;
                    }
                }

                return total[best] / weight[best];
            }

            private static bool OnGrid(double x, double beat)
            {
                return OnCell(x, beat / 8) || OnCell(x, beat / 12);
            }

            private static bool OnCell(double x, double cell)
            {
                double q = x / cell;
                return Math.Abs(q - Math.Round(q)) * cell <= GridTolerance;
            }

            // 같은 격자에 맞는 슬롯끼리 구간으로 묶고 구간마다 (원점, 박동)을 정한다.
            private void FindGridSections(double low, double chainGap)
            {
                if (n == 0) return;

                double whole = PulseOf(0, n, low, 2 * low);
                int i = 0;
                while (i < n)
                {
                    int limit = Math.Min(n, i + 9);
                    int hits = 0;
                    for (int k = i + 1; k < limit; k++)
                    {
                        if (OnGrid(onset[k] - onset[i], whole)) hits++;
                    }

                    double here = hits >= Math.Min(n - i - 1, 8) * 0.6 ? whole : PulseOf(i, Math.Min(n, i + 48), low, whole);

                    int j = i + 1;
                    int misses = 0;
                    int firstMiss = -1;
                    while (j < n)
                    {
                        if (OnGrid(onset[j] - onset[i], here))
                        {
                            misses = 0;
                            firstMiss = -1;
                        }
                        else
                        {
                            misses++;
                            if (firstMiss < 0) firstMiss = j;
                            if (misses >= GridMisses) break;
                        }

                        j++;
                    }

                    int end = misses >= GridMisses ? firstMiss : n;
                    double start = onset[i] + BusiestCell(i, end, here) / 24.0 * here;

                    // 패리티: 체인 끝이 주손 창에 더 많이 떨어지는 쪽이다.
                    int votesEven = 0;
                    int votesOdd = 0;
                    for (int k = i; k < end; k++)
                    {
                        double after = k + 1 < n ? onset[k + 1] - onset[k] : 9;
                        double before = k > 0 ? onset[k] - onset[k - 1] : 9;
                        if (after >= chainGap - TimeEpsilon && before < chainGap - TimeEpsilon)
                        {
                            if (Side(onset[k], start, here) == 0) votesEven++;
                            else votesOdd++;
                        }
                    }

                    if (votesOdd > votesEven) start += here;

                    for (int k = i; k < end; k++)
                    {
                        origin[k] = start;
                        pulse[k] = here;
                    }

                    i = end;
                }
            }

            private int BusiestCell(int from, int to, double beat)
            {
                var counts = new int[24];
                for (int k = from; k < to; k++)
                {
                    double offset = onset[k] - onset[from];
                    double phase = offset - Math.Floor(offset / beat) * beat;
                    int cell = ((int)Math.Round(phase / beat * 24)) % 24;
                    counts[cell]++;
                }

                int best = 0;
                for (int c = 1; c < 24; c++)
                {
                    if (counts[c] > counts[best]) best = c;
                }

                return best;
            }
        }
    }
}
