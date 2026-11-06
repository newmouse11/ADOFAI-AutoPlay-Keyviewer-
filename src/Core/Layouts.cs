using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoFingering.Core
{
    public static class Layouts
    {
        // UnityEngine.KeyCode 값. Core는 Unity를 참조하지 않으므로 숫자로 둔다.
        private const int KeySpace = 32;
        private const int KeyRightAlt = 307;
        private const int KeyLeftAlt = 308;

        private static readonly Role[] Key8 =
        {
            new Role(Hand.Left, Finger.Pinky), new Role(Hand.Left, Finger.Ring),
            new Role(Hand.Left, Finger.Middle), new Role(Hand.Left, Finger.Index),
            new Role(Hand.Right, Finger.Index), new Role(Hand.Right, Finger.Middle),
            new Role(Hand.Right, Finger.Ring), new Role(Hand.Right, Finger.Pinky),
        };

        private static readonly Role[] Key4 =
        {
            new Role(Hand.Left, Finger.Middle), new Role(Hand.Left, Finger.Index),
            new Role(Hand.Right, Finger.Index), new Role(Hand.Right, Finger.Middle),
        };

        private static readonly Role[] Key10Extra =
        {
            new Role(Hand.Left, Finger.Thumb1), new Role(Hand.Right, Finger.Thumb1),
        };

        private static readonly Role[] Key12Extra =
        {
            new Role(Hand.Left, Finger.Thumb1), new Role(Hand.Left, Finger.Thumb2),
            new Role(Hand.Right, Finger.Thumb1), new Role(Hand.Right, Finger.Thumb2),
        };

        // 뒤 네 칸을 화면 왼쪽부터 Caps, LShift, RShift, Return처럼 배치했다고 가정한다. 그래서 왼손만 Palm2가 먼저 온다.
        private static readonly Role[] Key16Extra =
        {
            new Role(Hand.Left, Finger.Palm2), new Role(Hand.Left, Finger.Palm1),
            new Role(Hand.Right, Finger.Palm1), new Role(Hand.Right, Finger.Palm2),
        };

        // 안쪽부터 바깥쪽 순서. 위치 기반 추정에서 쓴다.
        private static readonly Finger[] PositionRoles =
        {
            Finger.Index, Finger.Middle, Finger.Ring, Finger.Pinky, Finger.Palm1, Finger.Palm2,
        };

        public static Layout Build(int[] keyCodes, string[] labels = null)
        {
            if (keyCodes == null) throw new ArgumentNullException(nameof(keyCodes));

            var slots = new KeySlot[keyCodes.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new KeySlot
                {
                    KeyCode = keyCodes[i],
                    Label = labels != null && i < labels.Length
                        ? labels[i]
                        : keyCodes[i].ToString(CultureInfo.InvariantCulture),
                };
            }

            // 고정 표는 "손가락 8개 다음에 엄지"인 뷰어 배열만 가정한다. AdofaiTweaks는 등록 순서라
            // A S D F Space K O L ; ' 처럼 엄지가 중간에 끼어 표를 쓰면 손이 어긋난다.
            if (!HasThumbKeyInFingerRange(keyCodes) && AssignFromTable(slots))
            {
                return new Layout { Slots = slots };
            }

            AssignByPosition(slots);
            return new Layout { Slots = slots };
        }

        public static void ApplyOverrides(Layout layout, IEnumerable<RoleOverride> overrides)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (overrides == null) return;

            foreach (RoleOverride item in overrides)
            {
                // 저장된 설정은 키 개수별이고, 뷰어를 바꾸면 범위 밖 슬롯이 남을 수 있다.
                if (item == null || item.KeyCount != layout.Slots.Length) continue;
                if (item.Slot < 0 || item.Slot >= layout.Slots.Length) continue;

                KeySlot slot = layout.Slots[item.Slot];
                slot.Hand = item.Hand;
                slot.Finger = item.Finger;
                slot.Enabled = item.Enabled;
            }
        }

        private static bool HasThumbKeyInFingerRange(int[] keyCodes)
        {
            for (int i = 0; i < keyCodes.Length && i < Key8.Length; i++)
            {
                if (IsThumbKey(keyCodes[i])) return true;
            }

            return false;
        }

        private static bool IsThumbKey(int code)
        {
            return code == KeySpace || code == KeyLeftAlt || code == KeyRightAlt;
        }

        private static bool AssignFromTable(KeySlot[] slots)
        {
            switch (slots.Length)
            {
                case 4:
                    Assign(slots, 0, Key4);
                    return true;
                case 8:
                    Assign(slots, 0, Key8);
                    return true;
                case 10:
                    Assign(slots, 0, Key8);
                    Assign(slots, 8, Key10Extra);
                    return true;
                case 12:
                    Assign(slots, 0, Key8);
                    Assign(slots, 8, Key12Extra);
                    return true;
                case 16:
                    AssignKey16(slots);
                    return true;
                case 20:
                    AssignKey16(slots);
                    // 손가락 역할은 16개까지라 나머지 4칸은 켜지 않는다. 역할은 수정 UI의 시작값일 뿐이다.
                    for (int i = 16; i < 20; i++)
                    {
                        slots[i].Hand = i < 18 ? Hand.Left : Hand.Right;
                        slots[i].Finger = Finger.Palm2;
                        slots[i].Enabled = false;
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static void AssignKey16(KeySlot[] slots)
        {
            Assign(slots, 0, Key8);
            Assign(slots, 8, Key12Extra);
            Assign(slots, 12, Key16Extra);
        }

        private static void Assign(KeySlot[] slots, int start, Role[] roles)
        {
            for (int i = 0; i < roles.Length; i++)
            {
                slots[start + i].Hand = roles[i].Hand;
                slots[start + i].Finger = roles[i].Finger;
                slots[start + i].Enabled = true;
            }
        }

        private static void AssignByPosition(KeySlot[] slots)
        {
            var fingers = new List<KeySlot>();
            int leftThumbs = 0;
            int rightThumbs = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                KeySlot slot = slots[i];
                if (!IsThumbKey(slot.KeyCode))
                {
                    fingers.Add(slot);
                    continue;
                }

                Hand hand = NearerHand(slot.KeyCode, i, slots.Length);
                int order = hand == Hand.Left ? leftThumbs++ : rightThumbs++;
                slot.Hand = hand;
                slot.Finger = order == 0 ? Finger.Thumb1 : Finger.Thumb2;
                slot.Enabled = order < 2;
            }

            int leftCount = fingers.Count / 2;
            for (int i = 0; i < fingers.Count; i++)
            {
                bool left = i < leftCount;
                int fromInner = left ? leftCount - 1 - i : i - leftCount;
                KeySlot slot = fingers[i];
                slot.Hand = left ? Hand.Left : Hand.Right;
                slot.Enabled = fromInner < PositionRoles.Length;
                slot.Finger = PositionRoles[Math.Min(fromInner, PositionRoles.Length - 1)];
            }
        }

        private static Hand NearerHand(int code, int index, int count)
        {
            if (code == KeyLeftAlt) return Hand.Left;
            if (code == KeyRightAlt) return Hand.Right;
            return index * 2 < count ? Hand.Left : Hand.Right;
        }

        private struct Role
        {
            public readonly Hand Hand;
            public readonly Finger Finger;

            public Role(Hand hand, Finger finger)
            {
                Hand = hand;
                Finger = finger;
            }
        }
    }
}
