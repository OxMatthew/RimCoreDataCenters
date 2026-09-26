using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    public enum MarketEventKind
    {
        RivalBuyer,
        Glut,
        Shortage
    }

    /// <summary>
    /// Slowly drifts each data cartridge type's price within a band, and occasionally layers a named
    /// event (a rival buyer, a glut or a shortage) on top of one type for a while. Pure economy flavor:
    /// no new building, no letters requiring a decision, just a StatPart reading this component's numbers.
    /// </summary>
    public class MapComponent_MarketDynamics : MapComponent
    {
        private const int CheckIntervalTicks = 200;
        private const int DayTicks = 60000;

        private Dictionary<ThingDef, float> baseline = new Dictionary<ThingDef, float>();
        private int nextDriftTick = -1;

        private bool hasEvent;
        private ThingDef eventDef;
        private MarketEventKind eventKind;
        private float eventMultiplier;
        private int eventEndTick;
        private int nextEventCheckTick = -1;

        public MapComponent_MarketDynamics(Map map) : base(map)
        {
        }

        public static MapComponent_MarketDynamics For(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_MarketDynamics>();
        }

        private static IEnumerable<ThingDef> TrackedDefs()
        {
            yield return RcdcDefOf.RCDC_DataCartridge;
            yield return RcdcDefOf.RCDC_DataCartridge_Research;
            yield return RcdcDefOf.RCDC_DataCartridge_Financial;
            yield return RcdcDefOf.RCDC_DataCartridge_Medical;
        }

        private MarketDynamicsSettingsDef Settings
        {
            get { return RcdcDefOf.RCDC_MarketDynamicsSettings; }
        }

        private static int Now
        {
            get { return Find.TickManager != null ? Find.TickManager.TicksGame : 0; }
        }

        public bool HasEvent { get { return hasEvent; } }
        public ThingDef EventDef { get { return eventDef; } }
        public MarketEventKind EventKind { get { return eventKind; } }
        public float EventMultiplier { get { return eventMultiplier; } }
        public int EventEndTick { get { return eventEndTick; } }

        /// <summary>The drifting baseline for a cartridge type, before any event (1.0 = base value).</summary>
        public float BaselineMultiplier(ThingDef def)
        {
            float value;
            return baseline.TryGetValue(def, out value) ? value : 1f;
        }

        /// <summary>The full price multiplier for a cartridge type right now: baseline drift times any active event.</summary>
        public float PriceMultiplier(ThingDef def)
        {
            float value = BaselineMultiplier(def);
            if (hasEvent && eventDef == def)
            {
                value *= eventMultiplier;
            }
            return value;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (ThingDef def in TrackedDefs())
            {
                if (!baseline.ContainsKey(def))
                {
                    baseline[def] = 1f;
                }
            }
            if (nextDriftTick < 0)
            {
                ScheduleNextDrift();
            }
            if (nextEventCheckTick < 0)
            {
                ScheduleNextEventCheck();
            }
        }

        private void ScheduleNextDrift()
        {
            MarketDynamicsSettingsDef settings = Settings;
            float min = settings != null ? settings.driftIntervalMinDays : 2f;
            float max = settings != null ? settings.driftIntervalMaxDays : 4f;
            nextDriftTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(min, max) * DayTicks);
        }

        private void ScheduleNextEventCheck()
        {
            MarketDynamicsSettingsDef settings = Settings;
            float min = settings != null ? settings.eventCheckIntervalMinDays : 4f;
            float max = settings != null ? settings.eventCheckIntervalMaxDays : 8f;
            nextEventCheckTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(min, max) * DayTicks);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Now % CheckIntervalTicks != 0)
            {
                return;
            }
            MarketDynamicsSettingsDef settings = Settings;
            if (settings == null)
            {
                return;
            }
            if (Now >= nextDriftTick)
            {
                Drift(settings);
                ScheduleNextDrift();
            }
            if (hasEvent)
            {
                if (Now >= eventEndTick)
                {
                    EndEvent();
                }
            }
            else if (Now >= nextEventCheckTick)
            {
                if (Rand.Chance(settings.eventChance))
                {
                    StartEvent(settings);
                }
                ScheduleNextEventCheck();
            }
        }

        private void Drift(MarketDynamicsSettingsDef settings)
        {
            foreach (ThingDef def in TrackedDefs())
            {
                float value = BaselineMultiplier(def);
                value += Rand.Range(-settings.driftStep, settings.driftStep);
                value = UnityEngine.Mathf.Clamp(value, settings.driftMultiplierMin, settings.driftMultiplierMax);
                baseline[def] = value;
            }
        }

        private void StartEvent(MarketDynamicsSettingsDef settings)
        {
            List<ThingDef> defs = TrackedDefs().ToList();
            eventDef = defs[Rand.Range(0, defs.Count)];
            eventKind = (MarketEventKind)Rand.Range(0, 3);
            eventMultiplier = eventKind == MarketEventKind.Glut
                ? Rand.Range(settings.eventMalusMin, settings.eventMalusMax)
                : Rand.Range(settings.eventBonusMin, settings.eventBonusMax);
            eventEndTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(settings.eventDurationMinDays, settings.eventDurationMaxDays) * DayTicks);
            hasEvent = true;
            string key = "RCDC_Market_" + eventKind + "Message";
            float magnitude = eventKind == MarketEventKind.Glut ? (1f - eventMultiplier) * 100f : (eventMultiplier - 1f) * 100f;
            Messages.Message(key.Translate(eventDef.label, magnitude.ToString("F0")),
                eventKind == MarketEventKind.Glut ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.PositiveEvent, false);
        }

        private void EndEvent()
        {
            Messages.Message("RCDC_Market_EventEndedMessage".Translate(eventDef.label), MessageTypeDefOf.NeutralEvent, false);
            hasEvent = false;
            eventDef = null;
        }

        /// <summary>A short per-type summary for the operations console's inspect text.</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (ThingDef def in TrackedDefs())
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                float mult = PriceMultiplier(def);
                int pct = UnityEngine.Mathf.RoundToInt((mult - 1f) * 100f);
                sb.Append(def.label).Append(' ');
                if (pct == 0)
                {
                    sb.Append("RCDC_Market_Steady".Translate());
                }
                else
                {
                    sb.Append(pct > 0 ? "+" : "").Append(pct).Append('%');
                }
            }
            return sb.ToString();
        }

        /// <summary>Forces the drift check to run on the next tick (debug/test tooling).</summary>
        internal void DevForceDrift()
        {
            nextDriftTick = Now;
        }

        /// <summary>Directly sets a cartridge type's drifting baseline, bypassing the random walk (test tooling).</summary>
        internal void DevSetBaseline(ThingDef def, float value)
        {
            baseline[def] = value;
        }

        /// <summary>Forces a specific market event to start right now, skipping the random roll (debug/test tooling).</summary>
        internal void DevForceEvent(MarketEventKind kind, ThingDef def)
        {
            if (hasEvent)
            {
                return;
            }
            MarketDynamicsSettingsDef settings = Settings;
            eventDef = def;
            eventKind = kind;
            eventMultiplier = kind == MarketEventKind.Glut
                ? Rand.Range(settings.eventMalusMin, settings.eventMalusMax)
                : Rand.Range(settings.eventBonusMin, settings.eventBonusMax);
            eventEndTick = Now + UnityEngine.Mathf.RoundToInt(Rand.Range(settings.eventDurationMinDays, settings.eventDurationMaxDays) * DayTicks);
            hasEvent = true;
        }

        /// <summary>Makes the active event end on the next tick check (debug/test tooling).</summary>
        internal void DevExpireEventNow()
        {
            if (hasEvent)
            {
                eventEndTick = Now;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref baseline, "rcdcBaseline", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref nextDriftTick, "rcdcNextDriftTick", -1);
            Scribe_Values.Look(ref hasEvent, "rcdcHasEvent", false);
            Scribe_Defs.Look(ref eventDef, "rcdcEventDef");
            Scribe_Values.Look(ref eventKind, "rcdcEventKind", MarketEventKind.RivalBuyer);
            Scribe_Values.Look(ref eventMultiplier, "rcdcEventMultiplier", 1f);
            Scribe_Values.Look(ref eventEndTick, "rcdcEventEndTick", 0);
            Scribe_Values.Look(ref nextEventCheckTick, "rcdcNextEventCheckTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && baseline == null)
            {
                baseline = new Dictionary<ThingDef, float>();
            }
        }
    }
}
