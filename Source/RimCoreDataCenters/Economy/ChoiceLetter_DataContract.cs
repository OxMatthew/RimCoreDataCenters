using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// The letter offering a data contract. Accept locks in the deal on the map's
    /// <see cref="MapComponent_DataContracts"/>; decline or leaving it unanswered past its timeout
    /// just clears the offer and schedules the next one.
    /// </summary>
    public class ChoiceLetter_DataContract : ChoiceLetter
    {
        private Map map;

        public static ChoiceLetter_DataContract Create(Map map, ThingDef cartridgeDef, int quantity, float bonusPercent)
        {
            string label = "RCDC_Contract_OfferLabel".Translate();
            string plural = Find.ActiveLanguageWorker.Pluralize(cartridgeDef.label, quantity);
            string text = "RCDC_Contract_OfferText".Translate(quantity, plural, bonusPercent.ToStringPercent());
            ChoiceLetter_DataContract letter = (ChoiceLetter_DataContract)LetterMaker.MakeLetter(label, text, RcdcDefOf.RCDC_ContractLetter, TargetInfo.Invalid);
            letter.map = map;
            letter.title = label;
            return letter;
        }

        private MapComponent_DataContracts Contracts
        {
            get { return MapComponent_DataContracts.For(map); }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                DiaOption accept = new DiaOption("RCDC_Contract_Accept".Translate());
                accept.action = delegate
                {
                    MapComponent_DataContracts contracts = Contracts;
                    if (contracts != null)
                    {
                        contracts.AcceptOffer();
                    }
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;
                yield return accept;

                DiaOption decline = new DiaOption("RCDC_Contract_Decline".Translate());
                decline.action = delegate
                {
                    MapComponent_DataContracts contracts = Contracts;
                    if (contracts != null)
                    {
                        contracts.DeclineOffer();
                    }
                    Find.LetterStack.RemoveLetter(this);
                };
                decline.resolveTree = true;
                yield return decline;

                DiaOption later = new DiaOption("RCDC_Contract_Later".Translate());
                later.resolveTree = true;
                yield return later;
            }
        }

        public override bool CanShowInLetterStack
        {
            get
            {
                MapComponent_DataContracts contracts = Contracts;
                return contracts != null && contracts.HasOffer && base.CanShowInLetterStack;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "rcdcContractMap");
        }
    }
}
