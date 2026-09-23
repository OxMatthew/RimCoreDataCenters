using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>Self-test section for the data contract system.</summary>
    internal static partial class SelfTest
    {
        private static IEnumerable<Waiter> SectionContracts()
        {
            if (layout == null)
            {
                yield break;
            }

            MapComponent_DataContracts contracts = MapComponent_DataContracts.For(Map);
            Check("the data contracts component exists on the map", contracts != null);
            if (contracts == null)
            {
                yield break;
            }
            Check("no contract is pending or active before this section", !contracts.HasOffer && !contracts.HasActiveContract);

            // ---- an offer appears and reads cleanly ----
            contracts.DevForceOffer();
            WaitUntil offered = new WaitUntil(() => contracts.HasOffer, 600);
            yield return offered;
            Check("a data contract offer appears once a rack exists", contracts.HasOffer, offered.TimedOut ? "timed out" : "ok");
            if (!contracts.HasOffer)
            {
                yield break;
            }
            DataContractSettingsDef settings = RcdcDefOf.RCDC_DataContractSettings;
            Check("the offer quantity is within the configured range",
                contracts.OfferQuantity >= settings.quantityMin && contracts.OfferQuantity <= settings.quantityMax, contracts.OfferQuantity.ToString());
            Check("the offer bonus is within the configured range",
                contracts.OfferBonusPercent >= settings.bonusPercentMin - 0.001f && contracts.OfferBonusPercent <= settings.bonusPercentMax + 0.001f, contracts.OfferBonusPercent.ToString("F3"));

            ChoiceLetter_DataContract offerLetter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_DataContract>().FirstOrDefault();
            Check("the offer letter was sent", offerLetter != null);
            if (offerLetter != null)
            {
                string text = offerLetter.Text.ToString();
                Check("the offer letter names the quantity and cartridge with no missing strings",
                    !string.IsNullOrEmpty(text) && !text.Contains("RCDC_") && text.Contains(contracts.OfferQuantity.ToString()) && text.Contains(contracts.OfferDef.label),
                    text.Replace('\n', '|'));
            }

            // ---- accepting locks in the deal ----
            ThingDef acceptedDef = contracts.OfferDef;
            int acceptedQuantity = contracts.OfferQuantity;
            contracts.AcceptOffer();
            Find.LetterStack.RemoveLetter(offerLetter);   // the real UI does this in the same click; mirror it here
            Check("accepting starts the active contract and clears the offer", contracts.HasActiveContract && !contracts.HasOffer);
            Check("the active contract matches what was offered", contracts.ActiveDef == acceptedDef && contracts.ActiveQuantity == acceptedQuantity,
                contracts.ActiveDef.defName + " x" + contracts.ActiveQuantity);
            Check("the active contract has a positive silver reward", contracts.ActiveRewardSilver > 0, contracts.ActiveRewardSilver.ToString());

            // ---- delivering enough cartridges by the deadline pays out ----
            IntVec3 stockCell = layout.AisleCell + new IntVec3(-3, 0, -2);
            SpawnCartridges(acceptedDef, acceptedQuantity, stockCell);
            // The demo racks keep producing standard cartridges in the background throughout this section, so
            // compare against a just-before-consumption baseline rather than assuming an absolute zero afterward.
            int storedBeforeConsume = Map.listerThings.ThingsOfDef(acceptedDef).Sum(t => t.stackCount);
            int silverBefore = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
            int expectedReward = contracts.ActiveRewardSilver;
            contracts.DevForceDeadlineNow();
            WaitUntil resolved = new WaitUntil(() => !contracts.HasActiveContract, 600);
            yield return resolved;
            Check("the contract clears itself once the deadline hits", !contracts.HasActiveContract, resolved.TimedOut ? "timed out" : "ok");
            Check("a fulfilled-contract letter was sent", Find.LetterStack.LettersListForReading.Any(l => l.Label.ToString() == "RCDC_Contract_FulfilledLabel".Translate().ToString()));
            int storedAfterConsume = Map.listerThings.ThingsOfDef(acceptedDef).Sum(t => t.stackCount);
            Check("the delivered cartridges were consumed", storedAfterConsume <= storedBeforeConsume - acceptedQuantity + 5,
                storedBeforeConsume + " -> " + storedAfterConsume + " (delivered " + acceptedQuantity + ")");

            WaitUntil landed = new WaitUntil(() => Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount) >= silverBefore + expectedReward, 3000);
            yield return landed;
            int silverAfter = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
            Check("fulfilling the contract pays out at least the quoted silver reward",
                silverAfter - silverBefore >= expectedReward, (silverAfter - silverBefore) + " vs expected " + expectedReward + (landed.TimedOut ? " (timed out)" : ""));

            // Clean up the silver so it doesn't skew any later section that checks trade proceeds.
            foreach (Thing silver in Map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList())
            {
                silver.Destroy(DestroyMode.Vanish);
            }

            // ---- missing the deadline costs nothing but the contract ----
            contracts.DevForceOffer();
            WaitUntil offeredAgain = new WaitUntil(() => contracts.HasOffer, 600);
            yield return offeredAgain;
            Check("a new offer appears after the first contract resolved", contracts.HasOffer, offeredAgain.TimedOut ? "timed out" : "ok");
            if (contracts.HasOffer)
            {
                contracts.AcceptOffer();
                Check("the second contract is active", contracts.HasActiveContract);
                int silverBeforeExpiry = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
                contracts.DevForceDeadlineNow();
                WaitUntil expired = new WaitUntil(() => !contracts.HasActiveContract, 600);
                yield return expired;
                int silverAfterExpiry = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
                Check("missing the deadline clears the contract with no reward and no penalty",
                    !contracts.HasActiveContract && silverAfterExpiry == silverBeforeExpiry, expired.TimedOut ? "timed out" : "ok");
            }

            // ---- an ignored offer withdraws itself ----
            contracts.DevForceOffer();
            WaitUntil offeredThird = new WaitUntil(() => contracts.HasOffer, 600);
            yield return offeredThird;
            Check("a third offer appears", contracts.HasOffer, offeredThird.TimedOut ? "timed out" : "ok");
            if (contracts.HasOffer)
            {
                contracts.DevExpireOfferNow();
                WaitUntil withdrawn = new WaitUntil(() => !contracts.HasOffer, 600);
                yield return withdrawn;
                Check("an ignored offer withdraws itself", !contracts.HasOffer, withdrawn.TimedOut ? "timed out" : "ok");
                Check("the withdrawn offer's letter is gone from the letter stack", !Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_DataContract));
            }

            // ---- leave one active, unresolved contract for the save/load section to round-trip ----
            contracts.DevForceOffer();
            WaitUntil offeredFinal = new WaitUntil(() => contracts.HasOffer, 600);
            yield return offeredFinal;
            Check("a final offer appears to leave an active contract for the save/load check", contracts.HasOffer, offeredFinal.TimedOut ? "timed out" : "ok");
            if (contracts.HasOffer)
            {
                contracts.AcceptOffer();
                Check("state prepared for the save: an unresolved data contract is active", contracts.HasActiveContract);
            }

            yield return null;
        }
    }
}
