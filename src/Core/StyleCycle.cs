using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoFingering.Core
{
    public sealed class Tier
    {
        public int K;
        public byte[] Cycle;
        public int OffStart;

        // 엄지나 손바닥 키가 들어간 tier. 네 손가락으로 칠 수 있는 구간에서는 쓰지 않는다.
        public bool UsesExtraKeys;
    }

    public static class StyleCycle
    {
        private const int FingerCount = 8;

        private static readonly Finger[] ActivationOrder =
        {
            Finger.Index, Finger.Middle, Finger.Ring, Finger.Pinky,
            Finger.Thumb1, Finger.Thumb2, Finger.Palm1, Finger.Palm2,
        };

        private static readonly Finger[] InnerToOuter =
        {
            Finger.Thumb2, Finger.Thumb1, Finger.Index, Finger.Middle,
            Finger.Ring, Finger.Pinky, Finger.Palm1, Finger.Palm2,
        };

        private static readonly int[] TierSizes = { 2, 3, 4, 6, 8, 10, 12, 16 };

        public static Tier[] BuildTiers(Layout layout, SolverOptions options)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (options == null) throw new ArgumentNullException(nameof(options));
            // 사이클과 결과가 슬롯 번호를 byte로 저장하고 255는 NoSlot으로 예약돼 있다.
            if (layout.Slots.Length >= Solution.NoSlot)
            {
                throw new ArgumentException("슬롯이 너무 많음: " + layout.Slots.Length, nameof(layout));
            }

            int[,] slotOf = ResolveSlots(layout);
            var tiers = new SortedDictionary<int, Tier>();
            var used = new bool[2, FingerCount];
            int total = 0;

            foreach (Finger finger in ActivationOrder)
            {
                for (int hand = 0; hand < 2; hand++)
                {
                    if (slotOf[hand, (int)finger] < 0) continue;
                    used[hand, (int)finger] = true;
                    total++;
                }

                // 홀수 합계는 플레이어가 키 하나를 끈 경우에만 나온다. 표에 없다고 버리면 남은 키를 전혀 못 쓰게 된다.
                if (total >= 2 && total <= options.MaxKeys && (total % 2 == 1 || Array.IndexOf(TierSizes, total) >= 0))
                {
                    tiers[total] = BuildTier(used, slotOf, options);
                }
            }

            AddTripletTier(tiers, slotOf, options);
            if (tiers.Count == 0) AddSingleKeyTier(tiers, slotOf, options);

            return tiers.Values.ToArray();
        }

        // 역할 수정 중에는 같은 역할이 겹칠 수 있다. 앞 슬롯만 쓰면 tier 크기가 표대로 유지된다.
        private static int[,] ResolveSlots(Layout layout)
        {
            var slotOf = new int[2, FingerCount];
            for (int hand = 0; hand < 2; hand++)
            {
                for (int finger = 0; finger < FingerCount; finger++) slotOf[hand, finger] = -1;
            }

            for (int i = 0; i < layout.Slots.Length; i++)
            {
                KeySlot slot = layout.Slots[i];
                if (!slot.Enabled || slotOf[(int)slot.Hand, (int)slot.Finger] >= 0) continue;
                slotOf[(int)slot.Hand, (int)slot.Finger] = i;
            }

            return slotOf;
        }

        // 3키는 누적 활성화로 나올 수 없는 특수 tier다. 2키와 4키 사이에 손 하나만 한 손가락을 더 쓴다.
        private static void AddTripletTier(SortedDictionary<int, Tier> tiers, int[,] slotOf, SolverOptions options)
        {
            if (options.MaxKeys < 3) return;

            Hand twoFingerHand = options.TripletSplit == TripletSplit.MainTwoFingers
                ? options.MainHand
                : Other(options.MainHand);
            int two = (int)twoFingerHand;
            int one = (int)Other(twoFingerHand);

            if (slotOf[two, (int)Finger.Index] < 0
                || slotOf[two, (int)Finger.Middle] < 0
                || slotOf[one, (int)Finger.Index] < 0)
            {
                return;
            }

            var used = new bool[2, FingerCount];
            used[two, (int)Finger.Index] = true;
            used[two, (int)Finger.Middle] = true;
            used[one, (int)Finger.Index] = true;
            tiers[3] = BuildTier(used, slotOf, options);
        }

        // 활성 슬롯이 하나뿐이면 2키 tier가 생기지 않는다. 그래도 키는 켜져야 하므로 그 하나로 돌린다.
        private static void AddSingleKeyTier(SortedDictionary<int, Tier> tiers, int[,] slotOf, SolverOptions options)
        {
            int[] hands = { (int)options.MainHand, (int)Other(options.MainHand) };
            foreach (Finger finger in ActivationOrder)
            {
                foreach (int hand in hands)
                {
                    if (slotOf[hand, (int)finger] < 0) continue;
                    var used = new bool[2, FingerCount];
                    used[hand, (int)finger] = true;
                    tiers[1] = BuildTier(used, slotOf, options);
                    return;
                }
            }
        }

        private static Tier BuildTier(bool[,] used, int[,] slotOf, SolverOptions options)
        {
            List<byte> main = BuildGroup(options.MainHand, used, slotOf, options.Style);
            List<byte> off = BuildGroup(Other(options.MainHand), used, slotOf, options.Style);
            int offStart = main.Count;
            main.AddRange(off);
            return new Tier { K = main.Count, Cycle = main.ToArray(), OffStart = offStart, UsesExtraKeys = HasExtraKey(used) };
        }

        private static bool HasExtraKey(bool[,] used)
        {
            for (int hand = 0; hand < 2; hand++)
            {
                if (used[hand, (int)Finger.Thumb1] || used[hand, (int)Finger.Thumb2]
                    || used[hand, (int)Finger.Palm1] || used[hand, (int)Finger.Palm2])
                {
                    return true;
                }
            }

            return false;
        }

        private static List<byte> BuildGroup(Hand hand, bool[,] used, int[,] slotOf, RollStyle style)
        {
            var group = new List<byte>();
            foreach (Finger finger in InnerToOuter)
            {
                if (used[(int)hand, (int)finger]) group.Add((byte)slotOf[(int)hand, (int)finger]);
            }

            if (RunsOuterToInner(hand, style)) group.Reverse();
            return group;
        }

        private static bool RunsOuterToInner(Hand hand, RollStyle style)
        {
            switch (style)
            {
                case RollStyle.Inward:
                    return true;
                case RollStyle.Stair:
                    return hand == Hand.Left;
                case RollStyle.ReverseStair:
                    return hand == Hand.Right;
                default:
                    return false;
            }
        }

        private static Hand Other(Hand hand)
        {
            return hand == Hand.Left ? Hand.Right : Hand.Left;
        }
    }
}
