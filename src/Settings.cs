using System.Collections.Generic;
using UnityModManagerNet;

namespace AutoFingering
{
    using AutoFingering.Core;

    public enum UiLanguage { Game, Korean, English }

    // UMM이 XmlSerializer로 저장하므로 public 필드와 기본 생성자를 유지한다.
    public sealed class Settings : UnityModManager.ModSettings
    {
        public const int MinFloorMs = 60;
        public const int MaxFloorMs = 250;

        public UiLanguage Language = UiLanguage.Game;
        public RollStyle Style = RollStyle.Outward;
        public Hand MainHand = Hand.Right;
        public int Base = 2;
        public int MaxKeys = SolverOptions.AllKeys;
        public int FloorMs = 130;
        public TripletSplit Split = TripletSplit.MainTwoFingers;
        public List<RoleOverride> RoleOverrides = new List<RoleOverride>();

        public int BuiltInKeys = 8;
        public float BuiltInX = 0.5f;
        public float BuiltInY = 0.08f;
        public float BuiltInScale = 1f;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public SolverOptions ToOptions()
        {
            // 손으로 고친 Settings.xml도 올 수 있어 범위를 여기서 한 번 더 막는다.
            int floor = FloorMs < MinFloorMs ? MinFloorMs : FloorMs > MaxFloorMs ? MaxFloorMs : FloorMs;
            return new SolverOptions
            {
                Style = Style,
                MainHand = MainHand,
                Base = Base,
                MaxKeys = MaxKeys,
                Floor = floor / 1000.0,
                TripletSplit = Split,
            };
        }
    }
}
