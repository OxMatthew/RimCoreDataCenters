using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Every tunable number for the data contract system, in one XML-editable def since contracts
    /// are not tied to a single building.
    /// </summary>
    public class DataContractSettingsDef : Def
    {
        /// <summary>Days between a contract resolving (fulfilled, expired or declined) and the next offer.</summary>
        public float offerCooldownMinDays = 5f;
        public float offerCooldownMaxDays = 10f;

        /// <summary>How long an unanswered offer letter stays valid before it is withdrawn.</summary>
        public float offerTimeoutDays = 3f;

        /// <summary>How many cartridges a contract asks for.</summary>
        public int quantityMin = 15;
        public int quantityMax = 35;

        /// <summary>The premium paid over the cartridges' current market value (0.3 = 30% more).</summary>
        public float bonusPercentMin = 0.20f;
        public float bonusPercentMax = 0.40f;

        /// <summary>Days given to deliver after accepting.</summary>
        public float deadlineMinDays = 4f;
        public float deadlineMaxDays = 8f;
    }
}
