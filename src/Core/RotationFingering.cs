using System;
using System.Collections.Generic;

namespace AutoFingering.Core
{
    // 8키기반: 리듬과 상관없이 손가락을 정해진 순서로 계속 돈다.
    // 사용자가 직접 친 영상에서 입력의 96%가 이 순서를 따랐다. 같은 모양이 계속되어 외우기 쉽고, 손가락마다 쉬는 시간이 고르다.
    public static partial class FingeringSolver
    {
        // 동타가 셋 이상이면 지금 손의 기본 손가락 대신 같은 손의 아래 키를 이만큼 쓴다(사람은 3동타를 손가락 하나 + 아래 키 둘로 쳤다).
        private const int RotationLowerInChord = 2;

        // 사람은 빠른 구간에서 노트와 상관없이 약 25 ms마다 한 번씩 계속 굴렸다.
        // 61 ms 간격에 평균 1.44번, 30 ms 간격에 0.19번을 더 눌렀다. 정수로 반올림하면 이 비율이 안 나와 남는 몫을 다음 간격으로 넘긴다.
        // 노트 간격이 이보다 넓으면 굴리기를 멈춘다(쉬는 동안 키를 켜면 외울 때 노트와 헷갈린다).
        private const double RollGap = 0.025;
        private const double RollMaxGap = 0.12;

        private static Fill[] Rotate(PressEvent[] events, HandSet hands, SolverOptions options, byte[] output)
        {
            var cycle = new List<int>();
            var handAt = new List<int>();
            foreach (int hand in RotationHandOrder(hands, options.Style))
            {
                foreach (int slot in hands.RollKeys(hand))
                {
                    cycle.Add(slot);
                    handAt.Add(hand);
                }
            }

            // 엄지와 손바닥 키만 있는 배치는 그 키들로 돈다.
            if (cycle.Count == 0)
            {
                for (int hand = 0; hand < 2; hand++)
                {
                    foreach (int slot in hands.ExtraKeys(hand))
                    {
                        cycle.Add(slot);
                        handAt.Add(hand);
                    }
                }
            }

            var state = new RotationState(cycle, handAt, hands);
            var fills = new List<Fill>();
            double rollDebt = 0;
            int i = 0;
            while (i < events.Length)
            {
                int j = i;
                while (j + 1 < events.Length && events[j + 1].Time - events[j].Time <= ChordGap + TimeEpsilon) j++;
                bool chord = j > i && events[j].Time - events[i].Time <= ChordSpan + TimeEpsilon;
                int end = chord ? j : i;

                List<int> keys = state.Take(end - i + 1, events[i].Time);
                for (int e = i; e <= end; e++)
                {
                    int slot = keys[(e - i) % keys.Count];
                    output[e] = (byte)slot;
                    if (events[e].ReleaseTime >= 0) state.Hold(slot, events[e].ReleaseTime);
                }

                if (end + 1 < events.Length) AddFills(events, end, state, fills, ref rollDebt);
                i = end + 1;
            }

            return fills.ToArray();
        }

        // 다음 노트까지 굴리는 간격으로 몇 번 더 누를 수 있는지 보고, 그만큼 순환을 이어서 채운다.
        // debt는 앞 간격들에서 다 못 누른 몫이다. 굴리기를 멈추면 버린다.
        private static void AddFills(PressEvent[] events, int last, RotationState state, List<Fill> fills, ref double debt)
        {
            double gap = events[last + 1].Time - events[last].Time;
            if (gap > RollMaxGap + TimeEpsilon)
            {
                debt = 0;
                return;
            }

            debt += Math.Max(0, gap / RollGap - 1);
            int extra = (int)Math.Floor(debt + 0.5);
            debt -= extra;
            for (int k = 1; k <= extra; k++)
            {
                double delay = gap * k / (extra + 1);
                int slot = state.Take(1, events[last].Time + delay)[0];
                fills.Add(new Fill { After = last, Delay = (float)delay, Slot = (byte)slot });
            }
        }

