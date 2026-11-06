using System;

namespace AutoFingering.Game
{
    using AutoFingering.Core;

    // 슬롯별 점등 시간을 관리한다. 게임을 띄우지 않고 tests에서 돌리려고 Unity를 참조하지 않는다.
    // 시계는 Session이 Time.time으로 넣어 준다. Time.time은 일시정지에 멈추므로 멈춘 동안 탭 불이 그대로 유지된다.
    internal sealed class KeyScheduler
    {
        private const float MinTapSeconds = 0.05f;
        private const float MaxTapSeconds = 0.12f;
        private const float GapShare = 0.8f;
        private const int NoEvent = -1;

        private readonly Action<int, bool> output;
        private readonly bool[] lit;
        private readonly float[] offAt;
        private readonly int[] holdEvent;
        private readonly int[] releasedFrame;
        private readonly int[] litFrame;
        private readonly int[] pendingEvent;
        private readonly bool[] pendingHold;
        private readonly float[] pendingGap;

        private float now;
        private int frame;
        private float frameSeconds;

        public KeyScheduler(int slots, Action<int, bool> output)
        {
            this.output = output;
            lit = new bool[slots];
            offAt = new float[slots];
            holdEvent = new int[slots];
            releasedFrame = new int[slots];
            litFrame = new int[slots];
            pendingEvent = new int[slots];
            pendingHold = new bool[slots];
            pendingGap = new float[slots];
            for (int i = 0; i < slots; i++)
            {
                holdEvent[i] = NoEvent;
                releasedFrame[i] = -1;
                pendingEvent[i] = NoEvent;
            }
        }

        public bool TapsDrained
        {
            get
            {
                for (int i = 0; i < lit.Length; i++)
                {
                    if ((lit[i] && holdEvent[i] == NoEvent) || pendingEvent[i] != NoEvent) return false;
                }

                return true;
            }
        }

        public void SetClock(float time, int frameCount, float deltaTime)
        {
            now = time;
            frame = frameCount;
            frameSeconds = deltaTime;
        }

        public void Press(int slot, int evt, bool hold, float gap)
        {
            // 한 프레임에 떼기와 누르기가 겹치면 폴링 뷰어는 변화를 못 본다. 먼저 끄고 다음 프레임에 누른다.
            if (lit[slot] || releasedFrame[slot] == frame)
            {
                // 이번 프레임에 켠 키는 끄지 않는다. 켜자마자 끄면 폴링 뷰어가 이 입력을 통째로 놓친다. Tick이 다음 프레임에 끈다.
                if (lit[slot] && litFrame[slot] != frame) TurnOff(slot);
                pendingEvent[slot] = evt;
                pendingHold[slot] = hold;
                pendingGap[slot] = gap;
                return;
            }

            // 대기 중이던 이전 입력은 이번 입력이 대신한다. 남겨 두면 불이 꺼진 뒤 없는 입력이 뒤늦게 켜진다.
            pendingEvent[slot] = NoEvent;
            TurnOn(slot, evt, hold, gap);
        }

        // 체크포인트 시작은 누른 적 없는 홀드의 뗄 착지를 만난다. 이벤트 번호가 맞을 때만 끄면 몇 번을 불러도 안전하다.
        public void ReleaseHold(int slot, int evt)
        {
            if (lit[slot] && holdEvent[slot] == evt)
            {
                TurnOff(slot);
            }
            else if (pendingEvent[slot] == evt)
            {
                // 아직 켜지 못한 홀드가 이미 끝났다. 잠깐 켰다 끄는 탭으로 보여 준다.
                pendingHold[slot] = false;
            }
        }

        public void Tick()
        {
            for (int slot = 0; slot < lit.Length; slot++)
            {
                if (lit[slot])
                {
                    // 켠 프레임에 바로 끄면 화면에 안 그려지고 폴링 뷰어는 입력 자체를 놓친다. 최소 한 프레임은 유지한다.
                    // 다음 입력이 기다리면 남은 시간과 상관없이 끈다. 그래야 다음 프레임에 그 입력을 켤 수 있다.
                    bool expired = holdEvent[slot] == NoEvent && now >= offAt[slot];
                    if ((expired || pendingEvent[slot] != NoEvent) && frame != litFrame[slot]) TurnOff(slot);
                }
                else if (pendingEvent[slot] != NoEvent && releasedFrame[slot] != frame)
                {
                    int evt = pendingEvent[slot];
                    pendingEvent[slot] = NoEvent;
                    TurnOn(slot, evt, pendingHold[slot], pendingGap[slot]);
                }
            }
        }

        public void Reset()
        {
            for (int slot = 0; slot < lit.Length; slot++)
            {
                if (lit[slot]) TurnOff(slot);
                pendingEvent[slot] = NoEvent;
            }
        }

        // 이벤트별로 같은 슬롯을 다음에 쓰기까지 남은 실제 초. 다음 사용이 없으면 양의 무한대.
        public static float[] BuildGaps(PressEvent[] events, byte[] slots, int slotCount)
        {
            var gaps = new float[events.Length];
            var nextUse = new double[slotCount];
            for (int i = 0; i < slotCount; i++) nextUse[i] = double.PositiveInfinity;

            for (int i = events.Length - 1; i >= 0; i--)
            {
                byte slot = slots[i];
                if (slot == Solution.NoSlot)
                {
                    gaps[i] = float.PositiveInfinity;
                    continue;
                }

                gaps[i] = (float)(nextUse[slot] - events[i].Time);
                nextUse[slot] = events[i].Time;
            }

            return gaps;
        }

        private void TurnOn(int slot, int evt, bool hold, float gap)
        {
            lit[slot] = true;
            litFrame[slot] = frame;
            holdEvent[slot] = hold ? evt : NoEvent;
            offAt[slot] = now + TapSeconds(gap);
            output(slot, true);
        }

        private void TurnOff(int slot)
        {
            lit[slot] = false;
            holdEvent[slot] = NoEvent;
            releasedFrame[slot] = frame;
            output(slot, false);
        }

        // 다음 입력 전에 꺼진 프레임이 한 번은 있어야 같은 키의 연타가 뷰어에 구분되어 보인다.
        private float TapSeconds(float gap)
        {
            float seconds = Math.Min(Math.Max(GapShare * gap, MinTapSeconds), MaxTapSeconds);
            float room = gap - frameSeconds;
            return seconds < room ? seconds : Math.Max(room, 0f);
        }
    }
}
