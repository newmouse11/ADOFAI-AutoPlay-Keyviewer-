namespace AutoFingering.Core
{
    public enum Hand { Left, Right }

    public enum Finger { Index, Middle, Ring, Pinky, Thumb1, Thumb2, Palm1, Palm2 }

    public enum RollStyle { Outward, Inward, Stair, ReverseStair }

    public enum TripletSplit { MainTwoFingers, OffTwoFingers }

    public sealed class KeySlot
    {
        public int KeyCode;
        public string Label;
        public Hand Hand;
        public Finger Finger;
        public bool Enabled;
    }

    public sealed class Layout
    {
        public KeySlot[] Slots;
    }

    // UMM XmlSerializer로 저장되므로 public 필드와 기본 생성자를 유지한다.
    public sealed class RoleOverride
    {
        public int KeyCount;
        public int Slot;
        public Hand Hand;
        public Finger Finger;
        public bool Enabled = true;
    }

    public struct PressEvent
    {
        public double Time;
        public double ReleaseTime;
        public double Beat;

        // 구조체 기본값 0은 "0초에 떼는 홀드"로 읽히므로 생성자에서 탭(-1)을 기본으로 둔다.
        public PressEvent(double time, double beat, double releaseTime = -1)
        {
            Time = time;
            Beat = beat;
            ReleaseTime = releaseTime;
        }
    }

    public sealed class SolverOptions
    {
        public const int AllKeys = int.MaxValue;

        public RollStyle Style = RollStyle.Outward;
        public Hand MainHand = Hand.Right;
        public int Base = 2;
        public int MaxKeys = AllKeys;
        public double Floor = 0.130;
        public TripletSplit TripletSplit = TripletSplit.MainTwoFingers;
    }

    public sealed class Solution
    {
        public const byte NoSlot = 255;

        public byte[] Slot;
        public int FloorViolations;
    }

    public struct TileInfo
    {
        public int SeqId;
        public double EntryTime;
        public double AngleLength;
        public double ExtraBeats;
        public bool MidSpin;
        public int HoldLength;
        public int TapsNeeded;
        public bool Auto;
    }

    // 모든 배열의 인덱스는 seqID다. scrFloor 참조를 재시도 사이에 들고 있지 않기 위함이다.
    public sealed class TilePlan
    {
        public PressEvent[] Events;
        public int[] FirstEvent;
        public int[] EventCount;
        public int[] ReleaseEvent;
    }
}
