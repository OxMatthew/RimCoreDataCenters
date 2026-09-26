using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>References to this mod's defs, resolved by RimWorld after defs are loaded.</summary>
    [DefOf]
    public static class RcdcDefOf
    {
        public static ThingDef RCDC_ServerRack;
        public static ThingDef RCDC_NetworkCore;
        public static ThingDef RCDC_PrecisionCoolingUnit;
        public static ThingDef RCDC_UpsUnit;
        public static ThingDef RCDC_OperationsConsole;
        public static ThingDef RCDC_DataCartridge;
        public static ThingDef RCDC_DataCartridge_Research;
        public static ThingDef RCDC_DataCartridge_Financial;
        public static ThingDef RCDC_DataCartridge_Medical;

        public static ResearchProjectDef RCDC_DataClassification;

        public static IncidentDef RCDC_Espionage;
        public static LetterDef RCDC_EspionageLetter;
        public static SoundDef RCDC_EspionageAlert;

        public static JobDef RCDC_DataCenterOperations;
        public static JobDef RCDC_ServiceRack;

        public static WorkTypeDef RCDC_DataCenter;
        public static ResearchProjectDef RCDC_DataCenterInfrastructure;

        public static ThingDef RCDC_BiometricDoor;
        public static ThingDef RCDC_MetalDetectorGate;
        public static ThingDef RCDC_AiCore;

        public static LetterDef RCDC_AiLetter;
        public static SoundDef RCDC_AiChime;

        public static SoundDef RCDC_CartridgeReady;
        public static SoundDef RCDC_RackAlarm;
        public static SoundDef RCDC_AccessDenied;

        public static LetterDef RCDC_ContractLetter;
        public static DataContractSettingsDef RCDC_DataContractSettings;

        public static MarketDynamicsSettingsDef RCDC_MarketDynamicsSettings;

        static RcdcDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RcdcDefOf));
        }

        private static HashSet<ThingDef> cartridgeDefs;

        /// <summary>True for the base data cartridge and every specialized variant.</summary>
        public static bool IsDataCartridge(ThingDef def)
        {
            if (cartridgeDefs == null)
            {
                cartridgeDefs = new HashSet<ThingDef> { RCDC_DataCartridge, RCDC_DataCartridge_Research, RCDC_DataCartridge_Financial, RCDC_DataCartridge_Medical };
            }
            return def != null && cartridgeDefs.Contains(def);
        }
    }
}
