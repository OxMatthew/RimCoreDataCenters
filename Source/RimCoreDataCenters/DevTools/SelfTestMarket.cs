using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>Self-test section for cartridge price drift and market events.</summary>
    internal static partial class SelfTest
    {
        private static IEnumerable<Waiter> SectionMarket()
        {
            if (layout == null)
            {
                yield break;
            }

            MapComponent_MarketDynamics market = MapComponent_MarketDynamics.For(Map);
            Check("the market dynamics component exists on the map", market != null);
            if (market == null)
            {
                yield break;
            }
            MarketDynamicsSettingsDef settings = RcdcDefOf.RCDC_MarketDynamicsSettings;
            Check("market dynamics settings are loaded", settings != null);

            ThingDef standard = RcdcDefOf.RCDC_DataCartridge;
            ThingDef financial = RcdcDefOf.RCDC_DataCartridge_Financial;
            ThingDef[] allTypes = { standard, RcdcDefOf.RCDC_DataCartridge_Research, financial, RcdcDefOf.RCDC_DataCartridge_Medical };
            foreach (ThingDef def in allTypes)
            {
                market.DevSetBaseline(def, 1f);
            }
            Check("with a neutral baseline and no event, market conditions don't change the price",
                Near(market.PriceMultiplier(standard), 1f, 0.001f));
            string steadyDesc = market.Describe();
            Check("the description says steady when nothing has moved",
                steadyDesc.Contains("RCDC_Market_Steady".Translate()) && !steadyDesc.Contains("RCDC_"), steadyDesc);

            // ---- drift ----
            market.DevForceDrift();
            WaitUntil drifted = new WaitUntil(() => !Near(market.BaselineMultiplier(standard), 1f, 0.0001f), 600);
            yield return drifted;
            float driftedValue = market.BaselineMultiplier(standard);
            Check("drift moves the baseline within the configured band", !drifted.TimedOut
                && driftedValue >= settings.driftMultiplierMin - 0.001f && driftedValue <= settings.driftMultiplierMax + 0.001f,
                driftedValue.ToString("F3"));

            // ---- a forced event actually changes the price, by exactly the reported multiplier ----
            market.DevSetBaseline(standard, 1f);
            float priceNeutral = standard.GetStatValueAbstract(StatDefOf.MarketValue);
            market.DevForceEvent(MarketEventKind.RivalBuyer, standard);
            Check("forcing a market event starts it", market.HasEvent && market.EventDef == standard && market.EventKind == MarketEventKind.RivalBuyer);
            float eventMultiplier = market.PriceMultiplier(standard);
            Check("the event multiplier is within the configured bonus range",
                eventMultiplier >= settings.eventBonusMin - 0.001f && eventMultiplier <= settings.eventBonusMax + 0.001f, eventMultiplier.ToString("F3"));
            float priceWithEvent = standard.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("the market stat part applies exactly the reported multiplier",
                Near(priceWithEvent / priceNeutral, eventMultiplier, 0.01f), (priceWithEvent / priceNeutral).ToString("F3") + " vs " + eventMultiplier.ToString("F3"));

            string eventDesc = market.Describe();
            Check("the market description names the affected cartridge with no missing strings",
                !string.IsNullOrEmpty(eventDesc) && !eventDesc.Contains("RCDC_") && eventDesc.Contains(standard.label), eventDesc);

            CompOperationsConsole console = MapComponent_DataCenterNetwork.For(Map).Consoles.FirstOrDefault();
            if (console != null)
            {
                string consoleText = console.parent.GetInspectString();
                Check("the operations console shows current market conditions with no missing strings",
                    consoleText.Contains("Market:") && !consoleText.Contains("RCDC_"), consoleText.Replace('\n', '|'));
            }

            // ---- the event ends on its own, and the price returns to the neutral baseline ----
            market.DevExpireEventNow();
            WaitUntil ended = new WaitUntil(() => !market.HasEvent, 600);
            yield return ended;
            Check("the market event ends on its own", !market.HasEvent, ended.TimedOut ? "timed out" : "ok");
            float priceAfterEnd = standard.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("the price returns to the neutral baseline once the event ends", Near(priceAfterEnd, priceNeutral, 0.5f),
                priceAfterEnd.ToString("F1") + " vs " + priceNeutral.ToString("F1"));

            // ---- state prepared for the save: a drifted baseline and a fresh active event ----
            market.DevSetBaseline(standard, 1.12f);
            market.DevForceEvent(MarketEventKind.Shortage, financial);
            Check("state prepared for the save: a drifted baseline and an active market event",
                Near(market.BaselineMultiplier(standard), 1.12f, 0.001f) && market.HasEvent && market.EventDef == financial);

            yield return null;
        }
    }
}
