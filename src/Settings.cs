using System;
using System.Collections.Generic;
using UnityModManagerNet;

namespace AutoFingering
{
    using AutoFingering.Core;

    public enum UiLanguage { Game, Korean, English }

    // UMM이 XmlSerializer로 저장하므로 public 필드와 기본 생성자를 유지한다.
    public sealed class Settings : UnityModManager.ModSettings
    {
        public const double MinFloorMs = 60;
        public const double MaxFloorMs = 250;

        // 번갈아 치면 한 손가락은 두 타마다 한 번 치므로, 같은 손가락 간격은 BPM의 두 박이다.
        private const double TwoBeatsMsAtOneBpm = 120000;

        public const double MinTwoKeyBpm = TwoBeatsMsAtOneBpm / MaxFloorMs;
        public const double MaxTwoKeyBpm = TwoBeatsMsAtOneBpm / MinFloorMs;

        public UiLanguage Language = UiLanguage.Game;
        public RollStyle Style = RollStyle.Outward;
        public Hand MainHand = Hand.Right;
        public int Base = 2;
        public int MaxKeys = SolverOptions.AllKeys;

        // 화면에는 2키 최대 속도로 보여 주지만 저장은 이 값으로 한다. 1.0의 Settings.xml을 그대로 읽기 위함이다.
        public double FloorMs = 130;
        public TripletSplit Split = TripletSplit.MainTwoFingers;
        public List<RoleOverride> RoleOverrides = new List<RoleOverride>();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        // 프로퍼티로 두면 XmlSerializer가 저장 항목으로 삼는다.
        public double GetTwoKeyBpm()
        {
            return TwoBeatsMsAtOneBpm / ClampedFloorMs();
        }

        public void SetTwoKeyBpm(double bpm)
        {
            FloorMs = TwoBeatsMsAtOneBpm / Math.Min(Math.Max(bpm, MinTwoKeyBpm), MaxTwoKeyBpm);
        }

        public SolverOptions ToOptions()
        {
            return new SolverOptions
            {
                Style = Style,
                MainHand = MainHand,
                Base = Base,
                MaxKeys = MaxKeys,
                Floor = ClampedFloorMs() / 1000.0,
                TripletSplit = Split,
            };
        }

        // 손으로 고친 Settings.xml도 올 수 있어 범위를 여기서 한 번 더 막는다.
        private double ClampedFloorMs()
        {
            return Math.Min(Math.Max(FloorMs, MinFloorMs), MaxFloorMs);
        }
    }
}
