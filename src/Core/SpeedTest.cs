using System;
using System.Collections.Generic;

namespace AutoFingering.Core
{
    // 두 키를 번갈아 친 시간 동안 같은 키를 다시 치기까지의 간격을 모아 2키 최대 속도를 구한다.
    // 키끼리의 간격이 아니라 키별 간격을 쓰므로 번갈아 치는 리듬이 고르지 않아도 값이 흔들리지 않는다.
    // BPM은 분당 타수다. Jipper의 CBPM과 같은 기준이고, 60으로 나누면 KPS다.
    public sealed class SpeedTest
    {
        public const double Duration = 5;

        private const double SecondsPerMinute = 60;

        // 한 손가락으로 초당 40번은 못 친다. 이보다 짧으면 키 스위치의 떨림으로 본다.
        private const double BounceGap = 0.025;
        private const int MinRepeatsPerKey = 5;
        private const double TrimRatio = 0.1;
        private const int KeyCount = 2;

        private readonly int[] keys = new int[KeyCount];
        private readonly double[] lastPress = new double[KeyCount];
        private readonly int[] repeats = new int[KeyCount];
        private readonly List<double> gaps = new List<double>();
        private int keysSeen;
        private double firstPress;
        private double latestPress;

        public int Presses { get; private set; }

        public bool Started => Presses > 0;

        public double Remaining(double now)
        {
            return Started ? Math.Max(0, firstPress + Duration - now) : Duration;
        }

        public bool IsOver(double now)
        {
            return Started && now - firstPress >= Duration;
        }

        // 처음 누른 두 키만 잰다. 세 번째 키는 실수로 건드린 것으로 보고 버린다.
        public void Press(int key, double time)
        {
            if (Started && time - firstPress >= Duration) return;

            int index = Array.IndexOf(keys, key, 0, keysSeen);
            if (index < 0)
            {
                if (keysSeen == KeyCount) return;
                index = keysSeen++;
                keys[index] = key;
            }
            else
            {
                double gap = time - lastPress[index];
                if (gap < BounceGap) return;
                gaps.Add(gap);
                repeats[index]++;
            }

            lastPress[index] = time;
            latestPress = time;
            if (Presses == 0) firstPress = time;
            Presses++;
        }

        // 측정 중 화면에 보여 줄 값이다. 최종값은 Bpm()으로 따로 구한다.
        public double LiveBpm()
        {
            double elapsed = latestPress - firstPress;
            return Presses > 1 && elapsed > 0 ? (Presses - 1) * SecondsPerMinute / elapsed : 0;
        }

        // 두 키를 모두 충분히 치지 않았으면 0이다. 위아래 10%를 버려 잠깐 멈춘 구간과 튄 입력을 뺀다.
        public double Bpm()
        {
            if (repeats[0] < MinRepeatsPerKey || repeats[1] < MinRepeatsPerKey) return 0;

            var sorted = new List<double>(gaps);
            sorted.Sort();
            int trim = (int)(sorted.Count * TrimRatio);
            double sum = 0;
            for (int i = trim; i < sorted.Count - trim; i++) sum += sorted[i];
            double meanGap = sum / (sorted.Count - trim * 2);

            // 번갈아 치면 한 키는 두 타마다 한 번 눌리므로 같은 키 간격 한 번에 두 타가 들어간다.
            return 2 * SecondsPerMinute / meanGap;
        }
    }
}
