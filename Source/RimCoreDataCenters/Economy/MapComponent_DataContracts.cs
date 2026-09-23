using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Periodically offers the player a data contract: deliver N cartridges of a given type within a
    /// deadline for a silver bonus over market value. One offer or one active contract at a time, per
    /// map. Reuses vanilla trade-drop and letter machinery only - no custom UI beyond the offer letter.
    /// </summary>
    public class MapComponent_DataContracts : MapComponent
    {
        private const int CheckIntervalTicks = 200;
        private const int DayTicks = 60000;

        private bool hasOffer;
        private ThingDef offerDef;
        private int offerQuantity;
        private float offerBonusPercent;
        private int offerExpireTick;

        private bool hasActive;
        private ThingDef activeDef;
        private int activeQuantity;
        private int activeRewardSilver;
        private int activeDeadlineTick;

        private int nextOfferTick = -1;

        public MapComponent_DataContracts(Map map) : base(map)
        {
        }

        public static MapComponent_DataContracts For(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_DataContracts>();
        }

        public bool HasOffer { get { return hasOffer; } }
        public bool HasActiveContract { get { return hasActive; } }
        public ThingDef OfferDef { get { return offerDef; } }
        public int OfferQuantity { get { return offerQuantity; } }
        public float OfferBonusPercent { get { return offerBonusPercent; } }
        public ThingDef ActiveDef { get { return activeDef; } }
        public int ActiveQuantity { get { return activeQuantity; } }
        public int ActiveRewardSilver { get { return activeRewardSilver; } }
        public int ActiveDeadlineTick { get { return activeDeadlineTick; } }

        private DataContractSettingsDef Settings
        {
            get { return RcdcDefOf.RCDC_DataContractSettings; }
        }

        private static int Now
        {
            get { return Find.TickManager != null ? Find.TickManager.TicksGame : 0; }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Now % CheckIntervalTicks != 0)
            {
                return;
            }
            if (nextOfferTick < 0)
            {
                ScheduleNextOffer();
            }
            if (hasOffer)
            {
                if (Now >= offerExpireTick)
                {
                    WithdrawOffer();
                }
                return;
            }
            if (hasActive)
            {
                if (Now >= activeDeadlineTick)
                {
                    ResolveActive();
                }
                return;
            }
            if (Now >= nextOfferTick && Eligible())
            {
                MakeOffer();
            }
        }

        private bool Eligible()
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            return network != null && network.Racks.Count > 0;
        }

        private void ScheduleNextOffer()
        {
            DataContractSettingsDef settings = Settings;
            float min = settings != null ? settings.offerCooldownMinDays : 5f;
            float max = settings != null ? settings.offerCooldownMaxDays : 10f;
            nextOfferTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(min, max) * DayTicks);
        }

        private void MakeOffer()
        {
            DataContractSettingsDef settings = Settings;
            if (settings == null)
            {
                return;
            }
            List<ThingDef> options = RcdcDefOf.RCDC_DataClassification != null && RcdcDefOf.RCDC_DataClassification.IsFinished
                ? new List<ThingDef> { RcdcDefOf.RCDC_DataCartridge, RcdcDefOf.RCDC_DataCartridge_Research, RcdcDefOf.RCDC_DataCartridge_Financial, RcdcDefOf.RCDC_DataCartridge_Medical }
                : new List<ThingDef> { RcdcDefOf.RCDC_DataCartridge };

            offerDef = options[Rand.Range(0, options.Count)];
            offerQuantity = Rand.RangeInclusive(settings.quantityMin, settings.quantityMax);
            offerBonusPercent = Rand.Range(settings.bonusPercentMin, settings.bonusPercentMax);
            offerExpireTick = Now + UnityEngine.Mathf.RoundToInt(settings.offerTimeoutDays * DayTicks);
            hasOffer = true;

            ChoiceLetter_DataContract letter = ChoiceLetter_DataContract.Create(map, offerDef, offerQuantity, offerBonusPercent);
            Find.LetterStack.ReceiveLetter(letter);
        }

        private void WithdrawOffer()
        {
            Letter existing = Find.LetterStack.LettersListForReading.FirstOrDefault(l => l is ChoiceLetter_DataContract);
            if (existing != null)
            {
                Find.LetterStack.RemoveLetter(existing);
            }
            hasOffer = false;
            offerDef = null;
            ScheduleNextOffer();
        }

        public void AcceptOffer()
        {
            if (!hasOffer)
            {
                return;
            }
            DataContractSettingsDef settings = Settings;
            float unitValue = offerDef.GetStatValueAbstract(StatDefOf.MarketValue);
            activeDef = offerDef;
            activeQuantity = offerQuantity;
            activeRewardSilver = UnityEngine.Mathf.RoundToInt(offerQuantity * unitValue * (1f + offerBonusPercent));
            float minDays = settings != null ? settings.deadlineMinDays : 4f;
            float maxDays = settings != null ? settings.deadlineMaxDays : 8f;
            activeDeadlineTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(minDays, maxDays) * DayTicks);
            hasActive = true;
            hasOffer = false;
            offerDef = null;
            Messages.Message("RCDC_Contract_AcceptedMessage".Translate(activeQuantity, Find.ActiveLanguageWorker.Pluralize(activeDef.label, activeQuantity)), MessageTypeDefOf.PositiveEvent, false);
        }

        public void DeclineOffer()
        {
            if (!hasOffer)
            {
                return;
            }
            hasOffer = false;
            offerDef = null;
            ScheduleNextOffer();
        }

        private void ResolveActive()
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            int stored = network != null ? network.StoredCartridgeCount(activeDef) : 0;
            string plural = Find.ActiveLanguageWorker.Pluralize(activeDef.label, activeQuantity);
            if (stored >= activeQuantity)
            {
                ConsumeCartridges(activeDef, activeQuantity);
                DropReward(activeRewardSilver);
                Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(
                    "RCDC_Contract_FulfilledLabel".Translate(),
                    "RCDC_Contract_FulfilledText".Translate(activeQuantity, plural, activeRewardSilver),
                    LetterDefOf.PositiveEvent, TargetInfo.Invalid));
            }
            else
            {
                Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(
                    "RCDC_Contract_ExpiredLabel".Translate(),
                    "RCDC_Contract_ExpiredText".Translate(activeQuantity, plural, stored),
                    LetterDefOf.NeutralEvent, TargetInfo.Invalid));
            }
            hasActive = false;
            activeDef = null;
            ScheduleNextOffer();
        }

        private void ConsumeCartridges(ThingDef def, int amount)
        {
            int remaining = amount;
            foreach (Thing thing in map.listerThings.ThingsOfDef(def).OrderByDescending(t => t.stackCount).ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }
                int take = System.Math.Min(remaining, thing.stackCount);
                thing.SplitOff(take).Destroy();
                remaining -= take;
            }
        }

        private void DropReward(int silver)
        {
            List<Thing> things = new List<Thing>();
            int remaining = silver;
            int stackLimit = ThingDefOf.Silver.stackLimit;
            while (remaining > 0)
            {
                int amount = System.Math.Min(remaining, stackLimit);
                Thing pile = ThingMaker.MakeThing(ThingDefOf.Silver);
                pile.stackCount = amount;
                things.Add(pile);
                remaining -= amount;
            }
            if (things.Count == 0)
            {
                return;
            }
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, things, 110, false, false, true, false);
        }

        /// <summary>Forces an offer on the next tick check, ignoring the cooldown (debug/test tooling).</summary>
        internal void DevForceOffer()
        {
            if (hasOffer || hasActive)
            {
                return;
            }
            nextOfferTick = Now;
        }

        /// <summary>Makes the pending offer expire on the next tick check, as if it had been ignored (test tooling).</summary>
        internal void DevExpireOfferNow()
        {
            if (hasOffer)
            {
                offerExpireTick = Now;
            }
        }

        /// <summary>Makes the active contract's deadline hit on the next tick check (test tooling).</summary>
        internal void DevForceDeadlineNow()
        {
            if (hasActive)
            {
                activeDeadlineTick = Now;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasOffer, "rcdcHasOffer", false);
            Scribe_Defs.Look(ref offerDef, "rcdcOfferDef");
            Scribe_Values.Look(ref offerQuantity, "rcdcOfferQuantity", 0);
            Scribe_Values.Look(ref offerBonusPercent, "rcdcOfferBonusPercent", 0f);
            Scribe_Values.Look(ref offerExpireTick, "rcdcOfferExpireTick", 0);
            Scribe_Values.Look(ref hasActive, "rcdcHasActive", false);
            Scribe_Defs.Look(ref activeDef, "rcdcActiveDef");
            Scribe_Values.Look(ref activeQuantity, "rcdcActiveQuantity", 0);
            Scribe_Values.Look(ref activeRewardSilver, "rcdcActiveRewardSilver", 0);
            Scribe_Values.Look(ref activeDeadlineTick, "rcdcActiveDeadlineTick", 0);
            Scribe_Values.Look(ref nextOfferTick, "rcdcNextOfferTick", -1);
        }
    }
}
