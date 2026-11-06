using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AutoFingering.Core;
using AutoFingering.Game;

namespace AutoFingering.Tests
{
    internal static class Program
    {
        private static readonly int[] Jipper16 =
            { 9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 4117, 46, 301, 304, 303, 13 };

        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            Run("hand run shapes", TestRunShapes);
            Run("chords", TestChords);
            Run("options", TestOptions);
            Run("holds", TestHolds);
            Run("classifier", TestClassifier);
            Run("layouts", TestLayouts);
            Run("determinism and performance", TestDeterminismAndPerformance);
            Run("base and cancel", TestBaseAndCancel);
            Run("key scheduler", TestKeyScheduler);
            Run("speed test", TestSpeedTest);

            Console.WriteLine($"checks={_checks} failures={_failures}");
            return _failures == 0 ? 0 : 1;
        }

        private static void TestSpeedTest()
        {
            const int KeyA = 106;
            const int KeyB = 107;

            // 게임은 프레임마다 키를 읽으므로 입력 시각이 프레임 경계로 밀린 상태로 들어온다.
            static double Frame(double time) => Math.Ceiling(time * 60) / 60;

            static SpeedTest Alternate(double bpm, Func<double, double> at)
            {
                var test = new SpeedTest();
                double gap = 60 / bpm;
                for (int i = 0; i * gap < SpeedTest.Duration + 1; i++)
                {
                    test.Press(i % 2 == 0 ? KeyA : KeyB, at(100 + i * gap));
                }

                return test;
            }

            Check(Math.Abs(Alternate(961, t => t).Bpm() - 961) < 0.5, "exact alternation");

            foreach (double bpm in new[] { 480.0, 678.0, 822.0, 961.0, 1080.0, 1440.0 })
            {
                double measured = Alternate(bpm, Frame).Bpm();
                Check(Math.Abs(measured - bpm) / bpm < 0.02, $"60 fps at {bpm} BPM measured {measured}");
            }

            // 번갈아 치는 리듬이 어긋나도 같은 키 간격이 같으면 값은 같아야 한다.
            var uneven = new SpeedTest();
            for (int i = 0; i < 30; i++)
            {
                uneven.Press(KeyA, 100 + i * 0.2);
                uneven.Press(KeyB, 100.05 + i * 0.2);
            }

            Check(Math.Abs(uneven.Bpm() - 600) < 0.5, $"uneven rhythm measured {uneven.Bpm()}");

            var paused = new SpeedTest();
            for (int i = 0; i < 60; i++) paused.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i / 15.0 + (i >= 30 ? 0.6 : 0));
            Check(Math.Abs(paused.Bpm() - 900) / 900 < 0.05, $"a short stop measured {paused.Bpm()}");

            var bouncy = new SpeedTest();
            for (int i = 0; i < 40; i++)
            {
                int key = i % 2 == 0 ? KeyA : KeyB;
                bouncy.Press(key, 100 + i / 12.0);
                bouncy.Press(key, 100.01 + i / 12.0);
            }

            Check(bouncy.Presses == 40, $"bounced presses counted: {bouncy.Presses}");
            Check(Math.Abs(bouncy.Bpm() - 720) < 0.5, $"bounce measured {bouncy.Bpm()}");

