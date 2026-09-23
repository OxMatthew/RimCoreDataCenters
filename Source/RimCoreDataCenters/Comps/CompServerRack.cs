using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimCore.DataCenters
{
    public class CompProperties_ServerRack : CompProperties
    {
        // ---- Production -------------------------------------------------------------------
        /// <summary>The item produced by default (and the only one available until a variant's own research is finished).</summary>
        public ThingDef outputThing;

        /// <summary>
        /// Every data type this rack can be set to produce, including <see cref="outputThing"/>. A player can pick
        /// among these with the Specialization command once that item's own <c>researchPrerequisites</c> (if any)
        /// are finished. Leave empty (the default) to keep the rack fixed to <see cref="outputThing"/>.
        /// </summary>
        public List<ThingDef> outputOptions;

        /// <summary>Ticks of full-efficiency operation needed per production cycle (36000 = 0.6 day).</summary>
        public int ticksPerCartridge = 36000;

        /// <summary>Items produced per cycle.</summary>
        public int cartridgesPerCycle = 1;

        // ---- Heat and power ------------------------------------------------------------------
        /// <summary>Waste heat while running, in heat units per second (vanilla heater = 21).</summary>
        public float heatPerSecond = 8f;

        /// <summary>Fraction of heatPerSecond emitted while powered but idle/halted/shut down.</summary>
        public float standbyHeatFactor = 0.15f;

        /// <summary>Fraction of the rated power draw used while idle/halted/shut down.</summary>
        public float standbyPowerFactor = 0.2f;

        /// <summary>A rack stops adding heat once the air around it reaches this temperature (runaway safety valve).</summary>
        public float maxHeatPushTemperature = 60f;

        // ---- Temperature behaviour (degrees Celsius) ------------------------------------------
        /// <summary>Above this the rack throttles its output.</summary>
        public float warmTemperature = 32f;

        /// <summary>At or above this the rack performs an emergency shutdown.</summary>
        public float shutdownTemperature = 50f;

        /// <summary>After an emergency shutdown the rack restarts once the air cools to this.</summary>
        public float restartTemperature = 40f;

        /// <summary>Output fraction just below the shutdown temperature.</summary>
        public float minTemperatureEfficiency = 0.25f;

        // ---- Maintenance ------------------------------------------------------------------------
        /// <summary>Days of continuous operation before wear reaches 100% and the rack halts.</summary>
        public float daysToFullWear = 12f;

        /// <summary>Wear at which the rack asks for service and starts losing efficiency.</summary>
        public float serviceThreshold = 0.5f;

        /// <summary>Minimum wear before a player-ordered (forced) service is allowed.</summary>
        public float forcedServiceThreshold = 0.1f;

        /// <summary>Output fraction just before the rack halts from wear.</summary>
        public float minWearEfficiency = 0.35f;

        /// <summary>Output multiplier while the network has no operations coverage.</summary>
        public float unmonitoredOutputFactor = 0.65f;

        /// <summary>Wear-rate multiplier while the network has no operations coverage.</summary>
        public float unmonitoredWearFactor = 1.35f;

        /// <summary>Wear-rate multiplier while the rack is throttled by heat.</summary>
        public float throttledWearFactor = 1.5f;

        /// <summary>Work units for one service visit (1 per tick at speed 1.0).</summary>
        public int serviceWorkTicks = 1500;

        /// <summary>Part consumed by a service visit (null = none).</summary>
        public ThingDef serviceItem;

        public int serviceItemCount = 1;

        public CompProperties_ServerRack()
        {
            compClass = typeof(CompServerRack);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (outputThing == null)
            {
                yield return "CompProperties_ServerRack.outputThing is not set.";
            }
            if (outputOptions != null)
            {
                if (!outputOptions.Contains(outputThing))
                {
                    yield return "CompProperties_ServerRack.outputOptions must include outputThing.";
                }
                for (int i = 0; i < outputOptions.Count; i++)
                {
                    if (outputOptions[i] == null)
                    {
                        yield return "CompProperties_ServerRack.outputOptions has a null entry.";
                    }
                }
            }
            if (ticksPerCartridge < 250)
            {
                yield return "CompProperties_ServerRack.ticksPerCartridge must be at least 250.";
            }
            if (cartridgesPerCycle < 1)
            {
                yield return "CompProperties_ServerRack.cartridgesPerCycle must be at least 1.";
            }
            if (!(warmTemperature < shutdownTemperature))
            {
                yield return "CompProperties_ServerRack.warmTemperature must be below shutdownTemperature.";
            }
            if (!(restartTemperature < shutdownTemperature))
            {
                yield return "CompProperties_ServerRack.restartTemperature must be below shutdownTemperature.";
            }
            if (daysToFullWear <= 0f)
            {
                yield return "CompProperties_ServerRack.daysToFullWear must be positive.";
            }
            if (serviceThreshold <= 0f || serviceThreshold >= 1f)
            {
                yield return "CompProperties_ServerRack.serviceThreshold must be between 0 and 1.";
            }
            if (!parentDef.hasInteractionCell)
            {
                yield return "Server Rack needs hasInteractionCell (cartridges are placed there).";
            }
            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
            {
                yield return "Server Rack requires CompProperties_Power.";
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }
            if (outputThing != null)
            {
                float perDay = 60000f / ticksPerCartridge * cartridgesPerCycle;
                yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatOutput".Translate(),
                    "RCDC_PerDayValue".Translate(perDay.ToString("F1"), outputThing.label), "RCDC_StatOutputDesc".Translate(), 4400);
            }
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatHeat".Translate(),
                "RCDC_HeatValue".Translate(heatPerSecond.ToString("F1")), "RCDC_StatHeatDesc".Translate(), 4390);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatSafeTemp".Translate(),
                warmTemperature.ToStringTemperature("F0"), "RCDC_StatSafeTempDesc".Translate(
                    warmTemperature.ToStringTemperature("F0"), shutdownTemperature.ToStringTemperature("F0"),
                    restartTemperature.ToStringTemperature("F0")), 4380);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatServiceInterval".Translate(),
                "RCDC_DaysValue".Translate((daysToFullWear * serviceThreshold).ToString("F1")),
                "RCDC_StatServiceIntervalDesc".Translate((daysToFullWear * serviceThreshold).ToString("F1"), daysToFullWear.ToString("F0")), 4370);
        }
    }

    [StaticConstructorOnStartup]
    internal static class RackIcons
    {
        public static readonly Texture2D Specialization = ContentFinder<Texture2D>.Get("RCDC/UI/Specialization", true);
    }

    [StaticConstructorOnStartup]
    internal static class RackLedMaterials
    {
        public static readonly Material Green = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.25f, 1f, 0.4f));
        public static readonly Material Amber = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.72f, 0.15f));
        public static readonly Material Red = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.22f, 0.18f));
        public static readonly Material Blue = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.6f, 1f));
        public static readonly Material Off = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.08f, 0.09f, 0.1f));
    }

    /// <summary>
    /// A server rack. It produces data cartridges while powered, linked to a Network Core, cool
    /// enough and serviced. Everything it needs to remember (production progress, wear, the
    /// emergency-shutdown latch, message flags) is saved; the network link and headline status are
    /// re-derived after loading.
    /// </summary>
    public class CompServerRack : ThingComp
    {
        // ---- saved ------------------------------------------------------------------------------
        private float productionProgress;
        private float wear;
        private bool shutdownLatched;
        private int totalProduced;
        private bool warnedService;
        private bool warnedHalt;
        private int lastShutdownMessageTick = -999999;
        private ThingDef selectedOutput;

        // ---- derived at runtime (not saved) ------------------------------------------------------
        private CompPowerTrader power;
        private CompNetworkCore core;
        private NetworkFailure networkFailure;
        private RackStatus status = RackStatus.NoNetwork;
        private bool initialized;
        private bool powered;
        private bool networked;
        private bool monitored;
        private bool halted;
        private bool running;
        private bool outputBlocked;
        private float temperature = 21f;
        private float efficiency;
        private float temperatureEfficiency = 1f;
        private float wearEfficiency = 1f;
        private float monitoringEfficiency = 1f;
        private int lastFxTick;

        public CompProperties_ServerRack Props
        {
            get { return (CompProperties_ServerRack)props; }
        }

        public RackStatus Status
        {
            get { EnsureInitialized(); return status; }
        }

        public float Efficiency
        {
            get { EnsureInitialized(); return efficiency; }
        }

        public float Wear
        {
            get { return wear; }
        }

        public float Progress
        {
            get { return productionProgress; }
        }

        public int TotalProduced
        {
            get { return totalProduced; }
        }

        public bool IsShutdown
        {
            get { return shutdownLatched; }
        }

        public bool IsHalted
        {
            get { EnsureInitialized(); return halted; }
        }

        public bool IsThrottled
        {
            get { EnsureInitialized(); return !shutdownLatched && temperatureEfficiency < 0.999f; }
        }

        public bool IsRunning
        {
            get { EnsureInitialized(); return running; }
        }

        public bool IsMonitored
        {
            get { EnsureInitialized(); return monitored; }
        }

        public float Temperature
        {
            get { EnsureInitialized(); return temperature; }
        }

        public CompNetworkCore Core
        {
            get { EnsureInitialized(); return core; }
        }

        public NetworkFailure NetworkProblem
        {
            get { EnsureInitialized(); return networkFailure; }
        }

        /// <summary>
        /// The data type this rack currently produces. Defaults to <see cref="CompProperties_ServerRack.outputThing"/>
        /// and only ever changes if the player picks a different one with the Specialization command.
        /// </summary>
        public ThingDef OutputThing
        {
            get { return selectedOutput != null && Props.outputOptions != null && Props.outputOptions.Contains(selectedOutput) ? selectedOutput : Props.outputThing; }
        }

        /// <summary>Whether more than one data type is available to switch to at all (regardless of research).</summary>
        public bool HasSpecializationChoice
        {
            get { return Props.outputOptions != null && Props.outputOptions.Count > 1; }
        }

        public bool SwitchedOff
        {
            get
            {
                CompFlickable flick = parent.GetComp<CompFlickable>();
                return flick != null && !flick.SwitchIsOn;
            }
        }

        // ---- effective values: the XML numbers with the finished upgrade research applied -------------

        /// <summary>What the map's active AI core is doing to this rack (neutral when there is none).</summary>
        public AiModifiers Ai
        {
            get
            {
                MapComponent_DataCenterNetwork network = parent == null ? null : MapComponent_DataCenterNetwork.For(parent.Map);
                return network == null ? AiModifiers.Neutral : network.Ai;
            }
        }

        /// <summary>Temperature above which output is throttled.</summary>
        public float WarmTemperature
        {
            get { return Props.warmTemperature + RcdcUpgrades.Current.TemperatureTolerance + Ai.Tolerance; }
        }

        /// <summary>Temperature at which the rack shuts itself down.</summary>
        public float ShutdownTemperature
        {
            get { return Props.shutdownTemperature + RcdcUpgrades.Current.TemperatureTolerance + Ai.Tolerance; }
        }

        /// <summary>Temperature the room must fall to before a shut-down rack restarts.</summary>
        public float RestartTemperature
        {
            get { return Props.restartTemperature + RcdcUpgrades.Current.TemperatureTolerance + Ai.Tolerance; }
        }

        /// <summary>Production speed multiplier from upgrades and the AI (1 = none).</summary>
        public float UpgradeOutputFactor
        {
            get { return RcdcUpgrades.Current.OutputMultiplier * Ai.Output; }
        }

        /// <summary>
        /// Days until this rack asks for service at the wear rate it is running at now, or a negative number if it
        /// is not running (so there is no meaningful forecast).
        /// </summary>
        public float DaysUntilService()
        {
            EnsureInitialized();
            if (!running)
            {
                return -1f;
            }
            if (wear >= Props.serviceThreshold)
            {
                return 0f;
            }
            float perDay = 1f / Props.daysToFullWear * RcdcUpgrades.Current.WearRateMultiplier * Ai.Wear
                * (monitored ? 1f : UnmonitoredWearFactor) * (temperatureEfficiency < 0.999f ? Props.throttledWearFactor : 1f);
            return perDay <= 0f ? -1f : (Props.serviceThreshold - wear) / perDay;
        }

        /// <summary>Output factor of a rack that has no operations coverage, after upgrades.</summary>
        public float UnmonitoredOutputFactor
        {
            get { return Mathf.Clamp01(Props.unmonitoredOutputFactor + RcdcUpgrades.Current.UnmonitoredOutputBonus); }
        }

        /// <summary>Wear-rate multiplier of a rack that has no operations coverage, after upgrades (never below normal wear).</summary>
        public float UnmonitoredWearFactor
        {
            get { return Mathf.Max(1f, Props.unmonitoredWearFactor - RcdcUpgrades.Current.UnmonitoredWearReduction); }
        }

        // ---- lifecycle --------------------------------------------------------------------------

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
            initialized = false;
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
            core = null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref productionProgress, "rcdcProgress", 0f);
            Scribe_Values.Look(ref wear, "rcdcWear", 0f);
            Scribe_Values.Look(ref shutdownLatched, "rcdcShutdown", false);
            Scribe_Values.Look(ref totalProduced, "rcdcTotalProduced", 0);
            Scribe_Values.Look(ref warnedService, "rcdcWarnedService", false);
            Scribe_Values.Look(ref warnedHalt, "rcdcWarnedHalt", false);
            Scribe_Values.Look(ref lastShutdownMessageTick, "rcdcLastShutdownMsg", -999999);
            Scribe_Defs.Look(ref selectedOutput, "rcdcSelectedOutput");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (float.IsNaN(productionProgress) || productionProgress < 0f)
                {
                    productionProgress = 0f;
                }
                if (float.IsNaN(wear))
                {
                    wear = 0f;
                }
                productionProgress = Mathf.Min(productionProgress, 1f);
                wear = Mathf.Clamp01(wear);
                if (totalProduced < 0)
                {
                    totalProduced = 0;
                }
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized && parent != null && parent.Spawned)
            {
                Evaluate(false);
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent.Spawned)
            {
                Evaluate(true);
            }
        }

        // ---- efficiency curves ------------------------------------------------------------------

        public float TemperatureEfficiencyAt(float temp)
        {
            CompProperties_ServerRack p = Props;
            float warm = WarmTemperature;
            float shutdown = ShutdownTemperature;
            if (temp >= shutdown)
            {
                return 0f;
            }
            if (temp <= warm)
            {
                return 1f;
            }
            float t = Mathf.InverseLerp(warm, shutdown, temp);
            return Mathf.Lerp(1f, p.minTemperatureEfficiency, t);
        }

        public float WearEfficiencyAt(float w)
        {
            CompProperties_ServerRack p = Props;
            if (w >= 0.9999f)
            {
                return 0f;
            }
            if (w <= p.serviceThreshold)
            {
                return 1f;
            }
            float t = Mathf.InverseLerp(p.serviceThreshold, 1f, w);
            return Mathf.Lerp(1f, p.minWearEfficiency, t);
        }

        // ---- the simulation step ----------------------------------------------------------------

        /// <summary>
        /// Reads power, network, temperature and wear, derives the headline status, and (on a rare
        /// tick) applies wear, heat, power draw and production.
        /// </summary>
        private void Evaluate(bool applyEffects)
        {
            initialized = true;
            CompProperties_ServerRack p = Props;
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }

            powered = power != null && power.PowerOn;

            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            core = null;
            networkFailure = NetworkFailure.NoCoreInRange;
            if (network != null)
            {
                core = network.CoreFor(this, out networkFailure);
            }
            networked = core != null && core.IsOnline;
            if (core != null && !core.IsOnline)
            {
                networkFailure = NetworkFailure.CoreOffline;
            }
            monitored = core != null && core.IsMonitored;

            temperature = parent.AmbientTemperature;

            if (temperature >= ShutdownTemperature)
            {
                shutdownLatched = true;
            }
            else if (shutdownLatched && temperature <= RestartTemperature)
            {
                shutdownLatched = false;
                if (applyEffects)
                {
                    Messages.Message("RCDC_MsgRestart".Translate(parent.LabelShort), parent, MessageTypeDefOf.PositiveEvent, false);
                }
            }

            halted = wear >= 0.9999f;
            temperatureEfficiency = shutdownLatched ? 0f : TemperatureEfficiencyAt(temperature);
            wearEfficiency = WearEfficiencyAt(wear);
            monitoringEfficiency = monitored ? 1f : UnmonitoredOutputFactor;

            running = powered && networked && !shutdownLatched && !halted;
            efficiency = running ? temperatureEfficiency * wearEfficiency * monitoringEfficiency : 0f;

            outputBlocked = productionProgress >= 1f && !CanPlaceOutput();
            status = DeriveStatus();

            if (!applyEffects)
            {
                return;
            }

            ApplyPowerDraw();
            ApplyHeat();
            ApplyWear();
            ApplyProduction();
            status = DeriveStatus();
            AnnounceTransitions();
        }

        /// <summary>
        /// Picks the single headline status. Order matters: things that stop the rack outright come
        /// before things that merely slow it down.
        /// </summary>
        private RackStatus DeriveStatus()
        {
            if (!powered)
            {
                return RackStatus.NoPower;
            }
            if (!networked)
            {
                return RackStatus.NoNetwork;
            }
            if (shutdownLatched)
            {
                return RackStatus.TooHot;
            }
            if (halted)
            {
                return RackStatus.MaintenanceRequired;
            }
            if (outputBlocked)
            {
                return RackStatus.OutputFull;
            }
            if (temperatureEfficiency < 0.999f)
            {
                return RackStatus.TooHot;
            }
            if (wear >= Props.serviceThreshold)
            {
                return RackStatus.MaintenanceRequired;
            }
            return RackStatus.Operational;
        }

        private void ApplyPowerDraw()
        {
            // Only adjust the draw while the net is actually supplying power, so we never fight the
            // power grid's own on/off decision.
            if (power == null || !powered)
            {
                return;
            }
            float rated = power.Props.PowerConsumption;
            power.PowerOutput = -rated * (running ? 1f : Props.standbyPowerFactor);
        }

        private void ApplyHeat()
        {
            if (!powered)
            {
                return;
            }
            if (temperature >= Props.maxHeatPushTemperature)
            {
                return;
            }
            float factor = running ? 1f : Props.standbyHeatFactor;
            float energy = Props.heatPerSecond * RcdcUpgrades.Current.HeatMultiplier * Ai.Heat * factor * 4.1666665f;
            if (energy > 0f)
            {
                GenTemperature.PushHeat(parent, energy);
            }
        }

        private void ApplyWear()
        {
            if (!running)
            {
                return;
            }
            float perTick = 1f / (Props.daysToFullWear * 60000f);
            float multiplier = RcdcUpgrades.Current.WearRateMultiplier * Ai.Wear;
            if (!monitored)
            {
                multiplier *= UnmonitoredWearFactor;
            }
            if (temperatureEfficiency < 0.999f)
            {
                multiplier *= Props.throttledWearFactor;
            }
            wear = Mathf.Clamp01(wear + perTick * 250f * multiplier);
        }

        private void ApplyProduction()
        {
            if (running && !outputBlocked && efficiency > 0f)
            {
                productionProgress += 250f / Props.ticksPerCartridge * efficiency * UpgradeOutputFactor;
            }
            while (productionProgress >= 1f)
            {
                if (!TryOutput())
                {
                    productionProgress = 1f;
                    outputBlocked = true;
                    break;
                }
                productionProgress -= 1f;
            }
        }

        private void AnnounceTransitions()
        {
            int now = Find.TickManager.TicksGame;

            if (wear >= Props.serviceThreshold && !warnedService)
            {
                warnedService = true;
                Messages.Message("RCDC_MsgServiceDue".Translate(parent.LabelShort, (wear * 100f).ToString("F0")), parent, MessageTypeDefOf.CautionInput, false);
            }
            else if (wear < Props.serviceThreshold * 0.5f)
            {
                warnedService = false;
            }

            if (halted && !warnedHalt)
            {
                warnedHalt = true;
                Messages.Message("RCDC_MsgHalted".Translate(parent.LabelShort), parent, MessageTypeDefOf.NegativeEvent, false);
            }
            else if (!halted)
            {
                warnedHalt = false;
            }

            if (shutdownLatched && powered && now - lastShutdownMessageTick > 6000)
            {
                lastShutdownMessageTick = now;
                Messages.Message("RCDC_MsgShutdown".Translate(parent.LabelShort, temperature.ToStringTemperature("F0")), parent, MessageTypeDefOf.NegativeEvent, false);
                if (RcdcDefOf.RCDC_RackAlarm != null)
                {
                    RcdcDefOf.RCDC_RackAlarm.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
                }
            }
        }

        // ---- output -----------------------------------------------------------------------------

        private IntVec3 OutputCell
        {
            get { return parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position; }
        }

        private bool CanPlaceOutput()
        {
            Map map = parent.Map;
            IntVec3 cell = OutputCell;
            if (map == null || !cell.InBounds(map) || cell.Impassable(map))
            {
                return false;
            }
            ThingDef output = OutputThing;
            if (output == null)
            {
                return false;
            }
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.def.category != ThingCategory.Item)
                {
                    continue;
                }
                return t.def == output && t.stackCount + Props.cartridgesPerCycle <= t.def.stackLimit;
            }
            return true;
        }

        private bool TryOutput()
        {
            if (!CanPlaceOutput())
            {
                return false;
            }
            Thing product = ThingMaker.MakeThing(OutputThing);
            product.stackCount = Props.cartridgesPerCycle;
            Thing placed;
            IntVec3 cell = OutputCell;
            if (!GenPlace.TryPlaceThing(product, cell, parent.Map, ThingPlaceMode.Direct, out placed))
            {
                if (!product.Destroyed)
                {
                    product.Destroy();
                }
                return false;
            }
            totalProduced += Props.cartridgesPerCycle;
            if (RcdcDefOf.RCDC_CartridgeReady != null)
            {
                RcdcDefOf.RCDC_CartridgeReady.PlayOneShot(new TargetInfo(cell, parent.Map));
            }
            return true;
        }

        // ---- maintenance ------------------------------------------------------------------------

        /// <summary>Whether a colonist should (or, when forced, may) service this rack.</summary>
        public bool NeedsService(bool forced)
        {
            float threshold = forced ? Props.forcedServiceThreshold : Props.serviceThreshold;
            return wear >= threshold;
        }

        /// <summary>Reduce wear (used by operations shifts).</summary>
        public void ApplyWearRelief(float amount)
        {
            wear = Mathf.Clamp01(wear - Mathf.Max(0f, amount));
            if (wear < Props.serviceThreshold * 0.5f)
            {
                warnedService = false;
            }
            warnedHalt = warnedHalt && wear >= 0.9999f;
            initialized = false;
        }

        /// <summary>Called by the service job driver when a colonist finishes servicing the rack.</summary>
        public void CompleteService(Pawn worker)
        {
            wear = 0f;
            warnedService = false;
            warnedHalt = false;
            initialized = false;
            if (worker != null && parent.Spawned)
            {
                Messages.Message("RCDC_MsgServiced".Translate(worker.LabelShort, parent.LabelShort), parent, MessageTypeDefOf.TaskCompletion, false);
            }
        }

        // ---- inspection -------------------------------------------------------------------------

        public string StatusLabel
        {
            get { return ("RCDC_Status_" + Status).Translate(); }
        }

        private static Color StatusColor(RackStatus s)
        {
            switch (s)
            {
                case RackStatus.Operational:
                    return new Color(0.55f, 0.95f, 0.6f);
                case RackStatus.OutputFull:
                    return new Color(0.55f, 0.75f, 1f);
                case RackStatus.MaintenanceRequired:
                    return new Color(1f, 0.8f, 0.35f);
                default:
                    return new Color(1f, 0.45f, 0.4f);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
            {
                return null;
            }
            EnsureInitialized();
            StringBuilder sb = new StringBuilder();
            sb.Append("RCDC_RackStatus".Translate()).Append(": ").Append(StatusLabel.Colorize(StatusColor(status)));

            switch (status)
            {
                case RackStatus.NoPower:
                    sb.Append(" - ").Append((SwitchedOff ? "RCDC_DetailSwitchedOff" : "RCDC_DetailNoPower").Translate());
                    break;
                case RackStatus.NoNetwork:
                    sb.Append(" - ").Append(("RCDC_DetailNet_" + networkFailure).Translate());
                    break;
                case RackStatus.TooHot:
                    sb.Append(" - ").Append((shutdownLatched
                        ? "RCDC_DetailShutdown".Translate(RestartTemperature.ToStringTemperature("F0"))
                        : "RCDC_DetailThrottled".Translate((temperatureEfficiency * 100f).ToString("F0"))));
                    break;
                case RackStatus.MaintenanceRequired:
                    sb.Append(" - ").Append((halted ? "RCDC_DetailHalted" : "RCDC_DetailDegraded").Translate());
                    break;
                case RackStatus.OutputFull:
                    sb.Append(" - ").Append("RCDC_DetailOutputFull".Translate());
                    break;
            }

            if (core != null)
            {
                sb.Append('\n').Append("RCDC_RackNetwork".Translate(core.ConnectedCount, core.Capacity));
                if (!monitored)
                {
                    sb.Append(" - ").Append("RCDC_UnmonitoredShort".Translate());
                }
            }

            sb.Append('\n').Append("RCDC_RackEfficiency".Translate((efficiency * 100f).ToString("F0"),
                (temperatureEfficiency * 100f).ToString("F0"), (wearEfficiency * 100f).ToString("F0"),
                (monitoringEfficiency * 100f).ToString("F0")));
            sb.Append('\n').Append("RCDC_RackTemperature".Translate(temperature.ToStringTemperature("F1"),
                WarmTemperature.ToStringTemperature("F0")));
            sb.Append('\n').Append("RCDC_RackWear".Translate((wear * 100f).ToString("F0"),
                (Props.serviceThreshold * 100f).ToString("F0")));
            sb.Append('\n').Append("RCDC_RackProgress".Translate(Mathf.Min(100f, productionProgress * 100f).ToString("F0")));
            string upgrades = RcdcUpgrades.Current.RackSummary();
            if (upgrades != null)
            {
                sb.Append('\n').Append("RCDC_RackUpgrades".Translate(upgrades));
            }
            if (Ai.Forecast)
            {
                float days = DaysUntilService();
                if (days >= 0f)
                {
                    sb.Append('\n').Append("RCDC_RackForecast".Translate(AiVoice.Name, days.ToString("F1")));
                }
            }
            if (HasSpecializationChoice)
            {
                sb.Append('\n').Append("RCDC_RackProducing".Translate(OutputThing.LabelCap));
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (HasSpecializationChoice)
            {
                yield return new Command_Action
                {
                    icon = RackIcons.Specialization,
                    defaultLabel = "RCDC_SpecializationGizmo".Translate(),
                    defaultDesc = "RCDC_SpecializationGizmoDesc".Translate(OutputThing.LabelCap),
                    action = OpenSpecializationMenu
                };
            }
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: +25% wear",
                    action = delegate { wear = Mathf.Clamp01(wear + 0.25f); initialized = false; }
                };
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: 100% wear",
                    action = delegate { wear = 1f; initialized = false; }
                };
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: reset wear",
                    action = delegate { wear = 0f; initialized = false; }
                };
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: +50% progress",
                    action = delegate { productionProgress = Mathf.Min(1f, productionProgress + 0.5f); }
                };
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: force shutdown",
                    action = delegate { shutdownLatched = true; initialized = false; }
                };
            }
        }

        private void OpenSpecializationMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ThingDef option in Props.outputOptions)
            {
                ThingDef picked = option;
                bool available = option == Props.outputThing || option.IsResearchFinished;
                string label = option.LabelCap;
                if (option == OutputThing)
                {
                    label += " (" + "RCDC_SpecializationCurrent".Translate() + ")";
                }
                if (!available)
                {
                    options.Add(new FloatMenuOption(label + " - " + "RCDC_SpecializationLocked".Translate(), null));
                    continue;
                }
                options.Add(new FloatMenuOption(label, delegate { SetOutput(picked); }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>Sets which data type this rack produces. Keeps the current progress toward the next cartridge.</summary>
        public void SetOutput(ThingDef def)
        {
            if (def == null || def == OutputThing)
            {
                return;
            }
            selectedOutput = def;
            if (parent.Spawned)
            {
                Messages.Message("RCDC_MsgSpecialization".Translate(parent.LabelShort, def.LabelCap), parent, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        // ---- developer / self-test hooks (internal; used by debug actions and the self-test) ----------

        internal void DevSetWear(float value)
        {
            wear = Mathf.Clamp01(value);
            initialized = false;
        }

        internal void DevSetProgress(float value)
        {
            productionProgress = Mathf.Clamp(value, 0f, 1f);
        }

        internal void DevSetShutdown(bool value)
        {
            shutdownLatched = value;
            initialized = false;
        }

        /// <summary>Re-reads inputs and refreshes the status without applying wear, heat or production.</summary>
        internal void DevRefresh()
        {
            if (parent.Spawned)
            {
                Evaluate(false);
            }
        }

        /// <summary>Runs one full simulation step immediately (same as one rare tick).</summary>
        internal void DevStep()
        {
            if (parent.Spawned)
            {
                Evaluate(true);
            }
        }

        internal string DevSummary()
        {
            return parent.LabelShort + " @" + parent.Position + ": " + status + ", eff " + efficiency.ToString("P0")
                + " (heat " + temperatureEfficiency.ToString("P0") + ", wear " + wearEfficiency.ToString("P0")
                + ", monitor " + monitoringEfficiency.ToString("P0") + "), temp " + temperature.ToString("F1")
                + ", wear " + wear.ToString("P0") + ", progress " + productionProgress.ToString("P0")
                + ", shutdown " + shutdownLatched + ", core " + (core == null ? "none" : core.ConnectedCount + "/" + core.Capacity);
        }

        // ---- visuals ----------------------------------------------------------------------------

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned)
            {
                return;
            }
            EnsureInitialized();

            Vector3 center = parent.DrawPos;
            center.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            // The LED offsets below are authored for the default rotation (front facing south).
            Quaternion toFacing = Quaternion.AngleAxis(parent.Rotation.AsAngle, Vector3.up);
            int tick = Find.TickManager.TicksGame;
            bool blink = ((tick / 30) + parent.thingIDNumber) % 2 == 0;
            bool fast = ((tick / 12) + parent.thingIDNumber) % 2 == 0;

            Material led0;
            Material led1;
            Material led2;
            switch (status)
            {
                case RackStatus.Operational:
                    led0 = RackLedMaterials.Green;
                    led1 = fast ? RackLedMaterials.Green : RackLedMaterials.Off;
                    led2 = blink ? RackLedMaterials.Blue : RackLedMaterials.Green;
                    break;
                case RackStatus.NoPower:
                    led0 = led1 = led2 = RackLedMaterials.Off;
                    break;
                case RackStatus.NoNetwork:
                    led0 = RackLedMaterials.Off;
                    led1 = blink ? RackLedMaterials.Amber : RackLedMaterials.Off;
                    led2 = RackLedMaterials.Off;
                    break;
                case RackStatus.TooHot:
                    led0 = led1 = led2 = (fast || shutdownLatched) ? RackLedMaterials.Red : RackLedMaterials.Off;
                    break;
                case RackStatus.MaintenanceRequired:
                    led0 = halted ? RackLedMaterials.Off : RackLedMaterials.Green;
                    led1 = RackLedMaterials.Amber;
                    led2 = blink ? RackLedMaterials.Amber : RackLedMaterials.Off;
                    break;
                default: // OutputFull
                    led0 = RackLedMaterials.Green;
                    led1 = led2 = RackLedMaterials.Blue;
                    break;
            }

            DrawLed(center, toFacing, -0.27f, led0);
            DrawLed(center, toFacing, 0f, led1);
            DrawLed(center, toFacing, 0.27f, led2);

            // A puff of heat shimmer while the rack is struggling with temperature.
            if (status == RackStatus.TooHot && tick != lastFxTick && tick % 45 == 0)
            {
                lastFxTick = tick;
                FleckMaker.ThrowHeatGlow(parent.Position, parent.Map, 0.9f);
            }
        }

        private void DrawLed(Vector3 center, Quaternion toFacing, float xOffset, Material mat)
        {
            Vector3 offset = toFacing * new Vector3(xOffset, 0f, -0.36f);
            Matrix4x4 matrix = Matrix4x4.TRS(center + offset, Quaternion.identity, new Vector3(0.09f, 1f, 0.06f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
        }
    }
}
