using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Every tunable number for cartridge price drift and market events, in one XML-editable def since
    /// this system is not tied to a single building.
    /// </summary>
    public class MarketDynamicsSettingsDef : Def
    {
        /// <summary>How often each cartridge type's baseline price takes a small random step.</summary>
        public float driftIntervalMinDays = 2f;
        public float driftIntervalMaxDays = 4f;

        /// <summary>The largest single step a price can drift, up or down (0.05 = 5%).</summary>
        public float driftStep = 0.05f;

        /// <summary>The band the drifting baseline is clamped to (0.85 = as low as 85% of base value).</summary>
        public float driftMultiplierMin = 0.85f;
        public float driftMultiplierMax = 1.20f;

        /// <summary>How often a new market event is rolled for, while none is active.</summary>
        public float eventCheckIntervalMinDays = 4f;
        public float eventCheckIntervalMaxDays = 8f;

        /// <summary>Chance a market event actually starts each time one is rolled for.</summary>
        public float eventChance = 0.35f;

        /// <summary>How long a market event lasts once it starts.</summary>
        public float eventDurationMinDays = 4f;
        public float eventDurationMaxDays = 7f;

        /// <summary>Price multiplier range for a Rival Buyer or Shortage event (a price spike).</summary>
        public float eventBonusMin = 1.15f;
        public float eventBonusMax = 1.35f;

        /// <summary>Price multiplier range for a Market Glut event (a price dip).</summary>
        public float eventMalusMin = 0.70f;
        public float eventMalusMax = 0.85f;
    }
}
