using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Put this on a data cartridge ThingDef to give stockpiled copies of it a small, capped, map-wide bonus
    /// while they sit in storage (read by <see cref="MapComponent_DataCenterNetwork.StoredCartridgeBonus"/>).
    /// Only one of the two bonus pairs should be set per def; whichever is non-zero is the one that applies.
    /// </summary>
    public class CartridgeSpecialEffect : DefModExtension
    {
        /// <summary>Research Speed bonus added per cartridge of this type stored on the map.</summary>
        public float researchBonusPerStored;

        /// <summary>The most the stored-cartridge research bonus can add, regardless of how many are stored.</summary>
        public float researchBonusCapStored;

        /// <summary>Immunity Gain Speed bonus added per cartridge of this type stored on the map.</summary>
        public float immunityBonusPerStored;

        /// <summary>The most the stored-cartridge immunity bonus can add, regardless of how many are stored.</summary>
        public float immunityBonusCapStored;
    }
}