            var thirdKey = new SpeedTest();
            for (int i = 0; i < 40; i++)
            {
                thirdKey.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i / 12.0);
                if (i > 0) thirdKey.Press(999, 100.02 + i / 12.0);
            }

            Check(Math.Abs(thirdKey.Bpm() - 720) < 0.5, $"third key measured {thirdKey.Bpm()}");

            // 처음 건드린 키가 측정 키로 잡혀도 몇 번 안 눌리면 틀린 값을 내지 않고 다시 하게 한다.
            var strayKey = new SpeedTest();
            strayKey.Press(999, 100);
            for (int i = 0; i < 40; i++) strayKey.Press(i % 2 == 0 ? KeyA : KeyB, 100.1 + i / 12.0);
            Check(strayKey.Bpm() == 0, $"a stray first key measured {strayKey.Bpm()}");

            var oneKey = new SpeedTest();
            for (int i = 0; i < 40; i++) oneKey.Press(KeyA, 100 + i / 8.0);
            Check(oneKey.Bpm() == 0, "one key has no 2-key speed");

            var tooFew = new SpeedTest();
            for (int i = 0; i < 6; i++) tooFew.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i * 0.1);
            Check(tooFew.Bpm() == 0, "too few presses");

            var idle = new SpeedTest();
            Check(!idle.Started && !idle.IsOver(1000), "the clock waits for the first press");
            Check(idle.Remaining(1000) == SpeedTest.Duration, "full time left before the first press");
            Check(idle.Bpm() == 0 && idle.LiveBpm() == 0, "no value before any press");

            var timed = new SpeedTest();
            timed.Press(KeyA, 100);
            Check(timed.Started && !timed.IsOver(104.9) && timed.IsOver(105), "ends five seconds after the first press");
            Check(Math.Abs(timed.Remaining(102) - 3) < 1e-9, "remaining time");
            Check(timed.Remaining(200) == 0, "remaining time stops at zero");
            timed.Press(KeyB, 105);
            Check(timed.Presses == 1, "a press after the end is ignored");
        }

        private static void Run(string name, Action test)
        {
            int before = _failures;
            var watch = Stopwatch.StartNew();
            try
            {
                test();
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine($"  exception: {ex}");
            }

            Console.WriteLine($"{(_failures == before ? "PASS" : "FAIL")} {name} ({watch.ElapsedMilliseconds} ms)");
        }

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (condition) return;

            _failures++;
            // 실패가 많아도 원인 파악에 필요한 앞부분만 보이면 된다.
            if (_failures <= 40) Console.WriteLine($"  fail: {message}");
        }

        private static Layout Layout8()
        {
            return Layouts.Build(
                new[] { 97, 115, 100, 102, 106, 107, 108, 59 },
                new[] { "a", "s", "d", "f", "j", "k", "l", ";" });
        }

        private static Layout Layout4()
        {
            return Layouts.Build(new[] { 100, 102, 106, 107 }, new[] { "d", "f", "j", "k" });
        }

        private static Layout Layout16()
        {
            string[] labels = Enumerable.Range(0, 16).Select(i => ((char)('A' + i)).ToString()).ToArray();
            return Layouts.Build(Jipper16, labels);
        }

        private static PressEvent[] Stream(int count, double gap, double notesPerBeat)
        {
            var events = new PressEvent[count];
            for (int i = 0; i < count; i++) events[i] = new PressEvent(i * gap, i / notesPerBeat);
            return events;
        }

        private static string Labels(Layout layout, byte[] slots)
        {
            return string.Concat(slots.Select(s => layout.Slots[s].Label));
        }

        private static int Distinct(byte[] slots, int start, int end)
        {
            return slots.Skip(start).Take(end - start).Distinct().Count();
        }

        private static int RecountViolations(PressEvent[] events, byte[] slots, double floor)
        {
            var last = new Dictionary<byte, double>();
            int violations = 0;
            for (int i = 0; i < events.Length; i++)
            {
                // 솔버와 같은 여유를 둔다. 누적 시간 오차로 정확히 floor인 간격이 위반으로 세어지면 안 된다.
                if (last.TryGetValue(slots[i], out double previous) && events[i].Time - previous < floor - 1e-9) violations++;
                last[slots[i]] = events[i].ReleaseTime >= 0 ? events[i].ReleaseTime : events[i].Time;
            }

            return violations;
        }

        private const double Jipper = 0.107;

        private static Layout Layout10()
        {
            return Layouts.Build(
                new[] { 97, 115, 100, 102, 106, 107, 108, 59, 32, 44 },
                new[] { "a", "s", "d", "f", "j", "k", "l", ";", "L", "R" });
        }

        private static SolverOptions JipperOptions(int baseKeys = 4, RollStyle style = RollStyle.Inward, Hand main = Hand.Left)
        {
            return new SolverOptions { Style = style, MainHand = main, Base = baseKeys, Floor = Jipper };
        }

        // 앞뒤 경계를 뺀 가운데 부분이 주어진 순서의 반복인지 본다.
        private static bool Cycles(string actual, string cycle)
        {
            string middle = actual.Substring(actual.Length / 4, actual.Length / 2);
            string doubled = cycle + cycle;
            for (int i = 0; i + cycle.Length <= middle.Length; i++)
            {
                if (doubled.IndexOf(middle.Substring(i, cycle.Length), StringComparison.Ordinal) < 0) return false;
            }

            return true;
        }

        // 지퍼의 키뷰어에서 확인한 빠르기별 모양이다. BPM은 분당 타수다.
        private static void TestRunShapes()
        {
            Layout layout = Layout8();

            void Expect(string name, double bpm, SolverOptions options, string cycle)
            {
                string keys = Labels(layout, FingeringSolver.Solve(Stream(96, 60.0 / bpm, 4), layout, options).Slot);
                Check(Cycles(keys, cycle), $"{name}: wanted {cycle}, got {keys.Substring(24, 32)}");
            }

            // 4키기반
            Expect("4-key 340 BPM", 340, JipperOptions(), "fj");
            Expect("4-key 960 BPM", 960, JipperOptions(), "dfkj");
            Expect("4-key 1360 BPM", 1360, JipperOptions(), "asdf;lkj");

            // 2키기반: 느리면 한 손가락으로 친다.
            Expect("2-key 408 BPM", 408, JipperOptions(2), "f");
            Expect("2-key 816 BPM", 816, JipperOptions(2), "fj");
            Expect("2-key 1632 BPM", 1632, JipperOptions(2), "dfkj");

            // 스타일은 손 묶음 안의 방향만 바꾼다.
            Expect("Outward", 960, JipperOptions(4, RollStyle.Outward), "fdjk");
            Expect("Stair", 960, JipperOptions(4, RollStyle.Stair), "dfjk");
            Expect("ReverseStair", 960, JipperOptions(4, RollStyle.ReverseStair), "fdkj");
            Expect("Outward 8 keys", 1360, JipperOptions(4, RollStyle.Outward), "fdsajkl;");

            // 주손을 바꾸면 거울이다.
            Expect("main right", 960, JipperOptions(4, RollStyle.Inward, Hand.Right), "kjdf");

            // 게임의 시각은 누적 오차로 1e-8초쯤 흔들린다. 경계값인 100 ms 연타도 모양이 일정해야 외울 수 있다.
            var noise = new Random(1);
            PressEvent[] exact = Stream(96, 0.1, 4);
            PressEvent[] noisy = Stream(96, 0.1, 4);
            for (int i = 0; i < noisy.Length; i++) noisy[i].Time += (noise.NextDouble() - 0.5) * 8e-8;
            string clean = Labels(layout, FingeringSolver.Solve(exact, layout, JipperOptions()).Slot);
            string jitter = Labels(layout, FingeringSolver.Solve(noisy, layout, JipperOptions()).Slot);
            Check(clean == jitter, $"timing noise changes the shape: {clean.Substring(24, 24)} / {jitter.Substring(24, 24)}");
            Check(Cycles(jitter, "dfkj"), $"100 ms stream: {jitter.Substring(24, 24)}");
        }

        private static PressEvent[] ChordStream(double period, params int[] pattern)
        {
            var events = new List<PressEvent>();
            for (int i = 0; i < 96; i++)
            {
                int size = pattern[i % pattern.Length];
                for (int k = 0; k < size; k++) events.Add(new PressEvent(i * period + k * 0.0014, i));
            }

            return events.ToArray();
        }

        private static List<string> ChordGroups(PressEvent[] events, byte[] slots, Layout layout)
        {
            var groups = new List<string>();
            int i = 0;
            while (i < events.Length)
            {
                int j = i;
                while (j + 1 < events.Length && events[j + 1].Time - events[j].Time <= 0.012) j++;
                var names = new List<string>();
                for (int k = i; k <= j; k++) names.Add(layout.Slots[slots[k]].Label);
                names.Sort(StringComparer.Ordinal);
                groups.Add(string.Concat(names));
                i = j + 1;
            }

            return groups;
        }

        private static void TestChords()
        {
            Layout layout = Layout10();

            // 2~3동타는 한 손에 담고 손을 번갈아 간다: [dfs] [jkl] [df] [jk]
            PressEvent[] mixed = ChordStream(0.0625, 3, 3, 2, 2);
            List<string> groups = ChordGroups(mixed, FingeringSolver.Solve(mixed, layout, JipperOptions()).Slot, layout);
            string shape = string.Join(" ", groups.Skip(16).Take(4));
            Check(shape == "dfs jkl df jk", $"small chords stay in one hand: {shape}");
            Check(string.Join(" ", groups.Skip(40).Take(4)) == shape, "the same rhythm keeps the same shape");

            foreach (double floor in new[] { Jipper, 0.150 })
            {
                var options = new SolverOptions { Style = RollStyle.Inward, MainHand = Hand.Left, Base = 4, Floor = floor };
                foreach (string g in ChordGroups(mixed, FingeringSolver.Solve(mixed, layout, options).Slot, layout))
                {
                    bool left = g.Any(c => "asdfL".IndexOf(c) >= 0);
                    bool right = g.Any(c => "jkl;R".IndexOf(c) >= 0);
                    Check(!(left && right), $"chord split across hands at floor {floor}: {g}");
                }
            }

            // 엄지가 없는 8키는 2동타를 바깥 손가락으로 돌리지 않고 손을 번갈아 간다: [df] [jk]
            PressEvent[] twos = ChordStream(0.0625, 2);
            Layout eightKeys = Layout8();
            List<string> twoGroups = ChordGroups(twos, FingeringSolver.Solve(twos, eightKeys, JipperOptions()).Slot, eightKeys);
            string twoShape = string.Join(" ", twoGroups.Skip(16).Take(4));
            Check(twoGroups.All(g => g == "df" || g == "jk"), $"2-note chords on 8 keys keep the inner fingers: {twoShape}");

            // 큰 동타는 양손을 쓰고 매번 같은 손가락을 잡는다. 125 ms 반복이 최소 간격보다 짧아도 돌려 쓰지 않는다(REX 8동타).
            foreach (double floor in new[] { Jipper, 0.130, 0.150 })
            {
                PressEvent[] eight = ChordStream(0.125, 8);
                var options = new SolverOptions { Style = RollStyle.Inward, MainHand = Hand.Left, Base = 4, Floor = floor };
                foreach (string g in ChordGroups(eight, FingeringSolver.Solve(eight, layout, options).Slot, layout))
                {
                    Check(g == ";adfjkls", $"8-note chord at floor {floor}: {g}");
                }
            }

            PressEvent[] six = ChordStream(0.25, 6);
            Check(ChordGroups(six, FingeringSolver.Solve(six, layout, JipperOptions()).Slot, layout).All(g => g == "dfjkls"), "6-note chord is three plus three");

            PressEvent[] nine = ChordStream(0.25, 9);
            Check(ChordGroups(nine, FingeringSolver.Solve(nine, layout, JipperOptions()).Slot, layout).All(g => g == ";Ladfjkls"), "9-note chord adds the main thumb");

            // 엄지는 필요할 때만 쓴다.
            Layout wide = Layout16();
            byte[] plain = FingeringSolver.Solve(Stream(400, 0.0625, 4), wide, JipperOptions()).Slot;
            Check(plain.All(s => wide.Slots[s].Finger <= Finger.Pinky), "a playable stream stays on the fingers");

            // 여덟 손가락으로 못 치는 속도에서만 아래 키가 연타에 나온다.
            PressEvent[] extreme = Stream(800, 0.006, 4);
            Solution fast = FingeringSolver.Solve(extreme, wide, JipperOptions());
            Check(fast.Slot.Any(s => wide.Slots[s].Finger > Finger.Pinky), "an impossible stream uses the extra keys");
            Solution eightOnly = FingeringSolver.Solve(extreme, Layout8(), JipperOptions());
            Check(fast.FloorViolations < eightOnly.FloorViolations, $"extra keys help: {fast.FloorViolations} < {eightOnly.FloorViolations}");

            // 다중탭과 초고속 연타가 겹치면 시각이 같은 입력이 묶이지 않고 이어진다. 경로가 사라져 계산이 실패하면 안 된다.
            var burst = new List<PressEvent>();
            for (int i = 0; i < 4; i++) burst.Add(new PressEvent(1, 0));
            for (int i = 1; i <= 6; i++) burst.Add(new PressEvent(1 + i * 0.01, i));
            Check(FingeringSolver.Solve(burst.ToArray(), Layouts.Build(new[] { 97, 98 }), new SolverOptions()).Slot.Length == burst.Count, "equal times inside a fast chain still solve");

            // 시각이 같은 입력은 동타이고, 키가 모자라도 죽지 않는다.
            var same = new[] { new PressEvent(1, 0), new PressEvent(1, 0), new PressEvent(1, 0) };
            Check(FingeringSolver.Solve(same, layout, JipperOptions()).Slot.Distinct().Count() == 3, "equal times are a chord on three fingers");
            Layout two = Layouts.Build(new[] { 102, 106 }, new[] { "f", "j" });
            Check(FingeringSolver.Solve(same, two, JipperOptions()).Slot.All(s => s < 2), "a chord larger than the layout still solves");
        }

        private static void TestOptions()
        {
            Layout layout = Layout10();
            PressEvent[] mixed = MixedStream(3000, 3);
            Solution inward = FingeringSolver.Solve(mixed, layout, JipperOptions());

            // 묶음 나누기는 스타일과 상관없다. 쓰는 손이 스타일마다 달라지면 안 된다.
            foreach (RollStyle style in new[] { RollStyle.Outward, RollStyle.Stair, RollStyle.ReverseStair })
            {
                byte[] other = FingeringSolver.Solve(mixed, layout, JipperOptions(4, style)).Slot;
                int differ = Enumerable.Range(0, mixed.Length).Count(i => layout.Slots[other[i]].Hand != layout.Slots[inward.Slot[i]].Hand);
                Check(differ == 0, $"{style} changes hands on {differ} presses");
            }

            // 주손을 바꾸면 정확히 거울이다.
            byte[] mirrored = FingeringSolver.Solve(mixed, layout, JipperOptions(4, RollStyle.Inward, Hand.Right)).Slot;
            int[] mirror = { 7, 6, 5, 4, 3, 2, 1, 0, 9, 8 };
            int mirrorDiffer = Enumerable.Range(0, mixed.Length).Count(i => mirror[inward.Slot[i]] != mirrored[i]);
            Check(mirrorDiffer == 0, $"main hand mirror differs on {mirrorDiffer} presses");

            // 키 수 제한: 힘 있는 손가락부터 남는다.
            Layout wide = Layout16();
            var expected = new Dictionary<int, string>
            {
                [2] = "E P",
                [4] = "2 E P =",
                [6] = "1 2 E P = Back",
                [8] = "Tab 1 2 E P = Back \\",
            };
            string[] labels = "Tab 1 2 E P = Back \\ Space C VK21 . Caps LShift RShift Return".Split(' ');
            Layout named = Layouts.Build(Jipper16, labels);
            foreach (KeyValuePair<int, string> pair in expected)
            {
                var options = new SolverOptions { MaxKeys = pair.Key, Floor = Jipper };
                byte[] slots = FingeringSolver.Solve(MixedStream(2000, 5), named, options).Slot;
                string used = string.Join(" ", slots.Distinct().OrderBy(s => s).Select(s => named.Slots[s].Label));
                Check(used == pair.Value, $"max {pair.Key}: {used}");
            }

            // 엄지와 손바닥 키만 있어도 연타가 한 키에 몰리지 않는다.
            Layout thumbs = Layouts.Build(new[] { 308, 32, 307 });
            byte[] thumbSlots = FingeringSolver.Solve(Stream(32, 0.05, 4), thumbs, new SolverOptions()).Slot;
            Check(thumbSlots.Distinct().Count() >= 2, $"thumb-only layout uses {thumbSlots.Distinct().Count()} keys");

            // 위반 집계는 실제 재타 간격과 같다.
            Check(inward.FloorViolations == RecountViolations(mixed, inward.Slot, Jipper * 0.75), "violations match a recount");
        }

        private static void TestHolds()
        {
            Layout layout = Layout8();
            var random = new Random(9);
            var events = new List<PressEvent>();
            double time = 0;
            for (int i = 0; i < 4000; i++)
            {
                double release = random.Next(6) == 0 ? time + 0.2 + random.NextDouble() * 1.5 : -1;
                events.Add(new PressEvent(time, i, release));
                time += new[] { 0.03, 0.06, 0.12, 0.25 }[random.Next(4)];
            }

            PressEvent[] all = events.ToArray();
            byte[] slots = FingeringSolver.Solve(all, layout, JipperOptions()).Slot;
            var heldUntil = new double[layout.Slots.Length];
            int overlaps = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (heldUntil[slots[i]] > all[i].Time)
                {
                    // 모든 키가 잡혀 있으면 어쩔 수 없다. 빈 키가 있었다면 오류다.
                    bool anyFree = heldUntil.Any(h => h <= all[i].Time);
                    if (anyFree) overlaps++;
                }

                if (all[i].ReleaseTime >= 0) heldUntil[slots[i]] = all[i].ReleaseTime;
            }

            Check(overlaps == 0, $"a press landed on a held finger {overlaps} times");

            // 홀드를 잡은 동안 다른 손가락이 연타를 이어 간다.
            var held = new List<PressEvent> { new PressEvent(0, 0, 3) };
            for (int i = 1; i <= 20; i++) held.Add(new PressEvent(i * 0.1, i));
            byte[] heldSlots = FingeringSolver.Solve(held.ToArray(), layout, JipperOptions()).Slot;
            Check(heldSlots.Skip(1).All(s => s != heldSlots[0]), "notes during a hold avoid the held finger");
        }

        private static TileInfo Tile(
            double entry, double angle = Math.PI, bool midSpin = false, int hold = -1, int taps = 1, bool auto = false, double extra = 0)
        {
            return new TileInfo
            {
                EntryTime = entry,
                AngleLength = angle,
                ExtraBeats = extra,
                MidSpin = midSpin,
                HoldLength = hold,
                TapsNeeded = taps,
                Auto = auto,
            };
        }

        private static TileInfo Mid(double entry)
        {
            return Tile(entry, 0, midSpin: true);
        }

        // 기대값: 타일별 입력 수와 떼는 이벤트. 시각은 개별 케이스에서 본다.
        private static TilePlan Classify(string name, TileInfo[] tiles, int[] counts, int[] releases, double pitch = 1, bool hitOnce = false)
        {
            TilePlan plan = TileClassifier.Build(tiles, pitch, hitOnce);
            Check(plan.EventCount.SequenceEqual(counts),
                $"{name}: counts [{string.Join(",", plan.EventCount)}] expected [{string.Join(",", counts)}]");
            Check(plan.ReleaseEvent.SequenceEqual(releases),
                $"{name}: releases [{string.Join(",", plan.ReleaseEvent)}] expected [{string.Join(",", releases)}]");

            int next = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (plan.EventCount[i] > 0) Check(plan.FirstEvent[i] == next, $"{name}: FirstEvent[{i}]");
                next += plan.EventCount[i];
            }

            Check(next == plan.Events.Length, $"{name}: event total");
            return plan;
        }

        private static void TestClassifier()
        {
            // 평범한 타일: 0번은 착지가 없고, 시간은 pitch로 나누고, 180도가 1박이다.
            TilePlan plain = Classify("plain",
                new[] { Tile(0), Tile(1), Tile(2), Tile(3), Tile(4, Math.PI / 2), Tile(4.5) },
                new[] { 0, 1, 1, 1, 1, 1 }, new[] { -1, -1, -1, -1, -1, -1 }, pitch: 2);
            Check(plain.Events.Select(e => e.Time).SequenceEqual(new[] { 0.5, 1, 1.5, 2, 2.25 }), "plain times use pitch");
            Check(Near(plain.Events.Select(e => e.Beat), 0, 1, 2, 3, 3.5), "plain beats");
            Check(plain.Events.All(e => e.ReleaseTime < 0), "plain are taps");

            // 일시정지는 게임의 entryBeat처럼 다음 타일의 Beat에 더해진다.
            TilePlan paused = Classify("pause beat",
                new[] { Tile(0), Tile(1, extra: 3), Tile(5), Tile(6) },
                new[] { 0, 1, 1, 1 }, new[] { -1, -1, -1, -1 });
            Check(Near(paused.Events.Select(e => e.Beat), 0, 4, 5), "extra beats add to beat");

            // 미드스핀: 체인 첫 착지에서 한 번만 누른다.
            TilePlan mid = Classify("midspin",
                new[] { Tile(0), Tile(1), Mid(2), Tile(2), Tile(3) },
                new[] { 0, 1, 1, 0, 1 }, new[] { -1, -1, -1, -1, -1 });
            Check(Near(mid.Events.Select(e => e.Beat), 0, 1, 2), "midspin adds no beat");
            Classify("midspin chain",
                new[] { Tile(0), Tile(1), Mid(2), Mid(2), Tile(2), Tile(3) },
                new[] { 0, 1, 1, 0, 0, 1 }, new[] { -1, -1, -1, -1, -1, -1 });

            // 미드스핀 + 자동: 다음 타일의 auto가 입력 필요 여부를 정한다.
            TileInfo autoMid = Mid(2);
            autoMid.Auto = true;
            Classify("auto midspin then manual",
                new[] { Tile(0), Tile(1), autoMid, Tile(2), Tile(3) },
                new[] { 0, 1, 0, 0, 1 }, new[] { -1, -1, -1, -1, -1 });
            Classify("manual midspin then auto",
                new[] { Tile(0), Tile(1), Mid(2), Tile(2, auto: true), Tile(3) },
                new[] { 0, 1, 1, 0, 1 }, new[] { -1, -1, -1, -1, -1 });

            // 홀드 뒤 미드스핀: 떼기가 두 착지를 처리하므로 새 입력이 없다.
            TilePlan midAfterHold = Classify("midspin after hold",
                new[] { Tile(0), Tile(1, hold: 0), Mid(2), Tile(2), Tile(3) },
                new[] { 0, 1, 0, 0, 1 }, new[] { -1, -1, 0, -1, -1 });
            Check(midAfterHold.Events[0].ReleaseTime == 2, "hold before midspin releases at midspin landing");
            Classify("two midspins after hold",
                new[] { Tile(0), Tile(1, hold: 0), Mid(2), Mid(2), Tile(2), Tile(3) },
                new[] { 0, 1, 0, 0, 0, 1 }, new[] { -1, -1, 0, -1, -1, -1 });

            // 홀드, 미드스핀, 홀드: 체인 끝에서 새 입력이 필요하다.
            TilePlan holdMidHold = Classify("hold midspin hold",
                new[] { Tile(0), Tile(1, hold: 0), Mid(2), Tile(2, hold: 0), Tile(3) },
                new[] { 0, 1, 0, 1, 0 }, new[] { -1, -1, 0, -1, 1 });
            Check(holdMidHold.Events[0].ReleaseTime == 2 && holdMidHold.Events[1].ReleaseTime == 3, "hold midspin hold times");
            Check(holdMidHold.Events[1].Time == 2, "second hold pressed when first is released");

            // 미드스핀 뒤 홀드: 첫 착지의 입력이 뒤 타일의 홀드를 잡는다.
            TilePlan midThenHold = Classify("midspin then hold",
                new[] { Tile(0), Tile(1), Mid(2), Tile(2, hold: 0), Tile(3), Tile(4) },
                new[] { 0, 1, 1, 0, 0, 1 }, new[] { -1, -1, -1, -1, 1, -1 });
            Check(midThenHold.Events[1].Time == 2 && midThenHold.Events[1].ReleaseTime == 3, "midspin press holds until next landing");

            // 홀드: pitch를 거친 실제 시각으로 뗀다.
            TilePlan hold = Classify("hold",
                new[] { Tile(0), Tile(1, hold: 1), Tile(4), Tile(5) },
                new[] { 0, 1, 0, 1 }, new[] { -1, -1, 0, -1 }, pitch: 2);
            Check(hold.Events[0].ReleaseTime == 2, $"hold release {hold.Events[0].ReleaseTime}");

            // 홀드 뒤 홀드: 같은 착지에서 떼고 누른다.
            TilePlan holdHold = Classify("hold after hold",
                new[] { Tile(0), Tile(1, hold: 0), Tile(2, hold: 0), Tile(3) },
                new[] { 0, 1, 1, 0 }, new[] { -1, -1, 0, 1 });
            Check(holdHold.Events[0].ReleaseTime == 2 && holdHold.Events[1].Time == 2, "hold after hold times");

            // 다중탭: 같은 시각에 횟수만큼, HitOnce면 한 번. 홀드를 잡는 것은 마지막 입력이다.
            TilePlan multi = Classify("multitap",
                new[] { Tile(0), Tile(1, taps: 3), Tile(2) },
                new[] { 0, 3, 1 }, new[] { -1, -1, -1 });
            Check(multi.Events[0].Time == 1 && multi.Events[2].Time == 1, "multitap events share time");
            Classify("multitap hit once",
                new[] { Tile(0), Tile(1, taps: 3), Tile(2) },
                new[] { 0, 1, 1 }, new[] { -1, -1, -1 }, hitOnce: true);
            TilePlan multiHold = Classify("multitap hold",
                new[] { Tile(0), Tile(1, taps: 2, hold: 0), Tile(2) },
                new[] { 0, 2, 0 }, new[] { -1, -1, 1 });
            Check(multiHold.Events[0].ReleaseTime < 0 && multiHold.Events[1].ReleaseTime == 2, "last multitap press holds");

            // 자동 타일과 자동 홀드.
            Classify("auto tile",
                new[] { Tile(0), Tile(1), Tile(2, auto: true), Tile(3) },
                new[] { 0, 1, 0, 1 }, new[] { -1, -1, -1, -1 });
            Classify("auto hold",
                new[] { Tile(0), Tile(1), Tile(2, hold: 0, auto: true), Tile(3), Tile(4) },
                new[] { 0, 1, 0, 0, 1 }, new[] { -1, -1, -1, -1, -1 });
            TilePlan holdIntoAuto = Classify("hold into auto tile",
                new[] { Tile(0), Tile(1, hold: 0), Tile(2, auto: true), Tile(3) },
                new[] { 0, 1, 0, 1 }, new[] { -1, -1, 0, -1 });
            Check(holdIntoAuto.Events[0].ReleaseTime == 2, "held key released on auto landing");

            // 홀드 바로 뒤 다중탭: 떼기가 첫 탭이고 나머지만 더 누른다. 미드스핀을 거치면 무한 판정이 다 처리한다.
            Classify("hold then multitap",
                new[] { Tile(0), Tile(1, hold: 0), Tile(2, taps: 3), Tile(3) },
                new[] { 0, 1, 2, 1 }, new[] { -1, -1, 0, -1 });
            Classify("hold then multitap hit once",
                new[] { Tile(0), Tile(1, hold: 0), Tile(2, taps: 3), Tile(3) },
                new[] { 0, 1, 0, 1 }, new[] { -1, -1, 0, -1 }, hitOnce: true);
            Classify("hold midspin multitap",
                new[] { Tile(0), Tile(1, hold: 0), Mid(2), Tile(2, taps: 3), Tile(3) },
                new[] { 0, 1, 0, 0, 1 }, new[] { -1, -1, 0, -1, -1 });

            // 자동 홀드는 알아서 진행하므로 앞 미드스핀의 입력은 탭으로 끝난다.
            TilePlan midAutoHold = Classify("manual midspin then auto hold",
                new[] { Tile(0), Tile(1), Mid(2), Tile(2, hold: 0, auto: true), Tile(3) },
                new[] { 0, 1, 1, 0, 0 }, new[] { -1, -1, -1, -1, -1 });
            Check(midAutoHold.Events[1].ReleaseTime < 0, "midspin into auto hold stays a tap");

            // 자동 홀드 다음 타일은 입력이 없고, 그 타일이 홀드여도 눌린 키가 없으니 뗄 것도 없다.
            Classify("auto hold then manual hold",
                new[] { Tile(0), Tile(1, hold: 0, auto: true), Tile(2, hold: 0), Tile(3) },
                new[] { 0, 0, 0, 0 }, new[] { -1, -1, -1, -1 });
            Classify("hold, two midspins, hold",
                new[] { Tile(0), Tile(1, hold: 0), Mid(2), Mid(2), Tile(2, hold: 0), Tile(3) },
                new[] { 0, 1, 0, 0, 1, 0 }, new[] { -1, -1, 0, -1, -1, 1 });

            // 홀드, 다중탭 미드스핀, 홀드: 미드스핀의 입력이 뒤 타일의 홀드까지 잡으므로 뒤 타일에 입력을 또 내지 않는다.
            TileInfo tapMid = Mid(2);
            tapMid.TapsNeeded = 2;
            Classify("hold, multitap midspin, hold",
                new[] { Tile(0), Tile(1, hold: 0), tapMid, Tile(2, hold: 0), Tile(3), Tile(4) },
                new[] { 0, 1, 1, 0, 0, 1 }, new[] { -1, -1, 0, -1, 1, -1 });

            // 첫 타일: 0번은 입력이 없고, 0번이 홀드면 뗄 키도 없다.
            Classify("first tile hold",
                new[] { Tile(0, hold: 0), Tile(1), Tile(2) },
                new[] { 0, 0, 1 }, new[] { -1, -1, -1 });
            Classify("first tile midspin",
                new[] { Mid(0), Tile(0), Tile(1) },
                new[] { 0, 1, 1 }, new[] { -1, -1, -1 });
            Classify("single tile", new[] { Tile(0) }, new[] { 0 }, new[] { -1 });
            Classify("no tiles", new TileInfo[0], new int[0], new int[0]);

            // 뗄 착지가 없는 마지막 타일의 홀드는 켜진 채 남지 않게 탭으로 낮춘다.
            TilePlan lastHold = Classify("hold on last tile",
                new[] { Tile(0), Tile(1, hold: 0) },
                new[] { 0, 1 }, new[] { -1, -1 });
            Check(lastHold.Events[0].ReleaseTime < 0, "last tile hold becomes tap");

            bool threw = false;
            try
            {
                TileClassifier.Build(new[] { Tile(0) }, 0, false);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            Check(threw, "pitch 0 rejected");
        }

        private static bool Near(IEnumerable<double> actual, params double[] expected)
        {
            double[] values = actual.ToArray();
            if (values.Length != expected.Length) return false;
            for (int i = 0; i < values.Length; i++)
            {
                if (Math.Abs(values[i] - expected[i]) > 1e-9) return false;
            }

            return true;
        }

        private static string Roles(Layout layout)
        {
            return string.Join(" ", layout.Slots.Select(s =>
                (s.Enabled ? "" : "-") + (s.Hand == Hand.Left ? "L" : "R") + s.Finger));
        }

        private static int[] Codes(int count)
        {
            return Enumerable.Range(0, count).Select(i => 1000 + i).ToArray();
        }

        private static void TestLayouts()
        {
            const string key8 = "LPinky LRing LMiddle LIndex RIndex RMiddle RRing RPinky";
            const string key12 = key8 + " LThumb1 LThumb2 RThumb1 RThumb2";
            const string key16 = key12 + " LPalm2 LPalm1 RPalm1 RPalm2";

            Check(Roles(Layouts.Build(Codes(4))) == "LMiddle LIndex RIndex RMiddle", "4 keys");
            Check(Roles(Layouts.Build(Codes(8))) == key8, "8 keys");
            Check(Roles(Layouts.Build(Codes(10))) == key8 + " LThumb1 RThumb1", "10 keys");
            Check(Roles(Layouts.Build(Codes(12))) == key12, "12 keys");
            Check(Roles(Layouts.Build(Codes(16))) == key16, "16 keys");
            Check(Roles(Layouts.Build(Codes(20))) == key16 + " -LPalm2 -LPalm2 -RPalm2 -RPalm2", "20 keys");

            // 다시 묶은 Key16 배열의 실사용 예: Tab 1 2 E P = Back \ Space C VK21 . Caps LShift RShift Return
            string[] jipperLabels = "Tab 1 2 E P = Back \\ Space C VK21 . Caps LShift RShift Return".Split(' ');
            Layout jipper = Layouts.Build(Jipper16, jipperLabels);
            Check(Roles(jipper) == key16, $"jipper order: {Roles(jipper)}");
            Check(Describe(jipper, 8) == "Space:LThumb1", Describe(jipper, 8));
            Check(Describe(jipper, 9) == "C:LThumb2", Describe(jipper, 9));
            Check(Describe(jipper, 10) == "VK21:RThumb1", Describe(jipper, 10));
            Check(Describe(jipper, 11) == ".:RThumb2", Describe(jipper, 11));
            Check(Describe(jipper, 12) == "Caps:LPalm2", Describe(jipper, 12));
            Check(Describe(jipper, 13) == "LShift:LPalm1", Describe(jipper, 13));
            Check(Describe(jipper, 14) == "RShift:RPalm1", Describe(jipper, 14));
            Check(Describe(jipper, 15) == "Return:RPalm2", Describe(jipper, 15));

            // 표에 없는 개수: 위치로 추정한다. 왼쪽 절반은 새끼에서 검지로, 오른쪽 절반은 검지에서 새끼로.
            Check(Roles(Layouts.Build(Codes(6))) == "LRing LMiddle LIndex RIndex RMiddle RRing", "6 keys fallback");
            Check(Roles(Layouts.Build(Codes(2))) == "LIndex RIndex", "2 keys fallback");
            Check(Roles(Layouts.Build(Codes(5))) == "LMiddle LIndex RIndex RMiddle RRing", "5 keys fallback");

            // Space는 위치로, Alt는 이름으로 가까운 손의 엄지가 된다.
            Check(Roles(Layouts.Build(new[] { 1001, 1002, 1003, 1004, 32, 1005, 1006 }))
                == "LRing LMiddle LIndex RIndex RThumb1 RMiddle RRing", "7 keys, space in right half");
            Check(Roles(Layouts.Build(new[] { 1001, 32, 1002, 1003, 1004, 1005, 1006 }))
                == "LRing LThumb1 LMiddle LIndex RIndex RMiddle RRing", "7 keys, space in left half");
            Check(Roles(Layouts.Build(new[] { 308, 1001, 1002, 1003, 1004, 307, 32 }))
                == "LThumb1 LMiddle LIndex RIndex RMiddle RThumb1 RThumb2", "alt keys by name");
            Check(Roles(Layouts.Build(new[] { 308, 308, 308, 1001, 1002 }))
                == "LThumb1 LThumb2 -LThumb2 LIndex RIndex", "third thumb disabled");

            // AdofaiTweaks 프로필은 등록 순서다. 개수가 10이어도 Space가 중간이면 표를 쓰면 안 된다.
            Check(Roles(Layouts.Build(new[] { 97, 115, 100, 102, 32, 107, 111, 108, 59, 39 }))
                == "LPinky LRing LMiddle LIndex LThumb1 RIndex RMiddle RRing RPinky RPalm1", "tweaks profile");

            // 역할이 모자라면 나머지는 끈다.
            Layout big = Layouts.Build(Codes(22));
            Check(big.Slots.Length == 22 && big.Slots.Count(s => s.Enabled) == 12, "22 keys keep only available roles");
            Check(!big.Slots[4].Enabled && big.Slots[5].Enabled && big.Slots[5].Finger == Finger.Palm2, "keys past Palm2 disabled");

            // 역할 덮어쓰기: 개수가 다른 항목은 무시하고, 범위 밖 슬롯에 죽지 않는다.
            Layout edited = Layout8();
            Layouts.ApplyOverrides(edited, new[]
            {
                new RoleOverride { KeyCount = 8, Slot = 0, Hand = Hand.Right, Finger = Finger.Thumb1 },
                new RoleOverride { KeyCount = 8, Slot = 7, Hand = Hand.Right, Finger = Finger.Pinky, Enabled = false },
                new RoleOverride { KeyCount = 4, Slot = 1, Hand = Hand.Right, Finger = Finger.Palm2 },
                new RoleOverride { KeyCount = 8, Slot = 99, Hand = Hand.Right, Finger = Finger.Palm2 },
                null,
            });
            Check(Roles(edited) == "RThumb1 LRing LMiddle LIndex RIndex RMiddle RRing -RPinky", $"overrides: {Roles(edited)}");
            Layouts.ApplyOverrides(edited, null);

            // 끈 슬롯은 쓰지 않는다.
            Layout noPinky = Layout8();
            Layouts.ApplyOverrides(noPinky, new[]
            {
                new RoleOverride { KeyCount = 8, Slot = 7, Hand = Hand.Right, Finger = Finger.Pinky, Enabled = false },
            });
            byte[] noPinkySlots = FingeringSolver.Solve(Stream(200, 0.020, 4), noPinky, new SolverOptions()).Slot;
            Check(noPinkySlots.All(s => s != 7), "disabled slot never used");

            // 역할이 겹치면 앞 슬롯만 쓴다.
            Layout duplicate = Layout4();
            Layouts.ApplyOverrides(duplicate, new[]
            {
                new RoleOverride { KeyCount = 4, Slot = 3, Hand = Hand.Right, Finger = Finger.Index },
            });
            byte[] duplicateSlots = FingeringSolver.Solve(Stream(100, 0.050, 3), duplicate, new SolverOptions()).Slot;
            Check(duplicateSlots.All(s => s != 3), "duplicate slot never used");

            // 엣지: 한 손, 활성 슬롯 하나, 활성 슬롯 없음, 슬롯 없음.
            Layout oneHand = Layouts.Build(new[] { 106, 107, 108, 59 });
            Layouts.ApplyOverrides(oneHand, new[]
            {
                new RoleOverride { KeyCount = 4, Slot = 0, Hand = Hand.Right, Finger = Finger.Index },
                new RoleOverride { KeyCount = 4, Slot = 1, Hand = Hand.Right, Finger = Finger.Middle },
                new RoleOverride { KeyCount = 4, Slot = 2, Hand = Hand.Right, Finger = Finger.Ring },
                new RoleOverride { KeyCount = 4, Slot = 3, Hand = Hand.Right, Finger = Finger.Pinky },
            });
            Check(FingeringSolver.Solve(Stream(200, 0.020, 4), oneHand, new SolverOptions()).Slot.All(s => s < 4), "one hand solves");

            Layout single = Layouts.Build(Codes(1));
            Check(FingeringSolver.Solve(Stream(10, 0.1, 2), single, new SolverOptions()).Slot.All(s => s == 0), "single key solves");

            Layout none = Layout4();
            foreach (KeySlot slot in none.Slots) slot.Enabled = false;
            Check(FingeringSolver.Solve(Stream(3, 0.1, 2), none, new SolverOptions()).Slot.All(s => s == Solution.NoSlot), "no keys gives NoSlot");
            Check(FingeringSolver.Solve(new PressEvent[0], Layout4(), new SolverOptions()).Slot.Length == 0, "no events");
        }

        private static string Describe(Layout layout, int index)
        {
            KeySlot slot = layout.Slots[index];
            return $"{slot.Label}:{(slot.Hand == Hand.Left ? "L" : "R")}{slot.Finger}";
        }

        private static void TestDeterminismAndPerformance()
        {
            PressEvent[] sample = MixedStream(50000, 7);
            Solution first = FingeringSolver.Solve(sample, Layout16(), new SolverOptions());
            Solution second = FingeringSolver.Solve(sample, Layout16(), new SolverOptions());
            Check(first.Slot.SequenceEqual(second.Slot), "same input gives same slots");
            Check(first.FloorViolations == second.FloorViolations, "same input gives same violations");
            Console.WriteLine($"  sample checksum {Checksum(first.Slot):x8}, violations {first.FloorViolations}");
            // 프로세스가 달라도 같아야 하므로 값을 고정한다. 비용 가중치를 일부러 바꿨을 때만 갱신한다.
            Check(Checksum(first.Slot) == 0xea46d597, "checksum changed between runs");

            PressEvent[] million = MixedStream(1000000, 11);
            var watch = Stopwatch.StartNew();
            Solution big = FingeringSolver.Solve(million, Layout16(), new SolverOptions());
            watch.Stop();
            Console.WriteLine($"  1,000,000 events, 16 keys: {watch.Elapsed.TotalSeconds:F2} s, violations {big.FloorViolations}");
            // 실제 맵은 수천에서 수만 타라 1초 안쪽이다. 100만 타는 최악 조건의 상한만 본다.
            Check(watch.Elapsed.TotalSeconds < 60, $"performance target missed: {watch.Elapsed.TotalSeconds:F2} s");
            Check(big.FloorViolations == RecountViolations(million, big.Slot, 0.130 * 0.75), "million recount");
        }

        // 느린 구간, 빠른 구간, 멈춤을 섞어 묶음 전환까지 포함한 최악에 가까운 입력을 만든다.
        private static PressEvent[] MixedStream(int count, int seed)
        {
            var random = new Random(seed);
            var events = new PressEvent[count];
            double time = 0;
            double beat = 0;
            int i = 0;
            while (i < count)
            {
                int length = Math.Min(random.Next(20, 400), count - i);
                double gap = new[] { 0.010, 0.012, 0.016, 0.020, 0.025, 0.035, 0.050, 0.100, 0.200 }[random.Next(9)];
                double notesPerBeat = new[] { 1.0, 2.0, 3.0, 4.0, 6.0, 8.0 }[random.Next(6)];
                for (int j = 0; j < length; j++)
                {
                    events[i++] = new PressEvent(time, beat);
                    time += gap;
                    beat += 1 / notesPerBeat;
                }

                if (random.Next(4) == 0) time += 1;
            }

            return events;
        }

        private static uint Checksum(byte[] data)
        {
            uint hash = 2166136261;
            foreach (byte value in data) hash = (hash ^ value) * 16777619;
            return hash;
        }

        private static void TestBaseAndCancel()
        {
            // 4키 기반은 같은 손가락을 더 여유 있게 다시 치므로 같은 속도에서 키를 더 쓴다.
            Layout layout = Layout8();
            PressEvent[] events = Stream(64, 0.0735, 4);
            int keys2 = Distinct(FingeringSolver.Solve(events, layout, new SolverOptions { Floor = 0.107 }).Slot, 8, 56);
            int keys4 = Distinct(FingeringSolver.Solve(events, layout, new SolverOptions { Floor = 0.107, Base = 4 }).Slot, 8, 56);
            Check(keys2 < keys4, $"base 2 uses {keys2} keys, base 4 uses {keys4}");

            var source = new CancellationTokenSource();
            source.Cancel();
            bool cancelled = false;
            try
            {
                FingeringSolver.Solve(events, layout, new SolverOptions(), source.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Check(cancelled, "cancelled token stops the solve");
        }

        private static void TestKeyScheduler()
        {
            const float Dt = 0.016f;
            var log = new List<(int Slot, bool Down)>();
            var keys = new KeyScheduler(2, (slot, down) => log.Add((slot, down)));
            int frame = 0;

            // 프레임 번호와 함께 시계를 옮기고 Tick까지 돈다. 실제 Session.Tick과 같은 순서다.
            void Step(float time)
            {
                keys.SetClock(time, ++frame, Dt);
                keys.Tick();
            }

            // 탭은 간격의 80%만큼 켜진다.
            keys.SetClock(1f, ++frame, Dt);
            keys.Press(0, 0, false, 0.08f);
            Step(1.063f);
            Check(log.Count == 1 && log[0] == (0, true), "tap stays lit before 0.8 * gap");
            Step(1.065f);
            Check(log.Count == 2 && log[1] == (0, false), "tap goes dark after 0.8 * gap");
            Check(keys.TapsDrained, "no tap left");

            // 다음 사용이 없으면 상한 70 ms, 간격이 짧으면 하한 50 ms에서 한 프레임을 뺀다.
            log.Clear();
            keys.SetClock(2f, ++frame, Dt);
            keys.Press(0, 1, false, float.PositiveInfinity);
            Step(2.069f);
            Check(log.Count == 1, "unused slot stays lit below the 70 ms cap");
            Step(2.071f);
            Check(log.Count == 2, "unused slot goes dark at the 70 ms cap");

            log.Clear();
            keys.SetClock(3f, ++frame, Dt);
            keys.Press(0, 2, false, 0.055f);
            Step(3.038f);
            Check(log.Count == 1, "short gap stays lit until one frame before the repeat");
            Step(3.040f);
            Check(log.Count == 2, "short gap leaves one frame unlit before the repeat");

            // 켜져 있는 슬롯을 다시 누르면 이번 프레임에 끄고 다음 프레임에 켠다.
            log.Clear();
            keys.SetClock(4f, ++frame, Dt);
            keys.Press(1, 3, false, 1f);
            keys.SetClock(4.01f, ++frame, Dt);
            keys.Press(1, 4, false, 1f);
            Check(log.Count == 2 && log[1] == (1, false), "repeat on a lit slot releases first");
            keys.Tick();
            Check(log.Count == 2, "release and press never share a frame");
            Check(!keys.TapsDrained, "deferred press still counts as pending");
            Step(4.02f);
            Check(log.Count == 3 && log[2] == (1, true), "deferred press fires on the next frame");
            Step(4.5f);
            Check(log.Count == 4 && keys.TapsDrained, "deferred press times out like a tap");

            // 홀드는 뗄 착지까지 켜져 있고, 다른 이벤트의 떼기나 중복 떼기는 무시한다.
            log.Clear();
            keys.SetClock(5f, ++frame, Dt);
            keys.Press(0, 5, true, 0.1f);
            Step(9f);
            Check(log.Count == 1, "hold outlasts the tap cap");
            Check(keys.TapsDrained, "a hold is not a tap");
            keys.ReleaseHold(0, 99);
            Check(log.Count == 1, "release of another event is ignored");
            keys.ReleaseHold(0, 5);
            keys.ReleaseHold(0, 5);
            Check(log.Count == 2 && log[1] == (0, false), "hold release is idempotent");
            keys.ReleaseHold(1, 6);
            Check(log.Count == 2, "release with no press is a no-op");

            // 켜지기 전에 끝난 홀드는 탭으로 보여 주고 시간이 지나면 꺼진다.
            log.Clear();
            keys.SetClock(10f, ++frame, Dt);
            keys.Press(0, 7, false, 1f);
            keys.Press(0, 8, true, 1f);
            keys.ReleaseHold(0, 8);
            Check(log.Count == 1, "a key is never turned off in the frame it was lit");
            Step(10.016f);
            Check(log.Count == 2 && log[1] == (0, false), "waiting press turns the key off on the next frame");
            Step(10.032f);
            Check(log.Count == 3 && log[2] == (0, true), "pending hold still lights up");
            Step(11f);
            Check(log.Count == 4 && log[3] == (0, false), "ended pending hold times out as a tap");

            // 초기화는 켜진 키를 전부 끄고 대기 입력을 버린다.
            log.Clear();
            keys.SetClock(12f, ++frame, Dt);
            keys.Press(0, 9, true, 1f);
            keys.Press(1, 10, false, 1f);
            keys.Press(1, 11, false, 1f);
            keys.Reset();
            Check(log.Count(entry => entry.Down) == 2 && log.Count(entry => !entry.Down) == 2, "reset turns everything off");
            Check(keys.TapsDrained, "reset drops pending presses");
            Step(13f);
            Check(log.Count == 4, "nothing fires after reset");

            // 3프레임 연속으로 누르면 대기 입력은 이번 입력에 흡수된다. 불이 꺼진 뒤 없는 입력이 켜지면 안 된다.
            log.Clear();
            keys.SetClock(14f, ++frame, Dt);
            keys.Press(0, 12, false, 0.05f);
            keys.Tick();
            keys.SetClock(14.016f, ++frame, Dt);
            keys.Press(0, 13, false, 0.05f);
            keys.Tick();
            keys.SetClock(14.032f, ++frame, Dt);
            keys.Press(0, 14, false, 0.05f);
            keys.Tick();
            Step(15f);
            Step(15.016f);
            Check(log.Count(entry => entry.Down) == 2, $"no phantom press, got {log.Count(entry => entry.Down)} presses");
            Check(keys.TapsDrained, "phantom scenario leaves nothing pending");

            // 간격이 한 프레임보다 짧아도 켠 프레임에는 끄지 않는다.
            log.Clear();
            keys.SetClock(16f, ++frame, Dt);
            keys.Press(1, 15, false, 0.01f);
            keys.Tick();
            Check(log.Count == 1, "key stays lit through the frame it was pressed");
            Step(16.016f);
            Check(log.Count == 2 && log[1] == (1, false), "key goes dark on the following frame");

            // 간격은 슬롯별 다음 사용까지의 시간이고, 키가 없는 이벤트는 건너뛴다.
            PressEvent[] events =
            {
                new PressEvent(0.0, 0), new PressEvent(0.1, 0), new PressEvent(0.25, 0), new PressEvent(0.5, 0),
            };
            float[] gaps = KeyScheduler.BuildGaps(events, new byte[] { 0, 1, Solution.NoSlot, 0 }, 2);
            Check(Math.Abs(gaps[0] - 0.5f) < 1e-6f, "gap to the next use of the same slot");
            Check(float.IsPositiveInfinity(gaps[1]), "last use of a slot has no gap");
            Check(float.IsPositiveInfinity(gaps[2]), "event with no slot has no gap");
            Check(float.IsPositiveInfinity(gaps[3]), "last event has no gap");
        }
    }
}
