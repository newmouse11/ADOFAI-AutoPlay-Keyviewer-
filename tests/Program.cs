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
        private static readonly RollStyle[] Styles =
            { RollStyle.Outward, RollStyle.Inward, RollStyle.Stair, RollStyle.ReverseStair };

        private static readonly Hand[] Hands = { Hand.Right, Hand.Left };

        // 스타일 표를 코드와 따로 적어 둔다. 코드에서 유도하면 표와 어긋나도 테스트가 잡지 못한다.
        private static readonly Dictionary<(RollStyle, Hand, int), string> CycleTable = new()
        {
            [(RollStyle.Outward, Hand.Right, 4)] = "jkfd",
            [(RollStyle.Inward, Hand.Right, 4)] = "kjdf",
            [(RollStyle.Stair, Hand.Right, 4)] = "jkdf",
            [(RollStyle.ReverseStair, Hand.Right, 4)] = "kjfd",
            [(RollStyle.Outward, Hand.Left, 4)] = "fdjk",
            [(RollStyle.Inward, Hand.Left, 4)] = "dfkj",
            [(RollStyle.Stair, Hand.Left, 4)] = "dfjk",
            [(RollStyle.ReverseStair, Hand.Left, 4)] = "fdkj",
            [(RollStyle.Outward, Hand.Right, 8)] = "jkl;fdsa",
            [(RollStyle.Inward, Hand.Right, 8)] = ";lkjasdf",
            [(RollStyle.Stair, Hand.Right, 8)] = "jkl;asdf",
            [(RollStyle.ReverseStair, Hand.Right, 8)] = ";lkjfdsa",
            [(RollStyle.Outward, Hand.Left, 8)] = "fdsajkl;",
            [(RollStyle.Inward, Hand.Left, 8)] = "asdf;lkj",
            [(RollStyle.Stair, Hand.Left, 8)] = "asdfjkl;",
            [(RollStyle.ReverseStair, Hand.Left, 8)] = "fdsa;lkj",
        };

        private static readonly Dictionary<(RollStyle, Hand, TripletSplit), string> TripletTable = new()
        {
            [(RollStyle.Outward, Hand.Right, TripletSplit.MainTwoFingers)] = "jkf",
            [(RollStyle.Inward, Hand.Right, TripletSplit.MainTwoFingers)] = "kjf",
            [(RollStyle.Stair, Hand.Right, TripletSplit.MainTwoFingers)] = "jkf",
            [(RollStyle.ReverseStair, Hand.Right, TripletSplit.MainTwoFingers)] = "kjf",
            [(RollStyle.Outward, Hand.Left, TripletSplit.MainTwoFingers)] = "fdj",
            [(RollStyle.Inward, Hand.Left, TripletSplit.MainTwoFingers)] = "dfj",
            [(RollStyle.Stair, Hand.Left, TripletSplit.MainTwoFingers)] = "dfj",
            [(RollStyle.ReverseStair, Hand.Left, TripletSplit.MainTwoFingers)] = "fdj",
            [(RollStyle.Outward, Hand.Right, TripletSplit.OffTwoFingers)] = "jfd",
            [(RollStyle.Inward, Hand.Right, TripletSplit.OffTwoFingers)] = "jdf",
            [(RollStyle.Stair, Hand.Right, TripletSplit.OffTwoFingers)] = "jdf",
            [(RollStyle.ReverseStair, Hand.Right, TripletSplit.OffTwoFingers)] = "jfd",
            [(RollStyle.Outward, Hand.Left, TripletSplit.OffTwoFingers)] = "fjk",
            [(RollStyle.Inward, Hand.Left, TripletSplit.OffTwoFingers)] = "fkj",
            [(RollStyle.Stair, Hand.Left, TripletSplit.OffTwoFingers)] = "fjk",
            [(RollStyle.ReverseStair, Hand.Left, TripletSplit.OffTwoFingers)] = "fkj",
        };

        private static readonly int[] Jipper16 =
            { 9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 4117, 46, 301, 304, 303, 13 };

        private static int _failures;
        private static int _checks;

        private static int Main()
        {
            Run("1 style cycles", TestStyleCycles);
            Run("2 same-finger floor", TestFloor);
            Run("3 slow alternation", TestSlowAlternation);
            Run("3b style recovery", TestStyleRecovery);
            Run("3c chords and extra keys", TestChordsAndExtraKeys);
            Run("4 burst anchor", TestBurstAnchor);
            Run("5 triplets", TestTriplets);
            Run("6 holds", TestHolds);
            Run("7 tempo change", TestTempoChange);
            Run("8 classifier", TestClassifier);
            Run("9 layouts", TestLayouts);
            Run("10 determinism and performance", TestDeterminismAndPerformance);
            Run("extra base and cancel", TestBaseAndCancel);
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

            static SpeedTest Alternate(double kps, Func<double, double> at)
            {
                var test = new SpeedTest();
                for (int i = 0; i / kps < SpeedTest.Duration + 1; i++)
                {
                    test.Press(i % 2 == 0 ? KeyA : KeyB, at(100 + i / kps));
                }

                return test;
            }

            Check(Math.Abs(Alternate(15, t => t).Kps() - 15) < 0.01, "exact alternation");

            foreach (double kps in new[] { 8.0, 11.3, 13.7, 18.0, 24.0 })
            {
                double measured = Alternate(kps, Frame).Kps();
                Check(Math.Abs(measured - kps) / kps < 0.02, $"60 fps at {kps} KPS measured {measured}");
            }

            // 번갈아 치는 리듬이 어긋나도 같은 키 간격이 같으면 값은 같아야 한다.
            var uneven = new SpeedTest();
            for (int i = 0; i < 30; i++)
            {
                uneven.Press(KeyA, 100 + i * 0.2);
                uneven.Press(KeyB, 100.05 + i * 0.2);
            }

            Check(Math.Abs(uneven.Kps() - 10) < 0.01, $"uneven rhythm measured {uneven.Kps()}");

            var paused = new SpeedTest();
            for (int i = 0; i < 60; i++) paused.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i / 15.0 + (i >= 30 ? 0.6 : 0));
            Check(Math.Abs(paused.Kps() - 15) / 15 < 0.05, $"a short stop measured {paused.Kps()}");

            var bouncy = new SpeedTest();
            for (int i = 0; i < 40; i++)
            {
                int key = i % 2 == 0 ? KeyA : KeyB;
                bouncy.Press(key, 100 + i / 12.0);
                bouncy.Press(key, 100.01 + i / 12.0);
            }

            Check(bouncy.Presses == 40, $"bounced presses counted: {bouncy.Presses}");
            Check(Math.Abs(bouncy.Kps() - 12) < 0.01, $"bounce measured {bouncy.Kps()}");

            var thirdKey = new SpeedTest();
            for (int i = 0; i < 40; i++)
            {
                thirdKey.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i / 12.0);
                if (i > 0) thirdKey.Press(999, 100.02 + i / 12.0);
            }

            Check(Math.Abs(thirdKey.Kps() - 12) < 0.01, $"third key measured {thirdKey.Kps()}");

            // 처음 건드린 키가 측정 키로 잡혀도 몇 번 안 눌리면 틀린 값을 내지 않고 다시 하게 한다.
            var strayKey = new SpeedTest();
            strayKey.Press(999, 100);
            for (int i = 0; i < 40; i++) strayKey.Press(i % 2 == 0 ? KeyA : KeyB, 100.1 + i / 12.0);
            Check(strayKey.Kps() == 0, $"a stray first key measured {strayKey.Kps()}");

            var oneKey = new SpeedTest();
            for (int i = 0; i < 40; i++) oneKey.Press(KeyA, 100 + i / 8.0);
            Check(oneKey.Kps() == 0, "one key has no 2-key speed");

            var tooFew = new SpeedTest();
            for (int i = 0; i < 6; i++) tooFew.Press(i % 2 == 0 ? KeyA : KeyB, 100 + i * 0.1);
            Check(tooFew.Kps() == 0, "too few presses");

            var idle = new SpeedTest();
            Check(!idle.Started && !idle.IsOver(1000), "the clock waits for the first press");
            Check(idle.Remaining(1000) == SpeedTest.Duration, "full time left before the first press");
            Check(idle.Kps() == 0 && idle.LiveKps() == 0, "no value before any press");

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

        private static string Repeat(string cycle, int count)
        {
            return string.Concat(Enumerable.Range(0, count).Select(i => cycle[i % cycle.Length]));
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

        private static void TestStyleCycles()
        {
            foreach (RollStyle style in Styles)
            {
                foreach (Hand hand in Hands)
                {
                    string key4 = CycleTable[(style, hand, 4)];
                    string key8 = CycleTable[(style, hand, 8)];
                    string name = $"{style}/{hand}";

                    foreach (double notesPerBeat in new[] { 1.0, 2.0, 4.0 })
                    {
                        CheckCycle(Layout4(), style, hand, SolverOptions.AllKeys, 0.035, notesPerBeat, key4, name);
                        CheckCycle(Layout8(), style, hand, 4, 0.035, notesPerBeat, key4, name + " max4");
                    }

                    foreach (double notesPerBeat in new[] { 1.0, 4.0, 8.0 })
                    {
                        CheckCycle(Layout8(), style, hand, 8, 0.020, notesPerBeat, key8, name + " max8");
                    }

                    // K=2는 어느 스타일이든 주손 검지 다음 부손 검지다.
                    string key2 = hand == Hand.Right ? "jf" : "fj";
                    CheckCycle(Layout4(), style, hand, SolverOptions.AllKeys, 0.100, 2.0, key2, name + " k2");
                    CheckCycle(Layout8(), style, hand, 2, 0.100, 2.0, key2, name + " k2 max2");

                    // 곧은 연타는 어느 속도에서도 3키로 떨어지면 안 되고, 표의 4키 사이클을 그대로 돈다.
                    for (int ms = 33; ms <= 64; ms++)
                    {
                        foreach (double notesPerBeat in new[] { 1.0, 2.0, 4.0 })
                        {
                            CheckCycle(Layout8(), style, hand, SolverOptions.AllKeys, ms / 1000.0, notesPerBeat, key4,
                                $"{name} sweep {ms}ms/{notesPerBeat}");
                        }
                    }

                    for (int ms = 17; ms <= 21; ms++)
                    {
                        CheckCycle(Layout8(), style, hand, SolverOptions.AllKeys, ms / 1000.0, 4.0, key8,
                            $"{name} sweep {ms}ms");
                    }
                }
            }
        }

        private static void CheckCycle(
            Layout layout, RollStyle style, Hand hand, int maxKeys, double gap, double notesPerBeat, string cycle, string name)
        {
            var options = new SolverOptions { Style = style, MainHand = hand, MaxKeys = maxKeys };
            string actual = Labels(layout, FingeringSolver.Solve(Stream(64, gap, notesPerBeat), layout, options).Slot);
            string expected = Repeat(cycle, 64);
            Check(actual == expected, $"{name}: expected {expected.Substring(0, 16)} got {actual.Substring(0, 16)}");
        }

        private static void TestFloor()
        {
            CheckNoViolations(Layout8(), Stream(500, 0.020, 4), new SolverOptions(), "8 keys 20ms");
            CheckNoViolations(Layout4(), Stream(500, 0.035, 4), new SolverOptions(), "4 keys 35ms");
            CheckNoViolations(Layout16(), Stream(500, 0.010, 4), new SolverOptions(), "16 keys 10ms");
            CheckNoViolations(Layout8(), Stream(500, 0.035, 4), new SolverOptions { Floor = 0.250 }, "floor 250");
            CheckNoViolations(Layout4(), Stream(500, 0.016, 4), new SolverOptions { Floor = 0.060 }, "floor 60");

            // 손가락이 충분한 고른 연타는 위반이 없을 뿐 아니라 권장 간격까지 지켜야 한다.
            foreach (var (layout, gap, floor) in new[] { (Layout8(), 0.020, 0.130), (Layout4(), 0.035, 0.130), (Layout8(), 0.035, 0.250), (Layout4(), 0.016, 0.060) })
            {
                PressEvent[] stream = Stream(500, gap, 4);
                byte[] slots = FingeringSolver.Solve(stream, layout, new SolverOptions { Floor = floor }).Slot;
                Check(RecountViolations(stream, slots, floor) == 0, $"gap {gap} floor {floor}: below the recommended interval");
            }

            // 불규칙한 간격: 어떤 8연속 구간도 Floor를 넘도록 간격 하한을 잡았으므로 위반 없이 풀 수 있다.
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new Random(seed);
                var events = new PressEvent[800];
                double time = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    events[i] = new PressEvent(time, i / 4.0);
                    time += 0.017 + random.NextDouble() * 0.2;
                }

                CheckNoViolations(Layout8(), events, new SolverOptions(), $"random seed {seed}");
            }

            // 다중탭: 같은 시각의 입력은 서로 다른 손가락으로 흩어져야 한다.
            var taps = new[] { new PressEvent(0, 0), new PressEvent(0, 0), new PressEvent(0, 0), new PressEvent(0, 0) };
            Solution chord = FingeringSolver.Solve(taps, Layout4(), new SolverOptions());
            Check(Distinct(chord.Slot, 0, 4) == 4, "4-tap uses 4 fingers");
            Check(chord.FloorViolations == 0, "4-tap has no violations");

            // 4키로 20ms는 물리적으로 불가능하다. 첫 4개만 빼고 전부 위반이어야 한다.
            PressEvent[] impossible = Stream(500, 0.020, 4);
            Solution forced = FingeringSolver.Solve(impossible, Layout4(), new SolverOptions());
            Check(forced.FloorViolations == impossible.Length - 4,
                $"4 keys 20ms violations expected {impossible.Length - 4} got {forced.FloorViolations}");
            Check(forced.FloorViolations == RecountViolations(impossible, forced.Slot, 0.130 * 0.75), "recount matches forced");
            Check(Distinct(forced.Slot, 0, impossible.Length) == 4, "forced stream uses every key");
        }

        private static void CheckNoViolations(Layout layout, PressEvent[] events, SolverOptions options, string name)
        {
            Solution solution = FingeringSolver.Solve(events, layout, options);
            int recount = RecountViolations(events, solution.Slot, options.Floor * 0.75);
            Check(solution.FloorViolations == 0, $"{name}: reported {solution.FloorViolations} violations");
            Check(recount == solution.FloorViolations, $"{name}: recount {recount} != {solution.FloorViolations}");
        }

        // 간격 1.4ms로 몰린 동타를 period마다 반복한다. 실제 맵(REX)의 8동타와 같은 모양이다.
        private static PressEvent[] Chords(int chords, int size, double period)
        {
            var events = new PressEvent[chords * size];
            for (int c = 0; c < chords; c++)
            {
                for (int n = 0; n < size; n++) events[c * size + n] = new PressEvent(1 + c * period + n * 0.0014, c);
            }

            return events;
        }

        private static IEnumerable<string> ChordSets(byte[] slots, int size)
        {
            for (int c = 0; c + size <= slots.Length; c += size)
            {
                yield return string.Join(",", slots.Skip(c).Take(size).OrderBy(s => s));
            }
        }

        private static void TestChordsAndExtraKeys()
        {
            foreach (RollStyle style in Styles)
            {
                var options = new SolverOptions { Style = style };

                // 8동타는 8손가락을 그대로 다시 친다. 빠르게 반복해도 아래 키로 돌려 쓰지 않는다.
                foreach (double period in new[] { 0.125, 0.250, 0.100 })
                {
                    Solution eight = FingeringSolver.Solve(Chords(40, 8, period), Layout16(), options);
                    Check(eight.Slot.All(s => s < 8), $"{style} 8 chord every {period}: used a lower key");
                    Check(ChordSets(eight.Slot, 8).All(set => set == "0,1,2,3,4,5,6,7"), $"{style} 8 chord every {period}: not all eight fingers");
                    Check(eight.FloorViolations == 0, $"{style} 8 chord every {period}: {eight.FloorViolations} violations");
                }

                // 9동타는 아래 키를 하나만 더 쓰고, 묶음마다 같은 손가락을 잡는다.
                Solution nine = FingeringSolver.Solve(Chords(30, 9, 0.250), Layout16(), options);
                Check(ChordSets(nine.Slot, 9).Distinct().Count() == 1, $"{style} 9 chord: finger set changes");
                Check(nine.Slot.Take(9).Count(s => s >= 8) == 1, $"{style} 9 chord: expected one lower key");

                // 손가락 수보다 적은 동타도 묶음마다 같은 손가락을 잡는다.
                foreach (int size in new[] { 2, 3, 4, 5, 6, 7 })
                {
                    Solution small = FingeringSolver.Solve(Chords(30, size, 0.250), Layout16(), options);
                    Check(ChordSets(small.Slot, size).Distinct().Count() == 1, $"{style} {size} chord: finger set changes");
                    Check(small.Slot.All(s => s < 8), $"{style} {size} chord: used a lower key");
                }
            }

            // 최대 키를 8로 막으면 9동타도 아래 키를 쓰지 않고, 못 지킨 만큼 위반으로 센다.
            Solution capped = FingeringSolver.Solve(Chords(30, 9, 0.250), Layout16(), new SolverOptions { MaxKeys = 8 });
            Check(capped.Slot.All(s => s < 8), "max 8 keys: used a lower key");
            Check(capped.FloorViolations >= 30, $"max 8 keys: {capped.FloorViolations} violations");

            // 8손가락으로 칠 수 있는 연타에는 아래 키가 없고, 기준에 2ms 모자라도 같은 키를 겹쳐 치지 않는다.
            foreach (double gap in new[] { 0.013, 0.016, 0.020 })
            {
                Solution stream = FingeringSolver.Solve(Stream(400, gap, 4), Layout16(), new SolverOptions());
                Check(stream.Slot.All(s => s < 8), $"stream gap {gap}: used a lower key");
                Check(stream.FloorViolations == 0, $"stream gap {gap}: {stream.FloorViolations} violations");
                for (int i = 1; i < stream.Slot.Length; i++)
                {
                    Check(stream.Slot[i] != stream.Slot[i - 1], $"stream gap {gap}: repeated a key at {i}");
                }
            }

            // 8손가락이 실제 한계를 넘는 연타만 아래 키를 쓴다. 이런 연타를 묶음으로 잘라 동타로 보면 안 된다.
            Solution extreme = FingeringSolver.Solve(Stream(400, 0.010, 4), Layout16(), new SolverOptions());
            Check(extreme.Slot.Any(s => s >= 8), "extreme stream: expected lower keys");
            Check(extreme.FloorViolations == 0, $"extreme stream: {extreme.FloorViolations} violations");
        }

        private static void TestSlowAlternation()
        {
            foreach (RollStyle style in Styles)
            {
                foreach (Hand hand in Hands)
                {
                    foreach (double gap in new[] { 0.15, 0.25, 0.3, 0.5 })
                    {
                        Layout layout = Layout8();
                        var options = new SolverOptions { Style = style, MainHand = hand };
                        string actual = Labels(layout, FingeringSolver.Solve(Stream(64, gap, 2), layout, options).Slot);
                        string expected = Repeat(hand == Hand.Right ? "jf" : "fj", 64);
                        Check(actual == expected, $"{style}/{hand} gap {gap}: {actual.Substring(0, 12)}");
                    }

                    // 모든 타일이 정박인 느린 곧은 타일도 한 키로 몰리면 안 된다. 게임처럼 시간을 누적해 경계값 흔들림까지 본다.
                    foreach (double gap in new[] { 0.4, 0.5, 1.0 })
                    {
                        foreach (double notesPerBeat in new[] { 1.0, 0.5 })
                        {
                            var events = new PressEvent[64];
                            double time = 0;
                            for (int i = 0; i < events.Length; i++)
                            {
                                events[i] = new PressEvent(time, i / notesPerBeat);
                                time += gap;
                            }

                            Layout layout = Layout8();
                            var options = new SolverOptions { Style = style, MainHand = hand };
                            string actual = Labels(layout, FingeringSolver.Solve(events, layout, options).Slot);
                            string body = actual.Substring(0, 62);
                            string expected = Repeat(hand == Hand.Right ? "jf" : "fj", 62);
                            Check(body == expected, $"{style}/{hand} straight gap {gap} npb {notesPerBeat}: {body.Substring(0, 20)}");
                        }
                    }
                }
            }
        }

        // 시작 위치만 다른 cycle 반복인지 본다.
        private static bool FollowsCycle(string actual, string cycle)
        {
            for (int offset = 0; offset < cycle.Length; offset++)
            {
                bool match = true;
                for (int i = 0; match && i < actual.Length; i++)
                {
                    match = actual[i] == cycle[(i + offset) % cycle.Length];
                }

                if (match) return true;
            }

            return false;
        }

        private static void TestStyleRecovery()
        {
            // 연타 안의 롱노트 하나가 남은 연타 전체의 표 순서를 깨면 안 된다.
            foreach (RollStyle style in Styles)
            {
                foreach (double gap in new[] { 0.033, 0.035, 0.040, 0.043 })
                {
                    foreach (int holdAt in new[] { 20, 37, 101 })
                    {
                        PressEvent[] events = Stream(1000, gap, 4);
                        events[holdAt] = new PressEvent(events[holdAt].Time, events[holdAt].Beat, events[holdAt].Time + 0.4);
                        for (int i = holdAt + 1; i < events.Length; i++)
                        {
                            events[i] = new PressEvent(events[i].Time + 0.4, events[i].Beat);
                        }

                        Layout layout = Layout4();
                        var options = new SolverOptions { Style = style, MaxKeys = 4 };
                        string actual = Labels(layout, FingeringSolver.Solve(events, layout, options).Slot);
                        string tail = actual.Substring(actual.Length - 200, 196);
                        Check(FollowsCycle(tail, CycleTable[(style, Hand.Right, 4)]), $"{style} gap {gap} hold {holdAt}: tail {tail.Substring(0, 16)}");
                    }
                }
            }

            // 16키 구간도 해법이 아니라 실제 풀이에서 표 순서를 지켜야 한다.
            foreach (RollStyle style in Styles)
            {
                Layout layout = Layout16();
                var options = new SolverOptions { Style = style };
                // 계단은 이 속도에서 수 초 뒤에야 표 순서로 안정된다. 충분히 길게 돌려 뒷부분만 본다.
                byte[] slots = FingeringSolver.Solve(Stream(8000, 0.009, 4), layout, options).Slot;
                Tier top = StyleCycle.BuildTiers(layout, options).Last();
                string cycle = Labels(layout, top.Cycle);
                string tail = Labels(layout, slots.Skip(7000).Take(400).ToArray());
                Check(FollowsCycle(tail, cycle), $"{style} 16 keys at 9 ms: {tail.Substring(0, 32)}, cycle {cycle}");
            }

            // 키 하나를 꺼도 남은 키를 다 써서 위반 없이 칠 수 있어야 한다.
            for (int disabled = 0; disabled < 8; disabled++)
            {
                Layout layout = Layout8();
                layout.Slots[disabled].Enabled = false;
                Solution solution = FingeringSolver.Solve(Stream(80, 0.022, 4), layout, new SolverOptions());
                Check(solution.FloorViolations == 0, $"slot {disabled} disabled at 22 ms: {solution.FloorViolations} violations");
                Check(solution.Slot.All(s => s != disabled), $"slot {disabled} disabled but used");
            }

            // 갤럽은 3키로 정박을 주손 검지에 고정한다.
            foreach (double unit in new[] { 0.04, 0.05, 0.06 })
            {
                var events = new PressEvent[240];
                double time = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    int part = i % 3;
                    events[i] = new PressEvent(time, i / 3 + (part == 0 ? 0 : part == 1 ? 0.5 : 0.75));
                    time += part == 0 ? 2 * unit : unit;
                }

                Layout layout = Layout8();
                string actual = Labels(layout, FingeringSolver.Solve(events, layout, new SolverOptions()).Slot);
                string heads = string.Concat(actual.Where((c, i) => i % 3 == 0));
                Check(heads.Distinct().Count() == 1 && heads[0] == 'j', $"gallop unit {unit}: heads {heads.Substring(0, 12)}, seq {actual.Substring(0, 12)}");
            }

            // 점음표 쌍은 3키로 가면 정박이 세 손가락을 돈다. 4키로 손 짝을 지켜야 한다.
            foreach (var (longGap, shortGap) in new[] { (0.0825, 0.0275), (0.066, 0.044), (0.055, 0.045), (0.09375, 0.03125) })
            {
                var events = new PressEvent[96];
                double time = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    events[i] = new PressEvent(time, i / 2 + (i % 2 == 0 ? 0 : 0.75));
                    time += i % 2 == 0 ? longGap : shortGap;
                }

                Layout layout = Layout8();
                string actual = Labels(layout, FingeringSolver.Solve(events, layout, new SolverOptions()).Slot);
                Check(FollowsCycle(actual.Substring(8, 80), "jkfd"), $"dotted {longGap}/{shortGap}: {actual.Substring(0, 16)}");
            }

            // 셔플도 두 음 단위 반복이라 4키가 맞다. 박의 2/3 위치에 있어도 3연음으로 치지 않는다.
            foreach (double unit in new[] { 0.030, 0.035, 0.040 })
            {
                var events = new PressEvent[96];
                double time = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    events[i] = new PressEvent(time, i / 2 + (i % 2 == 0 ? 0 : 2.0 / 3));
                    time += i % 2 == 0 ? 2 * unit : unit;
                }

                Layout layout = Layout8();
                string actual = Labels(layout, FingeringSolver.Solve(events, layout, new SolverOptions()).Slot);
                Check(FollowsCycle(actual.Substring(8, 80), "jkfd"), $"shuffle unit {unit}: {actual.Substring(0, 16)}");
            }

            // 게임처럼 시간을 누적하면 Floor의 정확한 배수에서 오차가 난다. 그래도 tier가 흔들리면 안 된다.
            foreach (var (gap, cycle) in new[] { (0.075, "jf"), (0.0375, "jkfd") })
            {
                var events = new PressEvent[400];
                double time = 1000.1;
                for (int i = 0; i < events.Length; i++)
                {
                    events[i] = new PressEvent(time, i / 4.0);
                    time += gap;
                }

                Layout layout = Layout8();
                Solution solution = FingeringSolver.Solve(events, layout, new SolverOptions { Floor = 0.150 });
                string actual = Labels(layout, solution.Slot);
                Check(FollowsCycle(actual, cycle), $"floor multiple gap {gap}: {actual.Substring(0, 16)}");
                Check(solution.FloorViolations == 0, $"floor multiple gap {gap}: {solution.FloorViolations} violations");
            }

            // 롱노트 중에 다시 누르면 위반으로 센다. 키가 하나뿐이면 피할 수 없다.
            var overlap = new[] { new PressEvent(0, 0, 1.0), new PressEvent(0.5, 1), new PressEvent(2, 2) };
            Check(FingeringSolver.Solve(overlap, Layouts.Build(Codes(1)), new SolverOptions()).FloorViolations == 1, "press during hold is a violation");

            // 긴 틈 기준은 0.4초다. 넘으면 시작 위치로 돌아가고 못 넘으면 사이클을 이어 간다.
            foreach (var (pause, expected) in new[] { (0.39, 'k'), (0.41, 'j') })
            {
                var events = new PressEvent[13];
                double time = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    if (i == 5) time += pause - 0.05;
                    // 뒤 구간은 정박을 비껴 둔다. 정박이 있으면 박 맞춤이 시작 위치 선택을 함께 좌우해 기준값만 볼 수 없다.
                    events[i] = new PressEvent(time, i < 5 ? i / 4.0 : (i - 5) / 4.0 + 0.12);
                    time += 0.05;
                }

                Layout layout = Layout4();
                var options = new SolverOptions { MaxKeys = 4 };
                string actual = Labels(layout, FingeringSolver.Solve(events, layout, options).Slot);
                Check(actual[5] == expected, $"pause {pause}: {actual}");
            }
        }

        private static void TestBurstAnchor()
        {
            foreach (RollStyle style in Styles)
            {
                foreach (Hand hand in Hands)
                {
                    string cycle = CycleTable[(style, hand, 4)];
                    string anchors = $"{cycle[0]}{cycle[2]}";

                    // 앞 구간 길이를 바꿔 끝나는 위상을 모두 거치게 한다. 재개 지점은 일부러 정박을 피한다.
                    for (int leading = 13; leading <= 20; leading++)
                    {
                        var events = new List<PressEvent>();
                        for (int i = 0; i < leading; i++) events.Add(new PressEvent(i * 0.035, i / 4.0));
                        double resume = leading * 0.035 + 2.0;
                        for (int i = 0; i < 16; i++) events.Add(new PressEvent(resume + i * 0.035, 100.3 + i / 4.0));

                        Layout layout = Layout4();
                        var options = new SolverOptions { Style = style, MainHand = hand };
                        string labels = Labels(layout, FingeringSolver.Solve(events.ToArray(), layout, options).Slot);
                        Check(anchors.IndexOf(labels[leading]) >= 0,
                            $"{style}/{hand} leading {leading}: burst starts on {labels[leading]}, anchors {anchors}");
                    }
                }
            }
        }

        private static void TestTriplets()
        {
            foreach (RollStyle style in Styles)
            {
                foreach (Hand hand in Hands)
                {
                    foreach (TripletSplit split in new[] { TripletSplit.MainTwoFingers, TripletSplit.OffTwoFingers })
                    {
                        var options = new SolverOptions { Style = style, MainHand = hand, TripletSplit = split };
                        Layout layout = Layout8();
                        string name = $"{style}/{hand}/{split}";

                        // 50ms면 3키의 같은 손가락 간격이 150ms라 3키로 충분하다.
                        string actual = Labels(layout, FingeringSolver.Solve(Stream(60, 0.050, 3), layout, options).Slot);
                        string expected = Repeat(TripletTable[(style, hand, split)], 60);
                        Check(actual == expected, $"{name}: expected {expected.Substring(0, 9)} got {actual.Substring(0, 9)}");

                        // 속도 전 구간에서 3연음은 3키 또는 6키이고 정박은 앵커에 떨어져야 한다.
                        for (int ms = 22; ms <= 64; ms++) CheckTripletSweep(layout, options, ms, name);
                    }
                }
            }
        }

        private static void CheckTripletSweep(Layout layout, SolverOptions options, int ms, string name)
        {
            PressEvent[] events = Stream(120, ms / 1000.0, 3);
            byte[] slots = FingeringSolver.Solve(events, layout, options).Slot;
            int distinct = Distinct(slots, 0, slots.Length);
            int expectedK = ms * 3 >= 130 ? 3 : 6;
            Check(distinct == expectedK, $"{name} {ms}ms: K={distinct}, expected {expectedK}");

            Tier tier = StyleCycle.BuildTiers(layout, options).First(t => t.K == expectedK);
            byte mainStart = tier.Cycle[0];
            byte offStart = tier.Cycle[tier.OffStart];
            for (int i = 0; i < slots.Length; i += 3)
            {
                Check(slots[i] == mainStart || slots[i] == offStart, $"{name} {ms}ms: on-beat note {i} not on anchor");
            }
        }

        private static void TestHolds()
        {
            // 긴 홀드 중에는 그 슬롯을 쓰지 못한다.
            var events = new List<PressEvent> { new PressEvent(0, 0, 3.0) };
            for (int i = 1; i <= 19; i++) events.Add(new PressEvent(i * 0.15, i / 2.0));
            for (int i = 20; i <= 40; i++) events.Add(new PressEvent(i * 0.15, i / 2.0));
            PressEvent[] array = events.ToArray();

            foreach (RollStyle style in Styles)
            {
                Layout layout = Layout8();
                byte[] slots = FingeringSolver.Solve(array, layout, new SolverOptions { Style = style }).Slot;
                for (int i = 1; i <= 19; i++)
                {
                    Check(slots[i] != slots[0], $"{style}: note {i} reuses held slot");
                }
            }

            // 슬롯이 둘뿐이면 홀드 동안 나머지 전부가 다른 슬롯으로 가야 한다.
            Layout two = Layouts.Build(new[] { 102, 106 }, new[] { "f", "j" });
            var twoEvents = new[]
            {
                new PressEvent(0, 0, 2.0), new PressEvent(0.5, 1), new PressEvent(1.0, 2), new PressEvent(1.5, 3),
            };
            byte[] twoSlots = FingeringSolver.Solve(twoEvents, two, new SolverOptions()).Slot;
            Check(twoSlots.Skip(1).All(s => s != twoSlots[0]), "two-slot hold keeps slot busy");

            // 홀드 뒤 홀드: 이전 키를 떼는 순간 새 키를 누른다. 같은 키면 간격 0이므로 반드시 달라야 한다.
            var tiles = new List<TileInfo> { Tile(0), Tile(1) };
            for (int i = 2; i < 12; i++) tiles.Add(Tile(i * 0.5, hold: 0));
            tiles.Add(Tile(6.5));
            TilePlan plan = TileClassifier.Build(tiles.ToArray(), 1, false);
            byte[] planSlots = FingeringSolver.Solve(plan.Events, Layout4(), new SolverOptions()).Slot;
            Check(plan.Events.Length == 11, $"hold chain events {plan.Events.Length}");
            for (int i = 2; i < planSlots.Length; i++)
            {
                Check(planSlots[i] != planSlots[i - 1], $"hold-after-hold event {i} reuses released slot");
                Check(plan.Events[i - 1].ReleaseTime == plan.Events[i].Time, $"hold {i - 1} releases at next press");
            }

            Check(FingeringSolver.Solve(plan.Events, Layout4(), new SolverOptions()).FloorViolations == 0, "hold chain clean");
        }

        private static void TestTempoChange()
        {
            foreach (RollStyle style in Styles)
            {
                var events = new List<PressEvent>();
                double time = 0;
                double beat = 0;
                void Add(int count, double gap, double notesPerBeat)
                {
                    for (int i = 0; i < count; i++)
                    {
                        events.Add(new PressEvent(time, beat));
                        time += gap;
                        beat += 1 / notesPerBeat;
                    }
                }

                Add(40, 0.200, 2);
                Add(200, 0.025, 4);
                Add(80, 0.200, 2);

                Layout layout = Layout8();
                PressEvent[] array = events.ToArray();
                Solution solution = FingeringSolver.Solve(array, layout, new SolverOptions { Style = style });
                Check(Distinct(solution.Slot, 0, 30) <= 2, $"{style}: slow start uses 2 keys");
                Check(Distinct(solution.Slot, 60, 220) >= 6, $"{style}: fast section uses at least 6 keys");
                Check(Distinct(solution.Slot, 260, 320) <= 2, $"{style}: drops back after fast section");
                Check(solution.FloorViolations == 0, $"{style}: violations {solution.FloorViolations}");
            }
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

        private static int[] TierSizes(Layout layout, SolverOptions options = null)
        {
            return StyleCycle.BuildTiers(layout, options ?? new SolverOptions()).Select(t => t.K).ToArray();
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

            // Tier 구성.
            Check(TierSizes(Layout16()).SequenceEqual(new[] { 2, 3, 4, 6, 8, 10, 12, 16 }), "16-key tiers");
            Check(TierSizes(Layout8()).SequenceEqual(new[] { 2, 3, 4, 6, 8 }), "8-key tiers");
            Check(TierSizes(Layout4()).SequenceEqual(new[] { 2, 3, 4 }), "4-key tiers");
            Check(TierSizes(Layout16(), new SolverOptions { MaxKeys = 6 }).SequenceEqual(new[] { 2, 3, 4, 6 }), "max 6");
            Check(TierSizes(Layout16(), new SolverOptions { MaxKeys = 4 }).SequenceEqual(new[] { 2, 3, 4 }), "max 4");
            Check(TierSizes(Layout16(), new SolverOptions { MaxKeys = 2 }).SequenceEqual(new[] { 2 }), "max 2");
            Check(StyleCycle.BuildTiers(Layout16(), new SolverOptions()).Sum(t => t.K) < 255, "states fit a byte");

            // 16키 안쪽에서 바깥쪽 순서: Thumb2, Thumb1, Index ... Palm2. 안밖이면 오른손이 Thumb2부터 시작한다.
            Layout order = Layouts.Build(Jipper16, jipperLabels);
            Tier full = StyleCycle.BuildTiers(order, new SolverOptions()).Last();
            string fullCycle = string.Join(" ", full.Cycle.Select(s => order.Slots[s].Label));
            Check(fullCycle == ". VK21 P = Back \\ RShift Return C Space E 2 1 Tab LShift Caps", $"16-key cycle: {fullCycle}");
            Check(full.OffStart == 8, "16-key off group start");

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

            // 끈 슬롯은 어떤 tier에도 들어가지 않는다. 8키에서 하나 끄면 7이라 8 tier가 사라진다.
            Layout noPinky = Layout8();
            Layouts.ApplyOverrides(noPinky, new[]
            {
                new RoleOverride { KeyCount = 8, Slot = 7, Hand = Hand.Right, Finger = Finger.Pinky, Enabled = false },
            });
            Check(TierSizes(noPinky).SequenceEqual(new[] { 2, 3, 4, 6, 7 }), $"disabled tiers {string.Join(",", TierSizes(noPinky))}");
            byte[] noPinkySlots = FingeringSolver.Solve(Stream(200, 0.020, 4), noPinky, new SolverOptions()).Slot;
            Check(noPinkySlots.All(s => s != 7), "disabled slot never used");

            // 역할이 겹치면 앞 슬롯만 쓴다.
            Layout duplicate = Layout4();
            Layouts.ApplyOverrides(duplicate, new[]
            {
                new RoleOverride { KeyCount = 4, Slot = 3, Hand = Hand.Right, Finger = Finger.Index },
            });
            Check(TierSizes(duplicate).SequenceEqual(new[] { 2, 3 }), "duplicate role collapses");
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
            Check(TierSizes(oneHand).SequenceEqual(new[] { 2, 3, 4 }), "one hand tiers");

            Layout single = Layouts.Build(Codes(1));
            Check(TierSizes(single).SequenceEqual(new[] { 1 }), "single key tier");
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
            Check(Checksum(first.Slot) == 0xcb812ce6, "checksum changed between runs");

            PressEvent[] million = MixedStream(1000000, 11);
            var watch = Stopwatch.StartNew();
            Solution big = FingeringSolver.Solve(million, Layout16(), new SolverOptions());
            watch.Stop();
            Console.WriteLine($"  1,000,000 events, 16 keys: {watch.Elapsed.TotalSeconds:F2} s, violations {big.FloorViolations}");
            Check(watch.Elapsed.TotalSeconds < 20, $"performance target missed: {watch.Elapsed.TotalSeconds:F2} s");
            Check(big.FloorViolations == RecountViolations(million, big.Slot, 0.130 * 0.75), "million recount");
            // 리셋 부채의 대가로 위반이 드물게 생긴다. 이 정도를 넘으면 절충이 깨진 것이다.
            Check(big.FloorViolations <= 100, $"million violations {big.FloorViolations}");
        }

        // 느린 구간, 빠른 구간, 3연음, 멈춤을 섞어 tier 전환까지 포함한 최악에 가까운 입력을 만든다.
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
            // 4키 기반은 2키 기반이 2키로 충분한 속도에서 4키를 쓴다.
            Layout layout = Layout8();
            PressEvent[] events = Stream(64, 0.100, 4);
            string base2 = Labels(layout, FingeringSolver.Solve(events, layout, new SolverOptions()).Slot);
            string base4 = Labels(layout, FingeringSolver.Solve(events, layout, new SolverOptions { Base = 4 }).Slot);
            Check(base2 == Repeat("jf", 64), $"base 2: {base2.Substring(0, 8)}");
            Check(base4 == Repeat("jkfd", 64), $"base 4: {base4.Substring(0, 8)}");

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
            keys.Press(0, 0, false, 0.1f);
            Step(1.079f);
            Check(log.Count == 1 && log[0] == (0, true), "tap stays lit before 0.8 * gap");
            Step(1.081f);
            Check(log.Count == 2 && log[1] == (0, false), "tap goes dark after 0.8 * gap");
            Check(keys.TapsDrained, "no tap left");

            // 다음 사용이 없으면 상한 120 ms, 간격이 짧으면 하한 50 ms에서 한 프레임을 뺀다.
            log.Clear();
            keys.SetClock(2f, ++frame, Dt);
            keys.Press(0, 1, false, float.PositiveInfinity);
            Step(2.119f);
            Check(log.Count == 1, "unused slot stays lit below the 120 ms cap");
            Step(2.121f);
            Check(log.Count == 2, "unused slot goes dark at the 120 ms cap");

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
