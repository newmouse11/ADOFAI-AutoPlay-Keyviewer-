using System;
using System.Collections.Generic;

namespace AutoFingering.Core
{
    public static class TileClassifier
    {
        // 미드스핀 규칙: 디컴파일한 게임 코드에서 확인한 내용 (D = 릴리스 R150, F = 릴리스 R145의 줄 번호)
        //
        // 1. 미드스핀 M은 바로 다음 타일 T와 entryTime이 같다. M의 시간 길이가 0으로 계산된다.
        //    (scrLevelMaker.cs D:641, F:743. 일시정지는 미드스핀에 걸리지 않는다: scnGame.cs D:1156, F:1118.)
        // 2. 실제 키 입력 하나가 M 착지와 T 착지를 모두 처리한다. Hit이 M에 착지하면 keyTimes를 하나 더 넣고
        //    midspinInfiniteMargin을 켜서 UpdateHoldKeys가 그 자리에서 Hit을 한 번 더 호출한다.
        //    (scrPlayer.cs Hit D:794-798, F:822-826 / UpdateHoldKeys D:723-731, F:725-735)
        //    즉 키가 내려가는 물리적 순간은 체인의 첫 착지다.
        // 3. 게임이 "입력 1회"를 집계할 때는 뒤 타일 쪽으로 센다. PlayerHitFloors는 midSpin을 빼고(scrLevelMaker.cs D:94, D 전용),
        //    히트음은 scrConductor.cs D/F:471의 !midSpin으로, OttoBlink는 scrPlayer.cs D:774, F:802로 M을 뺀다.
        //    이것은 중복을 막는 장부일 뿐 시간은 같으므로, "M을 건너뛰고 다음 타일이 누른다"와
        //    "첫 착지에서 누른다"는 평범한 경우 관찰 차이가 없다.
        // 4. 그래도 첫 착지로 정한 이유: 자동 타일이 섞이면 결과가 갈린다. 입력 필요 여부는 다음 타일의 auto로 정해진다
        //    (_nextTileIsAuto, scrPlayer.cs D:159, F:150). M이 수동이고 T가 auto면 입력이 필요하고,
        //    M이 auto이고 T가 수동이면 OttoHoldHit 루프나 Hit이 넣은 keyTimes가 T까지 처리하므로 입력이 필요 없다.
        //    첫 착지 규칙은 두 경우 모두 추가 분기 없이 맞는다.
        // 5. 그 입력이 시작하는 홀드는 체인 끝 타일의 것이다. 게임도 미드스핀을 건너뛰어 다음 홀드를 찾고
        //    (scrPlayer.cs D:419-426, F:415-419) 두 번째 Hit 뒤에 holdKeys를 다시 잡는다. 그래서 앞을 내다봐야 한다.
        // 6. 홀드 H 뒤에 미드스핀이 오면 H를 떼는 것이 M과 T 착지를 처리하므로 새 입력이 없다
        //    (scrPlayer.cs D:602-606, F:591-595, scrConductor.cs D/F:463). 단 체인 끝이 다시 홀드면 떼기만으로는
        //    넘어가지 않고 새 입력이 필요하다(scrPlayer.cs D:602,708, F:591,704). 이 입력만 예외로 T 착지에서 낸다.
        //    게임의 집계는 한 칸만 되돌아보지만 실제 입력 흐름은 체인 길이와 무관하므로 끝까지 되돌아본다.
        //
        // Beat 단위 (scrLevelMaker.cs D:659-667, F:765-773, scrFloor.entryBeat):
        // entryBeat[0] = -1, entryBeat[1] = 0, entryBeat[j+1] = entryBeat[j] + angleLength[j] / PI + extraBeats[j].
        // 반 바퀴(PI)가 1박이다(scrMisc.GetTimeBetweenAngles D:396, F:335). 일시정지 뒤 정박이 밀리지 않도록
        // TileInfo.ExtraBeats를 더한다. entryBeat를 그대로 읽지 않는 이유는
        // 게임 루프가 마지막 타일에 값을 넣지 않기 때문이다(j < Count - 1).
        public static TilePlan Build(TileInfo[] tiles, double pitch, bool multitapHitOnce)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (!(pitch > 0)) throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "pitch는 0보다 커야 함");