        // 안밖·밖안은 주손부터, 계단은 왼손부터, 역계단은 오른손부터 돈다.
        private static int[] RotationHandOrder(HandSet hands, RollStyle style)
        {
            int first = 0;
            if (style == RollStyle.Stair) first = hands.RealHand(0) == Hand.Left ? 0 : 1;
            if (style == RollStyle.ReverseStair) first = hands.RealHand(0) == Hand.Right ? 0 : 1;
            return new[] { first, 1 - first };
        }

        private sealed class RotationState
        {
            private readonly List<int> cycle;
            private readonly List<int> handAt;
            private readonly HandSet hands;
            private readonly Dictionary<int, double> heldUntil = new Dictionary<int, double>();
            private int next;

            public RotationState(List<int> cycle, List<int> handAt, HandSet hands)
            {
                this.cycle = cycle;
                this.handAt = handAt;
                this.hands = hands;
            }

            public void Hold(int slot, double until)
            {
                heldUntil[slot] = until;
            }

            public List<int> Take(int size, double time)
            {
                SkipHeld(time);
                if (size == 1)
                {
                    int slot = cycle[next];
                    Advance(1);
                    return new List<int> { slot };
                }

                List<int> inHand = TakeInHand(size, time);
                return inHand ?? TakeBig(size, time);
            }

            // 2~4동타는 한 손 안에서 친다. 손이 바뀌는 자리에 걸리면 반대 손으로 넘어가지 않고 그 손의 아래 키를 더한다.
            private List<int> TakeInHand(int size, double time)
            {
                int hand = handAt[next];
                int[] extras = FreeExtras(hand, time);
                int lower = size >= 3 ? Math.Min(RotationLowerInChord, extras.Length) : 0;
                int top = size - lower;
                int left = RemainingInHand();
                if (top > left)
                {
                    int shortfall = top - left;
                    if (lower + shortfall <= extras.Length)
                    {
                        lower += shortfall;
                        top = left;
                    }
                    else
                    {
                        // 이 손으로는 못 담는다. 반대 손의 처음부터 다시 담아 본다.
                        Advance(left);
                        hand = handAt[next];
                        extras = FreeExtras(hand, time);
                        lower = size >= 3 ? Math.Min(RotationLowerInChord, extras.Length) : 0;
                        top = size - lower;
                        if (top > RemainingInHand()) return null;
                    }
                }

                var keys = new List<int>(size);
                for (int k = 0; k < top; k++) keys.Add(cycle[(next + k) % cycle.Count]);
                for (int k = 0; k < lower; k++) keys.Add(extras[k]);
                Advance(Math.Max(top, 1));
                return keys;
            }

            // 다섯 이상은 지금 자리부터 순서대로 양손에 담고, 모자라면 아래 키를 더한다.
            private List<int> TakeBig(int size, double time)
            {
                var keys = new List<int>(size);
                int count = Math.Min(size, cycle.Count);
                for (int k = 0; k < count; k++) keys.Add(cycle[(next + k) % cycle.Count]);
                for (int hand = 0; hand < 2 && keys.Count < size; hand++)
                {
                    foreach (int slot in FreeExtras(hand, time))
                    {
                        if (keys.Count >= size) break;
                        keys.Add(slot);
                    }
                }

                Advance(count);
                return keys;
            }

            private int RemainingInHand()
            {
                int hand = handAt[next];
                int count = 0;
                while (count < cycle.Count && handAt[(next + count) % cycle.Count] == hand) count++;
                return count;
            }

            private int[] FreeExtras(int hand, double time)
            {
                var free = new List<int>();
                foreach (int slot in hands.ExtraKeys(hand))
                {
                    if (!IsHeld(slot, time)) free.Add(slot);
                }

                return free.ToArray();
            }

            // 홀드로 눌린 손가락은 건너뛴다. 전부 눌려 있으면 그대로 둔다.
            private void SkipHeld(double time)
            {
                for (int k = 0; k < cycle.Count && IsHeld(cycle[next], time); k++) Advance(1);
            }

            private bool IsHeld(int slot, double time)
            {
                double until;
                return heldUntil.TryGetValue(slot, out until) && until > time + TimeEpsilon;
            }

            private void Advance(int steps)
            {
                next = (next + steps) % cycle.Count;
            }
        }
    }
}
