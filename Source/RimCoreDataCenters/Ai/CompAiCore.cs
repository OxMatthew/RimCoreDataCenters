using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimCore.DataCenters
{
    /// <summary>How the AI core prioritises. Each non-balanced directive trades one thing for another.</summary>
    public enum AiDirective
    {
        Balanced,
        Efficiency,
        Stewardship,
        Curiosity
    }

    public enum AiStatus
    {
        Offline,
        Overheated,
        Rebooting,
        Sulking,
        Online
    }

    /// <summary>A temporary arrangement the player agreed to when the AI asked for something.</summary>
    public enum AiBoon
    {
        None,
        ComputeLoan,
        Overclock,
        Diagnostics
    }

    public enum AiGlitch
    {
        Reboot,
        Sulk,
        CacheError
    }

    /// <summary>What the AI core currently does to the data center, all in one place. Neutral when it is not active.</summary>
    public sealed class AiModifiers
    {
        public static readonly AiModifiers Neutral = new AiModifiers();

        public float Output = 1f;
        public float Heat = 1f;
        public float Wear = 1f;
        public float Tolerance;
        public float ResearchPerRack;
        public float ResearchCap;
        public float ResearchMultiplier = 1f;
        public bool Monitoring;
        public bool Forecast;
    }

    public class CompProperties_AiCore : CompProperties
    {
        public float heatPerSecond = 6f;
        public float maxOperatingTemperature = 45f;
        public float startRapport = 50f;

        /// <summary>The AI's benefits scale from this fraction (at zero rapport) up to full strength (at 100).</summary>
        public float minStrength = 0.5f;

        // Always-on benefit: constant self-diagnostics on every rack.
        public float diagnosticsWear = 0.9f;

        // Directives.
        public float efficiencyOutput = 1.10f;
        public float efficiencyHeat = 1.20f;
        public float efficiencyPower = 1.25f;
        public float stewardshipWear = 0.75f;
        public float stewardshipTolerance = 2f;
        public float stewardshipOutput = 0.95f;
        public float curiosityResearchPerRack = 0.02f;
        public float curiosityResearchCap = 0.10f;
        public float curiosityOutput = 0.92f;

        // Requests and what accepting them does.
        public float requestMinDays = 8f;
        public float requestMaxDays = 14f;
        public float requestTimeoutDays = 5f;
        public int loanTicks = 60000;
        public float loanOutput = 0.8f;
        public float loanResearchMultiplier = 2f;
        public int overclockTicks = 60000;
        public float overclockOutput = 1.25f;
        public float overclockHeat = 1.4f;
        public int diagnosticsTicks = 7500;
        public float diagnosticsOutput = 0.5f;
        public float diagnosticsWearRelief = 0.2f;

        // Rapport (0 to 100).
        public float dailyGain = 1f;
        public float dailyLoss = 1f;
        public float dailyOfflineLoss = 3f;
        public float acceptGain = 5f;
        public float declineLoss = 2f;
        public float ignoredLoss = 4f;

        // Glitches: mean days between them, by mood.
        public float glitchCalmDays = 45f;
        public float glitchUneasyDays = 25f;
        public float glitchTenseDays = 12f;

        /// <summary>Glitch rate multiplier while the data center is security certified (0.5 = half as often).</summary>
        public float certifiedGlitchRate = 0.5f;

        public int rebootMinTicks = 2500;
        public int rebootMaxTicks = 5000;
        public int sulkTicks = 60000;
        public int milestoneEvery = 100;

        public CompProperties_AiCore()
        {
            compClass = typeof(CompAiCore);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
            {
                yield return "AI core requires CompProperties_Power.";
            }
            if (minStrength < 0f || minStrength > 1f)
            {
                yield return "CompProperties_AiCore.minStrength must be between 0 and 1.";
            }
            if (requestMinDays <= 0f || requestMaxDays < requestMinDays)
            {
                yield return "CompProperties_AiCore request interval is invalid.";
            }
        }
    }

    /// <summary>
    /// The AI core. It is a simulated character, not a language model: everything it says comes from the
    /// mod's own strings, and everything it does is deterministic bookkeeping over the racks' real state.
    /// It keeps the data center monitored, forecasts maintenance, follows a directive, has a rapport with the
    /// player that rises and falls with how the data center is run, asks the player for small favours, and now
    /// and then has a harmless glitch. It never causes hostile events.
    /// </summary>
    public class CompAiCore : ThingComp
    {
        private const int DayTicks = 60000;

        // ---- saved ------------------------------------------------------------------------------
        private float rapport = -1f;
        private AiDirective directive;
        private int nextRequestTick;
        private int nextGlitchTick;
        private int sulkUntilTick;
        private int rebootUntilTick;
        private int lastDailyTick;
        private bool booted;
        private int lastMilestone;
        private AiBoon boon;
        private int boonUntilTick;
        private Letter pendingLetter;
        private AiBoon pendingKind;
        private int requestExpireTick;
        private AiBoon lastKind;
        private int lastChatterTick = -999999;

        // ---- runtime ----------------------------------------------------------------------------
        private CompPowerTrader power;
        private AiModifiers cachedModifiers = AiModifiers.Neutral;
        private int cachedModifiersTick = -1;
        private bool wasSulking;
        private bool wasRebooting;
        private readonly HashSet<int> hotSeen = new HashSet<int>();
        private readonly HashSet<int> serviceSeen = new HashSet<int>();

        public CompProperties_AiCore Props
        {
            get { return (CompProperties_AiCore)props; }
        }

        private static int Now
        {
            get { return Find.TickManager.TicksGame; }
        }

        public float Rapport
        {
            get { return rapport < 0f ? Props.startRapport : rapport; }
        }

        public AiDirective Directive
        {
            get { return directive; }
        }

        public AiBoon ActiveBoon
        {
            get { return boon != AiBoon.None && Now < boonUntilTick ? boon : AiBoon.None; }
        }

        public int BoonTicksLeft
        {
            get { return ActiveBoon == AiBoon.None ? 0 : boonUntilTick - Now; }
        }

        public bool HasPendingRequest
        {
            get { return pendingLetter != null; }
        }

        /// <summary>True once the AI has come online at least once (so a brand-new, still unpowered core raises no alert).</summary>
        public bool HasEverBooted
        {
            get { return booted; }
        }

        public AiStatus Status
        {
            get
            {
                if (parent == null || !parent.Spawned || power == null || !power.PowerOn)
                {
                    return AiStatus.Offline;
                }
                if (!HasPoweredCore())
                {
                    return AiStatus.Offline;
                }
                if (parent.AmbientTemperature >= Props.maxOperatingTemperature)
                {
                    return AiStatus.Overheated;
                }
                if (Now < rebootUntilTick)
                {
                    return AiStatus.Rebooting;
                }
                if (Now < sulkUntilTick)
                {
                    return AiStatus.Sulking;
                }
                return AiStatus.Online;
            }
        }

        public bool IsActive
        {
            get { return Status == AiStatus.Online; }
        }

        /// <summary>The AI's benefits scale with rapport (and with Adaptive Learning).</summary>
        public float Strength
        {
            get
            {
                float s = Mathf.Lerp(Props.minStrength, 1f, Rapport / 100f) + RcdcUpgrades.Current.AiBenefitBonus;
                return Mathf.Clamp(s, 0f, 1.5f);
            }
        }

        public string MoodLabel
        {
            get
            {
                float r = Rapport;
                return (r >= 65f ? "RCDC_AiMood_Warm" : (r >= 35f ? "RCDC_AiMood_Neutral" : "RCDC_AiMood_Cold")).Translate();
            }
        }

        /// <summary>What the AI is doing to the data center right now.</summary>
        public AiModifiers Modifiers
        {
            get
            {
                int now = Now;
                if (cachedModifiersTick == now)
                {
                    return cachedModifiers;
                }
                cachedModifiersTick = now;
                cachedModifiers = BuildModifiers();
                return cachedModifiers;
            }
        }

        private AiModifiers BuildModifiers()
        {
            if (Status != AiStatus.Online)
            {
                return AiModifiers.Neutral;
            }
            CompProperties_AiCore p = Props;
            float s = Strength;
            AiModifiers m = new AiModifiers();
            m.Monitoring = true;
            m.Forecast = true;
            m.Wear = Scale(p.diagnosticsWear, s);
            switch (directive)
            {
                case AiDirective.Efficiency:
                    m.Output *= Scale(p.efficiencyOutput, s);
                    m.Heat *= Scale(p.efficiencyHeat, s);
                    break;
                case AiDirective.Stewardship:
                    m.Wear *= Scale(p.stewardshipWear, s);
                    m.Tolerance += p.stewardshipTolerance * s;
                    m.Output *= Scale(p.stewardshipOutput, s);
                    break;
                case AiDirective.Curiosity:
                    m.ResearchPerRack += p.curiosityResearchPerRack * s;
                    m.ResearchCap += p.curiosityResearchCap * s;
                    m.Output *= Scale(p.curiosityOutput, s);
                    break;
            }
            switch (ActiveBoon)
            {
                case AiBoon.ComputeLoan:
                    m.Output *= p.loanOutput;
                    m.ResearchMultiplier = p.loanResearchMultiplier;
                    break;
                case AiBoon.Overclock:
                    m.Output *= p.overclockOutput;
                    m.Heat *= p.overclockHeat;
                    break;
                case AiBoon.Diagnostics:
                    m.Output *= p.diagnosticsOutput;
                    break;
            }
            return m;
        }

        /// <summary>Moves a multiplier toward 1 by the AI's current strength: at strength 1 the full value applies.</summary>
        private static float Scale(float full, float strength)
        {
            return 1f + (full - 1f) * strength;
        }

        private bool HasPoweredCore()
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network == null)
            {
                return false;
            }
            IList<CompNetworkCore> cores = network.Cores;
            for (int i = 0; i < cores.Count; i++)
            {
                if (cores[i].IsOnline && cores[i].InRange(parent))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- lifecycle --------------------------------------------------------------------------

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
            if (rapport < 0f)
            {
                rapport = Props.startRapport;
            }
            if (lastDailyTick <= 0)
            {
                lastDailyTick = Now;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network != null)
            {
                network.Register(this);
            }
        }

#if RIMWORLD_1_6
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
#else
        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
#endif
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            if (network != null)
            {
                network.Unregister(this);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref rapport, "rcdcAiRapport", -1f);
            Scribe_Values.Look(ref directive, "rcdcAiDirective", AiDirective.Balanced);
            Scribe_Values.Look(ref nextRequestTick, "rcdcAiNextRequest", 0);
            Scribe_Values.Look(ref nextGlitchTick, "rcdcAiNextGlitch", 0);
            Scribe_Values.Look(ref sulkUntilTick, "rcdcAiSulkUntil", 0);
            Scribe_Values.Look(ref rebootUntilTick, "rcdcAiRebootUntil", 0);
            Scribe_Values.Look(ref lastDailyTick, "rcdcAiLastDaily", 0);
            Scribe_Values.Look(ref booted, "rcdcAiBooted", false);
            Scribe_Values.Look(ref lastMilestone, "rcdcAiMilestone", 0);
            Scribe_Values.Look(ref boon, "rcdcAiBoon", AiBoon.None);
            Scribe_Values.Look(ref boonUntilTick, "rcdcAiBoonUntil", 0);
            Scribe_References.Look(ref pendingLetter, "rcdcAiLetter");
            Scribe_Values.Look(ref pendingKind, "rcdcAiPendingKind", AiBoon.None);
            Scribe_Values.Look(ref requestExpireTick, "rcdcAiRequestExpire", 0);
            Scribe_Values.Look(ref lastKind, "rcdcAiLastKind", AiBoon.None);
            Scribe_Values.Look(ref lastChatterTick, "rcdcAiLastChatter", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rapport = float.IsNaN(rapport) ? Props.startRapport : Mathf.Clamp(rapport, 0f, 100f);
            }
        }

        // ---- the simulation step ------------------------------------------------------------------

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned)
            {
                return;
            }
            Step();
        }

        /// <summary>One simulation step (a rare tick). Also called directly by the self-test.</summary>
        internal void Step()
        {
            int now = Now;
            AiStatus status = Status;
            cachedModifiersTick = -1;

            ApplyPowerAndHeat(status);

            // Announce transitions back to normal.
            bool rebooting = status == AiStatus.Rebooting;
            bool sulking = status == AiStatus.Sulking;
            if (wasRebooting && !rebooting && status == AiStatus.Online)
            {
                Say("Back", MessageTypeDefOf.PositiveEvent);
            }
            if (wasSulking && !sulking && status == AiStatus.Online)
            {
                Say("Back", MessageTypeDefOf.PositiveEvent);
            }
            wasRebooting = rebooting;
            wasSulking = sulking;

            if (boon != AiBoon.None && now >= boonUntilTick)
            {
                Messages.Message("RCDC_AiBoonEnded".Translate(AiVoice.Name), parent, MessageTypeDefOf.NeutralEvent, false);
                boon = AiBoon.None;
            }

            if (now - lastDailyTick >= DayTicks)
            {
                lastDailyTick += DayTicks;
                DailyUpdate(status);
            }

            if (status != AiStatus.Online)
            {
                return;
            }

            if (!booted)
            {
                booted = true;
                nextRequestTick = now + Mathf.RoundToInt(Rand.Range(Props.requestMinDays, Props.requestMaxDays) * DayTicks);
                nextGlitchTick = now + Mathf.RoundToInt(GlitchMeanTicks());
                Say("Boot", MessageTypeDefOf.PositiveEvent);
            }

            WatchRacks(now);
            CheckMilestone();
            HandleRequests(now);
            HandleGlitches(now);
            Chatter(now);
        }

        private void ApplyPowerAndHeat(AiStatus status)
        {
            if (power == null || !power.PowerOn)
            {
                return;
            }
            float rated = power.Props.PowerConsumption;
            float factor = 1f;
            if (status == AiStatus.Online && directive == AiDirective.Efficiency)
            {
                factor = Props.efficiencyPower;
            }
            else if (status != AiStatus.Online)
            {
                factor = 0.3f;
            }
            power.PowerOutput = -rated * factor;
            if (status == AiStatus.Online && parent.AmbientTemperature < 60f)
            {
                GenTemperature.PushHeat(parent, Props.heatPerSecond * 4.1666665f);
            }
        }

        private List<CompServerRack> Racks()
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            List<CompServerRack> list = new List<CompServerRack>();
            if (network != null)
            {
                for (int i = 0; i < network.Racks.Count; i++)
                {
                    CompServerRack r = network.Racks[i];
                    if (r != null && r.parent != null && r.parent.Spawned)
                    {
                        list.Add(r);
                    }
                }
            }
            return list;
        }

        // ---- mood ---------------------------------------------------------------------------------

        private void DailyUpdate(AiStatus status)
        {
            CompProperties_AiCore p = Props;
            if (status != AiStatus.Online)
            {
                if (booted)
                {
                    AdjustRapport(-p.dailyOfflineLoss);
                }
                return;
            }
            bool anyOperational = false;
            bool anyProblem = false;
            foreach (CompServerRack rack in Racks())
            {
                RackStatus s = rack.Status;
                if (s == RackStatus.Operational)
                {
                    anyOperational = true;
                }
                else if (s != RackStatus.OutputFull)
                {
                    anyProblem = true;
                }
            }
            if (anyProblem)
            {
                AdjustRapport(-p.dailyLoss);
            }
            else if (anyOperational)
            {
                AdjustRapport(p.dailyGain);
            }
        }

        internal void DevRunDaily()
        {
            DailyUpdate(Status);
        }

        /// <summary>Self-test hook: the AI core's current power draw in watts.</summary>
        internal float DevPowerDraw()
        {
            return power == null ? 0f : -power.PowerOutput;
        }

        /// <summary>Self-test hook: forget cached state so a change made this very tick (temperature, glitches) is seen at once.</summary>
        internal void DevRefresh()
        {
            cachedModifiersTick = -1;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network != null)
            {
                network.InvalidateAiCache();
            }
        }

        /// <summary>Self-test hook: makes the pending request time out on the next step.</summary>
        internal void DevExpireRequest()
        {
            requestExpireTick = 0;
        }

        /// <summary>Self-test hook: clears any temporary arrangement and pending request without touching rapport.</summary>
        internal void DevClear()
        {
            boon = AiBoon.None;
            boonUntilTick = 0;
            if (pendingLetter != null)
            {
                Find.LetterStack.RemoveLetter(pendingLetter);
                pendingLetter = null;
            }
            sulkUntilTick = 0;
            rebootUntilTick = 0;
            DevRefresh();
        }

        public void AdjustRapport(float delta)
        {
            rapport = Mathf.Clamp(Rapport + delta, 0f, 100f);
        }

        internal void DevSetRapport(float value)
        {
            rapport = Mathf.Clamp(value, 0f, 100f);
            cachedModifiersTick = -1;
        }

        // ---- talking ------------------------------------------------------------------------------

        private void Say(string topic, MessageTypeDef type)
        {
            AiVoice.Announce(parent, AiVoice.Line(topic), type);
        }

        private void WatchRacks(int now)
        {
            foreach (CompServerRack rack in Racks())
            {
                int id = rack.parent.thingIDNumber;
                RackStatus s = rack.Status;
                if (s == RackStatus.TooHot)
                {
                    if (hotSeen.Add(id))
                    {
                        AiVoice.Announce(rack.parent, AiVoice.Line("Overheat"), MessageTypeDefOf.CautionInput);
                    }
                }
                else
                {
                    hotSeen.Remove(id);
                }
                if (s == RackStatus.MaintenanceRequired)
                {
                    if (serviceSeen.Add(id))
                    {
                        AiVoice.Announce(rack.parent, AiVoice.Line("Service"), MessageTypeDefOf.CautionInput);
                    }
                }
                else
                {
                    serviceSeen.Remove(id);
                }
            }
        }

        private void CheckMilestone()
        {
            int total = 0;
            foreach (CompServerRack rack in Racks())
            {
                total += rack.TotalProduced;
            }
            int milestone = total / Props.milestoneEvery;
            if (milestone > lastMilestone)
            {
                lastMilestone = milestone;
                AiVoice.Announce(parent, "RCDC_AiMilestone".Translate((milestone * Props.milestoneEvery).ToString(), AiVoice.Line("Milestone")), MessageTypeDefOf.PositiveEvent);
            }
        }

        private void Chatter(int now)
        {
            if (now - lastChatterTick < 2 * DayTicks || !Rand.Chance(0.35f))
            {
                return;
            }
            lastChatterTick = now;
            AiVoice.Announce(parent, AiVoice.Line(AiVoice.IdleTopic(Rapport)), MessageTypeDefOf.NeutralEvent);
        }

        // ---- directives ----------------------------------------------------------------------------

        public void SetDirective(AiDirective value)
        {
            if (directive == value)
            {
                return;
            }
            directive = value;
            cachedModifiersTick = -1;
            AiVoice.Announce(parent, AiVoice.Line(value.ToString()), MessageTypeDefOf.NeutralEvent);
        }

        // ---- requests -------------------------------------------------------------------------------

        private void HandleRequests(int now)
        {
            if (pendingLetter != null)
            {
                bool stillThere = Find.LetterStack.LettersListForReading.Contains(pendingLetter);
                if (!stillThere)
                {
                    pendingLetter = null;   // the player closed it some other way; the choice handlers already ran
                    return;
                }
                if (now >= requestExpireTick)
                {
                    Find.LetterStack.RemoveLetter(pendingLetter);
                    pendingLetter = null;
                    AdjustRapport(-Props.ignoredLoss);
                    Messages.Message("RCDC_AiRequestIgnored".Translate(AiVoice.Name), parent, MessageTypeDefOf.NeutralEvent, false);
                    ScheduleNextRequest(now);
                }
                return;
            }
            if (now >= nextRequestTick)
            {
                SendRequest(PickRequestKind());
            }
        }

        private AiBoon PickRequestKind()
        {
            AiBoon[] kinds = { AiBoon.ComputeLoan, AiBoon.Overclock, AiBoon.Diagnostics };
            AiBoon pick = kinds[Rand.Range(0, kinds.Length)];
            if (pick == lastKind)
            {
                pick = kinds[(System.Array.IndexOf(kinds, pick) + 1) % kinds.Length];
            }
            return pick;
        }

        private void ScheduleNextRequest(int now)
        {
            nextRequestTick = now + Mathf.RoundToInt(Rand.Range(Props.requestMinDays, Props.requestMaxDays) * DayTicks);
        }

        internal void SendRequest(AiBoon kind)
        {
            if (pendingLetter != null)
            {
                return;
            }
            ChoiceLetter_AiRequest letter = ChoiceLetter_AiRequest.Create(this, kind);
            pendingLetter = letter;
            pendingKind = kind;
            lastKind = kind;
            requestExpireTick = Now + Mathf.RoundToInt(Props.requestTimeoutDays * DayTicks);
            Find.LetterStack.ReceiveLetter(letter);
        }

        internal void AcceptRequest(AiBoon kind)
        {
            CompProperties_AiCore p = Props;
            int now = Now;
            switch (kind)
            {
                case AiBoon.ComputeLoan:
                    boon = kind;
                    boonUntilTick = now + p.loanTicks;
                    break;
                case AiBoon.Overclock:
                    boon = kind;
                    boonUntilTick = now + p.overclockTicks;
                    break;
                case AiBoon.Diagnostics:
                    boon = kind;
                    boonUntilTick = now + p.diagnosticsTicks;
                    foreach (CompServerRack rack in Racks())
                    {
                        rack.ApplyWearRelief(p.diagnosticsWearRelief);
                    }
                    break;
            }
            pendingLetter = null;
            cachedModifiersTick = -1;
            AdjustRapport(p.acceptGain);
            ScheduleNextRequest(now);
            AiVoice.Announce(parent, AiVoice.Line("Thanks"), MessageTypeDefOf.PositiveEvent);
        }

        internal void DeclineRequest()
        {
            pendingLetter = null;
            AdjustRapport(-Props.declineLoss);
            ScheduleNextRequest(Now);
            AiVoice.Announce(parent, AiVoice.Line("Declined"), MessageTypeDefOf.NeutralEvent);
        }

        // ---- glitches --------------------------------------------------------------------------------

        /// <summary>Mean ticks between glitches: rarer when we get along, rarer still in a certified, hardened room.</summary>
        public float GlitchMeanTicks()
        {
            CompProperties_AiCore p = Props;
            float r = Rapport;
            float days = r >= 60f ? p.glitchCalmDays : (r >= 30f ? p.glitchUneasyDays : p.glitchTenseDays);
            float rate = 1f - Mathf.Clamp01(RcdcUpgrades.Current.AiGlitchReduction);
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network != null && network.IsCertified)
            {
                rate *= p.certifiedGlitchRate;
            }
            return days * DayTicks / Mathf.Max(0.05f, rate);
        }

        private void HandleGlitches(int now)
        {
            if (now < nextGlitchTick)
            {
                return;
            }
            AiGlitch glitch;
            float roll = Rand.Value;
            if (Rapport < 50f)
            {
                glitch = roll < 0.35f ? AiGlitch.Sulk : (roll < 0.7f ? AiGlitch.Reboot : AiGlitch.CacheError);
            }
            else
            {
                glitch = roll < 0.5f ? AiGlitch.Reboot : AiGlitch.CacheError;
            }
            TriggerGlitch(glitch);
        }

        internal void TriggerGlitch(AiGlitch glitch)
        {
            int now = Now;
            nextGlitchTick = now + Mathf.RoundToInt(GlitchMeanTicks() * Rand.Range(0.6f, 1.4f));
            cachedModifiersTick = -1;
            MapComponent_DataCenterNetwork changed = MapComponent_DataCenterNetwork.For(parent.Map);
            if (changed != null)
            {
                changed.InvalidateAiCache();
            }
            switch (glitch)
            {
                case AiGlitch.Reboot:
                    rebootUntilTick = now + Rand.RangeInclusive(Props.rebootMinTicks, Props.rebootMaxTicks);
                    AiVoice.Announce(parent, AiVoice.Line("GlitchReboot"), MessageTypeDefOf.NeutralEvent);
                    break;
                case AiGlitch.Sulk:
                    sulkUntilTick = now + Props.sulkTicks;
                    AiVoice.Announce(parent, AiVoice.Line("GlitchSulk"), MessageTypeDefOf.NeutralEvent);
                    break;
                default:
                    List<CompServerRack> racks = Racks();
                    if (racks.Count > 0)
                    {
                        CompServerRack victim = racks[Rand.Range(0, racks.Count)];
                        victim.DevSetProgress(0f);
                        AiVoice.Announce(victim.parent, "RCDC_AiCacheError".Translate(victim.parent.LabelShort, AiVoice.Line("GlitchCache")), MessageTypeDefOf.NeutralEvent);
                    }
                    break;
            }
        }

        // ---- inspection -------------------------------------------------------------------------------

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(AiVoice.Name).Append(": ").Append(("RCDC_AiStatus_" + Status).Translate());
            sb.Append('\n').Append("RCDC_AiRapportLine".Translate(MoodLabel, Mathf.RoundToInt(Rapport)));
            sb.Append('\n').Append("RCDC_AiDirectiveLine".Translate(("RCDC_AiDirective_" + directive).Translate()));
            AiBoon active = ActiveBoon;
            if (active != AiBoon.None)
            {
                sb.Append('\n').Append("RCDC_AiBoonLine".Translate(("RCDC_AiBoon_" + active).Translate(), (BoonTicksLeft / 2500f).ToString("F1")));
            }
            if (pendingLetter != null)
            {
                sb.Append('\n').Append("RCDC_AiWaitingLine".Translate());
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                icon = AiIcons.Directive,
                defaultLabel = "RCDC_AiDirectiveGizmo".Translate(),
                defaultDesc = "RCDC_AiDirectiveGizmoDesc".Translate(),
                action = OpenDirectiveMenu
            };
            yield return new Command_Action
            {
                icon = AiIcons.Report,
                defaultLabel = "RCDC_AiReportGizmo".Translate(AiVoice.Name),
                defaultDesc = "RCDC_AiReportGizmoDesc".Translate(),
                action = delegate { AiReport.Show(this); }
            };
            if (Prefs.DevMode)
            {
                yield return new Command_Action { icon = TexButton.Add, defaultLabel = "DEV: rapport 100", action = delegate { DevSetRapport(100f); } };
                yield return new Command_Action { icon = TexButton.Add, defaultLabel = "DEV: rapport 10", action = delegate { DevSetRapport(10f); } };
                yield return new Command_Action { icon = TexButton.Add, defaultLabel = "DEV: send request", action = delegate { SendRequest(PickRequestKind()); } };
                yield return new Command_Action { icon = TexButton.Add, defaultLabel = "DEV: glitch (reboot)", action = delegate { TriggerGlitch(AiGlitch.Reboot); } };
                yield return new Command_Action { icon = TexButton.Add, defaultLabel = "DEV: glitch (cache)", action = delegate { TriggerGlitch(AiGlitch.CacheError); } };
            }
        }

        private void OpenDirectiveMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (AiDirective d in new[] { AiDirective.Balanced, AiDirective.Efficiency, AiDirective.Stewardship, AiDirective.Curiosity })
            {
                AiDirective chosen = d;
                string label = ("RCDC_AiDirective_" + d).Translate() + " - " + ("RCDC_AiDirectiveDesc_" + d).Translate();
                options.Add(new FloatMenuOption(label, delegate { SetDirective(chosen); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ---- visuals ---------------------------------------------------------------------------------

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned)
            {
                return;
            }
            Material mat;
            switch (Status)
            {
                case AiStatus.Online:
                    mat = (Now / 45 + parent.thingIDNumber) % 2 == 0 ? RackLedMaterials.Blue : RackLedMaterials.Green;
                    break;
                case AiStatus.Rebooting:
                case AiStatus.Sulking:
                    mat = RackLedMaterials.Amber;
                    break;
                case AiStatus.Overheated:
                    mat = RackLedMaterials.Red;
                    break;
                default:
                    mat = RackLedMaterials.Off;
                    break;
            }
            Vector3 pos = parent.DrawPos;
            pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.28f, 1f, 0.28f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
        }
    }

    /// <summary>The two command icons for the AI core.</summary>
    [StaticConstructorOnStartup]
    internal static class AiIcons
    {
        public static readonly Texture2D Directive = ContentFinder<Texture2D>.Get("RCDC/UI/AiDirective", true);
        public static readonly Texture2D Report = ContentFinder<Texture2D>.Get("RCDC/UI/AiReport", true);
    }
}
