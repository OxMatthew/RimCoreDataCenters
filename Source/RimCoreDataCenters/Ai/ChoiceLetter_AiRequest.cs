using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// A letter in which the AI core asks the player to agree to something small. Two options: accept (the
    /// arrangement starts and rapport rises) or decline (rapport dips a little). Ignoring it is handled by the
    /// AI core, which withdraws the letter after a few days.
    /// </summary>
    public class ChoiceLetter_AiRequest : ChoiceLetter
    {
        private Thing aiThing;
        private AiBoon kind;

        /// <summary>Creates the letter for an AI request (the game sets label, text and targets through LetterMaker).</summary>
        public static ChoiceLetter_AiRequest Create(CompAiCore ai, AiBoon requestKind)
        {
            string prefix = "RCDC_AiReq_" + requestKind;
            ChoiceLetter_AiRequest letter = (ChoiceLetter_AiRequest)LetterMaker.MakeLetter(
                (prefix + "_Label").Translate(AiVoice.Name), (prefix + "_Text").Translate(AiVoice.Name), RcdcDefOf.RCDC_AiLetter, new LookTargets(ai.parent));
            letter.aiThing = ai.parent;
            letter.kind = requestKind;
            letter.title = (prefix + "_Label").Translate(AiVoice.Name);   // the dialog's heading
            return letter;
        }

        private CompAiCore Ai
        {
            get { return aiThing == null ? null : aiThing.TryGetComp<CompAiCore>(); }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                string prefix = "RCDC_AiReq_" + kind;
                DiaOption accept = new DiaOption((prefix + "_Accept").Translate());
                accept.action = delegate
                {
                    CompAiCore ai = Ai;
                    if (ai != null)
                    {
                        ai.AcceptRequest(kind);
                    }
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;
                yield return accept;

                DiaOption decline = new DiaOption((prefix + "_Decline").Translate());
                decline.action = delegate
                {
                    CompAiCore ai = Ai;
                    if (ai != null)
                    {
                        ai.DeclineRequest();
                    }
                    Find.LetterStack.RemoveLetter(this);
                };
                decline.resolveTree = true;
                yield return decline;

                DiaOption later = new DiaOption("RCDC_AiReq_Later".Translate());
                later.resolveTree = true;
                yield return later;
            }
        }

        public override bool CanShowInLetterStack
        {
            get { return aiThing != null && !aiThing.Destroyed && base.CanShowInLetterStack; }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref aiThing, "rcdcAi");
            Scribe_Values.Look(ref kind, "rcdcKind", AiBoon.ComputeLoan);
        }
    }
}
