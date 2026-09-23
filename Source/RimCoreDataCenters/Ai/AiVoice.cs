using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Everything the AI core says. The lines are ordinary keyed strings (Languages/English/Keyed), so they are
    /// translatable and there is no generated or downloaded text. Topics have a fixed number of variants.
    /// </summary>
    public static class AiVoice
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>
        {
            { "Boot", 3 }, { "Idle_Warm", 4 }, { "Idle_Neutral", 4 }, { "Idle_Cold", 4 }, { "Overheat", 3 }, { "Service", 3 },
            { "Milestone", 3 }, { "GlitchReboot", 2 }, { "GlitchSulk", 2 }, { "GlitchCache", 2 }, { "Back", 2 },
            { "Balanced", 2 }, { "Efficiency", 2 }, { "Stewardship", 2 }, { "Curiosity", 2 }, { "Thanks", 2 }, { "Declined", 2 },
            { "Intrusion", 2 }
        };

        public static string Name
        {
            get { return "RCDC_AiName".Translate(); }
        }

        /// <summary>Every keyed string the voice can ask for (used by the self-test to prove none is missing).</summary>
        public static IEnumerable<string> AllKeys()
        {
            foreach (KeyValuePair<string, int> pair in Counts)
            {
                for (int i = 1; i <= pair.Value; i++)
                {
                    yield return KeyFor(pair.Key, i);
                }
            }
        }

        private static string KeyFor(string topic, int index)
        {
            return "RCDC_AiLine_" + topic + "_" + index;
        }

        /// <summary>A random variant of a topic.</summary>
        public static string Line(string topic)
        {
            int n;
            if (!Counts.TryGetValue(topic, out n))
            {
                return topic;
            }
            return KeyFor(topic, Rand.RangeInclusive(1, n)).Translate();
        }

        /// <summary>Which idle-chatter set fits the AI's mood.</summary>
        public static string IdleTopic(float rapport)
        {
            return rapport >= 65f ? "Idle_Warm" : (rapport >= 35f ? "Idle_Neutral" : "Idle_Cold");
        }

        /// <summary>Shows a line as a message from the AI, with its chime.</summary>
        public static void Announce(Thing source, string line, MessageTypeDef type)
        {
            Messages.Message("RCDC_AiSay".Translate(Name, line), source, type, false);
            if (RcdcDefOf.RCDC_AiChime != null && source != null && source.Spawned)
            {
                RcdcDefOf.RCDC_AiChime.PlayOneShot(new TargetInfo(source.Position, source.Map));
            }
        }
    }
}