            int n = tiles.Length;
            var plan = new TilePlan
            {
                FirstEvent = new int[n],
                EventCount = new int[n],
                ReleaseEvent = new int[n],
            };
            for (int i = 0; i < n; i++) plan.ReleaseEvent[i] = -1;

            var events = new List<PressEvent>(n);
            int pendingEvent = -1;
            int pendingTile = -1;
            double beat = 0;

            // 0번 타일은 시작 위치라 착지가 없다. 첫 MoveToNextFloor는 1번 타일이다.
            for (int i = 1; i < n; i++)
            {
                if (i >= 2) beat += tiles[i - 1].AngleLength / Math.PI + tiles[i - 1].ExtraBeats;
                double time = tiles[i].EntryTime / pitch;

                // 떼기를 누르기보다 먼저 처리한다. 다음 타일이 auto여도 이미 누른 키는 떼야 불이 남지 않는다.
                if (pendingEvent >= 0 && i == pendingTile + 1)
                {
                    PressEvent held = events[pendingEvent];
                    held.ReleaseTime = time;
                    events[pendingEvent] = held;
                    plan.ReleaseEvent[i] = pendingEvent;
                    pendingEvent = -1;
                }

                plan.FirstEvent[i] = events.Count;
                // 앞선 미드스핀의 입력이 이미 이 타일의 홀드를 잡았으면 새 입력이 없다.
                int count = pendingEvent >= 0 && pendingTile == i ? 0 : PressCount(tiles, i, multitapHitOnce);
                if (count == 0) continue;

                int holdTile = HoldStartedBy(tiles, i);
                for (int tap = 0; tap < count; tap++) events.Add(new PressEvent(time, beat));
                plan.EventCount[i] = count;

                if (holdTile >= 0)
                {
                    // 다중탭은 마지막 입력에서 타일을 넘어가므로(scrPlanet.cs D:885, F:869) 그 키가 홀드를 잡는다.
                    pendingEvent = events.Count - 1;
                    pendingTile = holdTile;
                }
            }

            plan.Events = events.ToArray();
            return plan;
        }

        private static int PressCount(TileInfo[] tiles, int i, bool multitapHitOnce)
        {
            TileInfo tile = tiles[i];
            TileInfo previous = tiles[i - 1];
            if (tile.Auto || (previous.Auto && previous.HoldLength > -1)) return 0;

            int taps = multitapHitOnce ? 1 : Math.Max(1, tile.TapsNeeded);
            if (FollowsHold(tiles, i))
            {
                if (tile.HoldLength > -1) return taps;

                // 떼기의 Hit이 첫 탭으로 세어져 나머지 탭만 더 눌러야 한다(scrPlayer.cs D:602-606, F:591-595,
                // scrPlanet.cs D:880-893, F:864-877). 미드스핀을 거치면 무한 판정이 탭을 모두 처리해 입력이 없다.
                return previous.HoldLength > -1 ? taps - 1 : 0;
            }

            // 0번 타일이 미드스핀이면 체인의 첫 착지가 없으므로 1번 타일이 입력을 받는다.
            return !previous.MidSpin || i == 1 ? taps : 0;
        }

        // 바로 앞까지 미드스핀만 거슬러 올라가 그 앞이 홀드인지 본다.
        private static bool FollowsHold(TileInfo[] tiles, int i)
        {
            int q = i - 1;
            while (q > 0 && tiles[q].MidSpin && tiles[q].HoldLength <= -1) q--;
            return tiles[q].HoldLength > -1;
        }

        // 자동 홀드는 알아서 진행되므로 키를 계속 누를 필요가 없어 탭으로 처리한다(scrPlayer.cs D:498, D:631, F:487).
        private static int HoldStartedBy(TileInfo[] tiles, int i)
        {
            int h = i;
            while (tiles[h].MidSpin && tiles[h].HoldLength <= -1 && h + 1 < tiles.Length) h++;
            return tiles[h].HoldLength > -1 && !tiles[h].Auto ? h : -1;
        }
    }
}
