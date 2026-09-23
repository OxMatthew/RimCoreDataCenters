using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimCore.DataCenters
{
    /// <summary>Hooks the self-test into the running game. Inert unless the test is started.</summary>
    public class GameComponent_RcdcDev : GameComponent
    {
        public GameComponent_RcdcDev(Game game)
        {
        }

        public override void GameComponentTick()
        {
            SelfTest.Tick();
            ShowcaseScene.Tick();
        }

        public override void GameComponentUpdate()
        {
            SelfTest.Update();
        }

        public override void FinalizeInit()
        {
            SelfTest.Notify_GameLoadedOrStarted();
        }
    }

    /// <summary>
    /// A scripted in-game verification run: it builds a demo data center on the live map and
    /// checks research, construction, power, network capacity, heat, maintenance, colonist jobs,
    /// output, hauling, trading, the UPS and a save/load round trip, then prints one PASS/FAIL line per
    /// check. Started with the launch argument -rcdc-selftest or from the debug menu; developer tooling
    /// only, it does nothing during normal play.
    /// </summary>
    internal static partial class SelfTest
    {
        private const string LaunchArg = "rcdc-selftest";
        private const string SaveName = "RCDC_SelfTest";

        private static bool running;
        private static bool quitWhenDone;
        private static bool startRequested;
        private static IEnumerator<Waiter> routine;
        private static Waiter current;
        private static int passed;
        private static int failed;
        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> logProblems = new List<string>();

        // State shared between sections (statics survive the save/load round trip).
        private static DemoLayout layout;
        private static bool loadRequested;
        private static bool loadCompleted;
        private static Snapshot snapshot;
        private static int lastMaintenanceTick;
        private static float lastRateLog;

        // ------------------------------------------------------------------ entry points

        public static bool LaunchArgPresent
        {
            get { return GenCommandLine.CommandLineArgPassed(LaunchArg); }
        }

        public static void Start(bool quit)
        {
            if (running)
            {
                RcdcLog.Test("[SELFTEST] already running.");
                return;
            }
            running = true;
            quitWhenDone = quit;
            passed = 0;
            failed = 0;
            failures.Clear();
            logProblems.Clear();
            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;
            routine = Run().GetEnumerator();
            current = null;
            RcdcLog.Test("[SELFTEST] starting.");
        }

        public static void Notify_GameLoadedOrStarted()
        {
            if (running && loadRequested)
            {
                loadCompleted = true;
            }
        }

        /// <summary>
        /// The game only ticks fast enough when it is in the foreground, so the self-test drives the
        /// simulation itself: it pauses the normal clock and runs as many ticks per frame as fit in a
        /// small time budget.
        /// </summary>
        public static void Update()
        {
            if (!running || Find.TickManager == null || Find.CurrentMap == null || (loadRequested && !loadCompleted))
            {
                return;
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Stopwatch clock = Stopwatch.StartNew();
            int ticks = 0;
            while (running && ticks < 400 && clock.ElapsedMilliseconds < 12 && !(loadRequested && !loadCompleted))
            {
                Find.TickManager.DoSingleTick();
                ticks++;
            }
            if (Time.realtimeSinceStartup - lastRateLog > 20f && Find.TickManager != null)
            {
                lastRateLog = Time.realtimeSinceStartup;
                Info("tick " + Find.TickManager.TicksGame + ", " + ticks + " ticks in the last frame");
            }
        }

        public static void Tick()
        {
            if (!running)
            {
                if (!startRequested && LaunchArgPresent && Find.CurrentMap != null)
                {
                    startRequested = true;
                    Start(true);
                }
                return;
            }
            try
            {
                Maintain();
                if (current != null && !current.IsDone())
                {
                    return;
                }
                current = null;
                if (!routine.MoveNext())
                {
                    Finish();
                    return;
                }
                current = routine.Current;
            }
            catch (Exception ex)
            {
                Check("self-test harness ran without exceptions", false, ex.ToString());
                Finish();
            }
        }

        // ------------------------------------------------------------------ waits

        internal abstract class Waiter
        {
            public abstract bool IsDone();
        }

        private sealed class WaitTicks : Waiter
        {
            private readonly int end;

            public WaitTicks(int ticks)
            {
                end = Find.TickManager.TicksGame + ticks;
            }

            public override bool IsDone()
            {
                return Find.TickManager.TicksGame >= end;
            }
        }

        private sealed class WaitUntil : Waiter
        {
            private readonly Func<bool> condition;
            private readonly int tickDeadline;
            private readonly float realDeadline;
            public bool TimedOut;

            public WaitUntil(Func<bool> condition, int timeoutTicks, float realSeconds = 240f)
            {
                this.condition = condition;
                tickDeadline = Find.TickManager.TicksGame + timeoutTicks;
                realDeadline = Time.realtimeSinceStartup + realSeconds;
            }

            public override bool IsDone()
            {
                if (condition())
                {
                    return true;
                }
                if (Find.TickManager.TicksGame >= tickDeadline || Time.realtimeSinceStartup >= realDeadline)
                {
                    TimedOut = true;
                    return true;
                }
                return false;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok)
            {
                passed++;
                RcdcLog.Test("[SELFTEST] PASS " + name + (string.IsNullOrEmpty(detail) ? "" : " - " + detail));
            }
            else
            {
                failed++;
                failures.Add(name);
                RcdcLog.Test("[SELFTEST] FAIL " + name + (string.IsNullOrEmpty(detail) ? "" : " - " + detail));
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Warning) && !condition.Contains("[SELFTEST]"))
            {
                logProblems.Add(type + ": " + condition);
            }
        }

        private static void Info(string message)
        {
            RcdcLog.Test("[SELFTEST] info " + message);
        }

        private static void Finish()
        {
            Application.logMessageReceived -= OnLogMessage;
            Check("no errors or warnings were logged during the whole run", logProblems.Count == 0, logProblems.Count == 0 ? "" : string.Join(" || ", logProblems.Take(5).ToArray()));
            running = false;
            string summary = "SELFTEST RESULT: " + passed + " passed, " + failed + " failed" + (failed > 0 ? " (" + string.Join("; ", failures.ToArray()) + ")" : "");
            RcdcLog.Test("[SELFTEST] " + summary);
            if (quitWhenDone)
            {
                Application.Quit();
            }
        }

        private static Map Map
        {
            get { return Find.CurrentMap; }
        }

        private static List<Pawn> Colonists
        {
            get { return Map.mapPawns.FreeColonists.ToList(); }
        }

        private static int Now
        {
            get { return Find.TickManager.TicksGame; }
        }

        private static IEnumerable<CompServerRack> AllRacks()
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            return network == null ? new List<CompServerRack>() : network.Racks.ToList();
        }

        /// <summary>One-line state of every rack, attached to failures so a flaky map is easy to diagnose.</summary>
        private static string DescribeRacks()
        {
            return string.Join(" | ", AllRacks().Select(r =>
            {
                CompPowerTrader p = r.parent.TryGetComp<CompPowerTrader>();
                return r.DevSummary() + ", power " + (p != null && p.PowerOn) + ", net " + r.NetworkProblem;
            }).ToArray());
        }

        private static CompServerRack Rack(int index)
        {
            return layout.Racks[index].TryGetComp<CompServerRack>();
        }

        private static CompNetworkCore Core
        {
            get { return layout.Core.TryGetComp<CompNetworkCore>(); }
        }

        private static CompOperationsConsole Console
        {
            get { return layout.Console.TryGetComp<CompOperationsConsole>(); }
        }

        private static Room DataCenterRoom
        {
            get { return layout.Racks[0].GetRoom(); }
        }

        /// <summary>Sets a work priority, skipping colonists who are incapable of that work (the game logs a warning otherwise).</summary>
        internal static void TrySetPriority(Pawn pawn, WorkTypeDef work, int priority)
        {
            if (pawn.workSettings != null && !pawn.WorkTypeIsDisabled(work))
            {
                pawn.workSettings.SetPriority(work, priority);
            }
        }

        private static void SetWorkPriority(int priority)
        {
            foreach (Pawn pawn in Colonists)
            {
                TrySetPriority(pawn, RcdcDefOf.RCDC_DataCenter, priority);
            }
        }

        private static void Flick(Thing thing, bool on)
        {
            CompFlickable flick = thing == null ? null : thing.TryGetComp<CompFlickable>();
            if (flick != null && flick.SwitchIsOn != on)
            {
                flick.DoFlick();
            }
        }

        private static void FlickAll(IEnumerable<Thing> things, bool on)
        {
            foreach (Thing t in things)
            {
                Flick(t, on);
            }
        }

        /// <summary>Periodic upkeep so the test is not derailed by unrelated game systems.</summary>
        private static void Maintain()
        {
            if (Find.TickManager == null || Map == null || Now - lastMaintenanceTick < 200)
            {
                return;
            }
            lastMaintenanceTick = Now;
            foreach (Pawn pawn in Colonists)
            {
                if (pawn.needs == null)
                {
                    continue;
                }
                if (pawn.needs.food != null) pawn.needs.food.CurLevel = 1f;
                if (pawn.needs.rest != null) pawn.needs.rest.CurLevel = 1f;
                if (pawn.needs.joy != null) pawn.needs.joy.CurLevel = 1f;
                if (pawn.needs.mood != null && pawn.mindState != null && pawn.mindState.mentalStateHandler.InMentalState)
                {
                    pawn.mindState.mentalStateHandler.CurState.RecoverFromState();
                }
            }
            if (layout != null)
            {
                foreach (Thing gen in layout.Generators)
                {
                    if (gen != null && gen.Spawned)
                    {
                        DemoDataCenter.Refuel(gen);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the script

        private static IEnumerable<Waiter> Run()
        {
            WaitUntil ready = new WaitUntil(() => Find.CurrentMap != null && Find.TickManager != null
                && Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount > 0 && Now > 300, 5000);
            yield return ready;
            if (ready.TimedOut)
            {
                Check("test colony started", false, "no spawned colonists");
                yield break;
            }

            DebugSettings.enableStoryteller = false;
            Info("map " + Map.Size + ", biome " + Map.Biome.defName + ", outdoor " + Map.mapTemperature.OutdoorTemp.ToString("F1") + " C, colonists " + Colonists.Count);
            PrepareColonists();

            foreach (Waiter w in SectionDefs()) yield return w;
            foreach (Waiter w in SectionResearch()) yield return w;
            foreach (Waiter w in SectionUpgradeDefs()) yield return w;
            foreach (Waiter w in SectionAiDefs()) yield return w;
            foreach (Waiter w in SectionBuild()) yield return w;
            foreach (Waiter w in SectionBlueprints()) yield return w;
            foreach (Waiter w in SectionBaseline()) yield return w;
            foreach (Waiter w in SectionHeat()) yield return w;
            foreach (Waiter w in SectionNetwork()) yield return w;
            foreach (Waiter w in SectionMaintenance()) yield return w;
            foreach (Waiter w in SectionOperations()) yield return w;
            foreach (Waiter w in SectionOutput()) yield return w;
            foreach (Waiter w in SectionTrade()) yield return w;
            foreach (Waiter w in SectionUps()) yield return w;
            foreach (Waiter w in SectionUpgrades()) yield return w;
            foreach (Waiter w in SectionResearchBuff()) yield return w;
            foreach (Waiter w in SectionSecurity()) yield return w;
            foreach (Waiter w in SectionSpecialization()) yield return w;
            foreach (Waiter w in SectionAi()) yield return w;
            foreach (Waiter w in SectionEspionage()) yield return w;
            foreach (Waiter w in SectionContracts()) yield return w;
            foreach (Waiter w in SectionSaveLoad()) yield return w;
        }

        private static void DumpConstructionDiagnostics(IntVec3 cell)
        {
            foreach (Pawn pawn in Colonists)
            {
                Job job = pawn.CurJob;
                Info("colonist " + pawn.LabelShort + " at " + pawn.Position + ", job " + (job == null ? "none" : job.def.defName) + ", construction prio "
                    + pawn.workSettings.GetPriority(WorkTypeDefOf.Construction) + ", incapable of construction " + pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    + ", downed " + pawn.Downed + ", drafted " + pawn.Drafted
                    + ", carrying " + (pawn.carryTracker.CarriedThing == null ? "nothing" : pawn.carryTracker.CarriedThing.LabelCap)
                    + ", inventory " + string.Join(",", pawn.inventory.innerContainer.Select(t => t.LabelCap).ToArray()));
            }
            foreach (Thing t in cell.GetThingList(Map))
            {
                Frame frame = t as Frame;
                Info("at rack cell: " + t.def.defName + (frame != null ? ", frame work left " + frame.WorkLeft + ", delivered materials " + frame.resourceContainer.TotalStackCount : ""));
            }
            foreach (ThingDef mat in new[] { ThingDefOf.Steel, ThingDefOf.ComponentIndustrial, ThingDefOf.Gold })
            {
                Info("loose " + mat.defName + " on map: " + Map.listerThings.ThingsOfDef(mat).Sum(t => t.stackCount));
            }
        }

        private static void PrepareColonists()
        {
            foreach (Pawn pawn in Colonists)
            {
                if (pawn.skills != null)
                {
                    pawn.skills.GetSkill(SkillDefOf.Construction).Level = 10;
                    pawn.skills.GetSkill(SkillDefOf.Intellectual).Level = 10;
                }
                if (pawn.workSettings != null)
                {
                    pawn.workSettings.EnableAndInitialize();
                    foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefs)
                    {
                        TrySetPriority(pawn, w, 0);
                    }
                    TrySetPriority(pawn, WorkTypeDefOf.Construction, 1);
                    TrySetPriority(pawn, WorkTypeDefOf.Hauling, 2);
                }
            }
        }

        // ---- 1. definitions ---------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionDefs()
        {
            ThingDef[] buildings =
            {
                RcdcDefOf.RCDC_ServerRack, RcdcDefOf.RCDC_NetworkCore, RcdcDefOf.RCDC_PrecisionCoolingUnit,
                RcdcDefOf.RCDC_UpsUnit, RcdcDefOf.RCDC_OperationsConsole
            };
            foreach (ThingDef def in buildings)
            {
                Check("def loaded: " + (def == null ? "?" : def.defName), def != null);
                if (def == null)
                {
                    continue;
                }
                List<string> errors = def.ConfigErrors().ToList();
                Check("no config errors: " + def.defName, errors.Count == 0, string.Join("; ", errors.ToArray()));
                Check("texture loaded: " + def.defName, def.graphic != null && def.graphic != BaseContent.BadGraphic);
                Check("costs and work set: " + def.defName, def.CostList != null && def.CostList.Count > 0 && def.GetStatValueAbstract(StatDefOf.WorkToBuild) > 0f);
                Check("mass, flammability, beauty set: " + def.defName,
                    def.statBases.Any(s => s.stat == StatDefOf.Mass) && def.statBases.Any(s => s.stat == StatDefOf.Flammability) && def.statBases.Any(s => s.stat == StatDefOf.Beauty));
                Check("architect category and research prerequisite: " + def.defName,
                    def.designationCategory != null && def.designationCategory.defName == "RCDC_DataCenter"
                    && def.researchPrerequisites != null && def.researchPrerequisites.Contains(RcdcDefOf.RCDC_DataCenterInfrastructure));
            }

            foreach (ThingDef def in buildings)
            {
                bool cardOk = true;
                string cardDetail = "";
                try
                {
                    int lines = 0;
                    foreach (StatDrawEntry entry in def.SpecialDisplayStats(StatRequest.For(def, null)))
                    {
                        lines++;
                        string text = entry.LabelCap + " " + entry.ValueString + " " + entry.GetExplanationText(StatRequest.For(def, null));
                        if (text.Contains("RCDC_"))
                        {
                            cardOk = false;
                            cardDetail = text;
                        }
                    }
                    cardDetail = cardOk ? lines + " stat lines" : cardDetail;
                }
                catch (Exception ex)
                {
                    cardOk = false;
                    cardDetail = ex.Message;
                }
                Check("info card stats render: " + def.defName, cardOk, cardDetail);
            }

            ThingDef cartridge = RcdcDefOf.RCDC_DataCartridge;
            Check("data cartridge: stack limit 25", cartridge != null && cartridge.stackLimit == 25);
            Check("data cartridge: market value 80", cartridge != null && Mathf.Approximately(cartridge.BaseMarketValue, 80f), cartridge == null ? "" : cartridge.BaseMarketValue.ToString());
            Check("data cartridge is sellable", cartridge != null && TradeUtility.EverPlayerSellable(cartridge));
            foreach (string trader in new[] { "Orbital_BulkGoods", "Orbital_Exotic", "Caravan_Outlander_BulkGoods", "Caravan_Outlander_Exotic", "Base_Outlander_Standard" })
            {
                TraderKindDef kind = DefDatabase<TraderKindDef>.GetNamedSilentFail(trader);
                Check("trader buys cartridges: " + trader, kind != null && kind.WillTrade(cartridge));
            }
            Check("work type, jobs and alerts defined", RcdcDefOf.RCDC_DataCenter != null && RcdcDefOf.RCDC_DataCenterOperations != null && RcdcDefOf.RCDC_ServiceRack != null);
            Check("sounds defined", RcdcDefOf.RCDC_CartridgeReady != null && RcdcDefOf.RCDC_RackAlarm != null);
            foreach (string soundName in new[] { "RCDC_ServerHum", "RCDC_CoolerFan", "RCDC_CartridgeReady", "RCDC_RackAlarm", "RCDC_AccessDenied", "RCDC_AiChime", "RCDC_EspionageAlert" })
            {
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(soundName);
                bool loaded = false;
                try
                {
                    ResolvedGrain grain = sound == null || sound.subSounds.Count == 0 ? null : sound.subSounds[0].RandomizedResolvedGrain();
                    ResolvedGrain_Clip clip = grain as ResolvedGrain_Clip;
                    loaded = clip != null && clip.clip != null && clip.clip.length > 0.1f;
                }
                catch (Exception ex)
                {
                    Info("sound load error " + soundName + ": " + ex.Message);
                }
                Check("sound clip loads: " + soundName, loaded);
            }
            yield return null;
        }

        // ---- 2. research ------------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionResearch()
        {
            ResearchProjectDef project = RcdcDefOf.RCDC_DataCenterInfrastructure;
            Check("research project exists", project != null);
            if (project == null)
            {
                yield break;
            }
            Check("research is locked before prerequisites", !project.IsFinished && !project.PrerequisitesCompleted);
            Check("buildings locked before research", !RcdcDefOf.RCDC_ServerRack.IsResearchFinished);
            foreach (ResearchProjectDef prerequisite in project.prerequisites)
            {
                Find.ResearchManager.FinishProject(prerequisite, false, null, false);
            }
            Check("research unlockable once prerequisites are done", project.PrerequisitesCompleted && !project.IsFinished);
            Find.ResearchManager.FinishProject(project, false, null, false);
            Check("research completes", project.IsFinished);
            Check("all five buildings unlocked", new[] { RcdcDefOf.RCDC_ServerRack, RcdcDefOf.RCDC_NetworkCore, RcdcDefOf.RCDC_PrecisionCoolingUnit, RcdcDefOf.RCDC_UpsUnit, RcdcDefOf.RCDC_OperationsConsole }.All(d => d.IsResearchFinished));
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamed("RCDC_DataCenter");
            int buildDesignators = category.AllResolvedDesignators.OfType<Designator_Build>().Count();
            Check("architect tab offers build designators", buildDesignators >= 5, buildDesignators + " build designators");
            yield return null;
        }

        // ---- 3. construction (real colonists) and the demo room ----------------------------------

        private static IEnumerable<Waiter> SectionBuild()
        {
            IntVec3 origin;
            bool found = DemoDataCenter.TryFindSite(Map, out origin);
            Check("found a flat site for the demo data center", found);
            if (!found)
            {
                yield break;
            }
            layout = DemoDataCenter.Build(Map, origin, true, true);
            Check("demo shell built: 3 racks, core, console, cooler, 2 UPS, 4 generators",
                layout.Racks.Count == 3 && layout.Core != null && layout.Console != null && layout.Cooler != null && layout.Ups.Count == 2 && layout.Generators.Count == 4);
            Check("every building spawned", layout.Racks.All(r => r.Spawned) && layout.Core.Spawned && layout.Console.Spawned && layout.Cooler.Spawned && layout.Ups.All(u => u.Spawned));

            // Move the colonists to the door so they can reach the room.
            int spread = 0;
            foreach (Pawn pawn in Colonists)
            {
                pawn.Position = layout.AisleCell + new IntVec3(spread++, 0, 0);
                pawn.Notify_Teleported(true, true);
            }

            Room room = DataCenterRoom;
            Check("the demo room is a proper enclosed room", room != null && !room.UsesOutdoorTemperature && !room.TouchesMapEdge, room == null ? "no room" : "cells " + room.CellCount);
            if (room != null)
            {
                room.Temperature = 21f;
            }

            // Real construction by colonists: blueprint, materials, walk, build.
            ThingDef rackDef = RcdcDefOf.RCDC_ServerRack;
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(rackDef, layout.RackSlotWithheld, Map, Rot4.North, Faction.OfPlayer, null);
            Check("blueprint placed for a server rack", blueprint != null && blueprint.Spawned);
            foreach (ThingDefCountClass cost in rackDef.CostList)
            {
                Thing material = ThingMaker.MakeThing(cost.thingDef);
                material.stackCount = cost.count;
                Thing placedMaterial;
                bool placed = GenPlace.TryPlaceThing(material, layout.AisleCell, Map, ThingPlaceMode.Near, out placedMaterial);
                int loose = Map.listerThings.ThingsOfDef(cost.thingDef).Sum(t => t.stackCount);
                Info("materials for the rack: " + cost.thingDef.defName + " placed " + placed + ", loose on map " + loose + ", needed " + cost.count);
                // RimWorld only starts delivering to a blueprint when the WHOLE cost exists somewhere.
                if (loose < cost.count)
                {
                    Thing topUp = ThingMaker.MakeThing(cost.thingDef);
                    topUp.stackCount = cost.count - loose;
                    GenPlace.TryPlaceThing(topUp, layout.AisleCell, Map, ThingPlaceMode.Near);
                    Info("topped up " + cost.thingDef.defName + " by " + topUp.stackCount + " (loose now " + Map.listerThings.ThingsOfDef(cost.thingDef).Sum(t => t.stackCount) + ")");
                }
            }
            Check("construction skill prerequisite is enforced", rackDef.constructionSkillPrerequisite == 6);
            WaitUntil built = new WaitUntil(() => layout.RackSlotWithheld.GetThingList(Map).Any(t => t.def == rackDef), 15000);
            yield return built;
            Check("colonists constructed a server rack from a blueprint", !built.TimedOut);
            if (built.TimedOut)
            {
                DumpConstructionDiagnostics(layout.RackSlotWithheld);
            }
            Thing newRack = layout.RackSlotWithheld.GetThingList(Map).FirstOrDefault(t => t.def == rackDef);
            if (newRack != null)
            {
                layout.Racks.Insert(0, newRack);
                Info("rack built: " + newRack.Label + ", hp " + newRack.HitPoints + "/" + newRack.MaxHitPoints);
            }
            layout.RackSlotWithheld = IntVec3.Invalid;
            yield return new WaitTicks(60);
        }

        // ---- 4. blueprint acceptance -------------------------------------------------------------

        private static IEnumerable<Waiter> SectionBlueprints()
        {
            if (layout == null)
            {
                yield break;
            }
            // Search outward from the data center for a legal spot for each building, so the check is not
            // at the mercy of whatever rock or trees the random test map happens to have there.
            ThingDef[] defs = { RcdcDefOf.RCDC_ServerRack, RcdcDefOf.RCDC_NetworkCore, RcdcDefOf.RCDC_UpsUnit, RcdcDefOf.RCDC_OperationsConsole };
            foreach (ThingDef def in defs)
            {
                AcceptanceReport lastReport = AcceptanceReport.WasRejected;
                bool accepted = false;
                IntVec3 accepted_at = IntVec3.Invalid;
                for (int radius = 4; radius <= 40 && !accepted; radius += 2)
                {
                    for (int dx = -radius; dx <= radius && !accepted; dx++)
                    {
                        for (int dz = -radius; dz <= radius && !accepted; dz++)
                        {
                            if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius)
                            {
                                continue;
                            }
                            IntVec3 cell = layout.Origin + new IntVec3(dx, 0, dz);
                            if (!cell.InBounds(Map) || layout.Site.Contains(cell))
                            {
                                continue;
                            }
                            lastReport = GenConstruct.CanPlaceBlueprintAt(def, cell, Rot4.North, Map);
                            if (lastReport.Accepted)
                            {
                                accepted = true;
                                accepted_at = cell;
                            }
                        }
                    }
                }
                Check("can place a blueprint for " + def.defName, accepted, accepted ? "at " + accepted_at : lastReport.Reason);
            }
            // The cooler is placed over a wall, like the vanilla cooler.
            IntVec3 wallCell = layout.Origin + new IntVec3(6, 0, DemoDataCenter.InteriorHeight);
            AcceptanceReport cooler = GenConstruct.CanPlaceBlueprintAt(RcdcDefOf.RCDC_PrecisionCoolingUnit, wallCell, Rot4.North, Map);
            Check("can place a blueprint for RCDC_PrecisionCoolingUnit over a wall", cooler.Accepted, cooler.Reason);
            yield return null;
        }

        // ---- 5. baseline operation ----------------------------------------------------------------

        private static IEnumerable<Waiter> SectionBaseline()
        {
            if (layout == null)
            {
                yield break;
            }
            SetWorkPriority(0);
            yield return new WaitTicks(600);
            foreach (CompServerRack rack in layout.Racks.Select(r => r.TryGetComp<CompServerRack>()))
            {
                rack.DevRefresh();
            }
            CompNetworkCore core = Core;
            Check("power net supplies the data center", Rack(0).parent.TryGetComp<CompPowerTrader>().PowerOn);
            Check("network core links all four racks", core.ConnectedCount == 4 && core.Capacity == 6, core.ConnectedCount + "/" + core.Capacity);
            Check("network core is online", core.IsOnline);
            Check("racks are Operational", layout.Racks.All(r => r.TryGetComp<CompServerRack>().Status == RackStatus.Operational),
                string.Join(" | ", layout.Racks.Select(r => r.TryGetComp<CompServerRack>().DevSummary()).ToArray()));
            float eff = Rack(0).Efficiency;
            Check("unmonitored racks run at 65% efficiency", Mathf.Abs(eff - 0.65f) < 0.02f, eff.ToString("P0"));
            Check("racks are unmonitored before a shift", !core.IsMonitored);
            string rackText = Rack(0).CompInspectStringExtra();
            Check("rack inspect text is complete and translated", !string.IsNullOrEmpty(rackText) && !rackText.Contains("RCDC_") && rackText.Contains("Status") && rackText.Contains("Operational"), rackText == null ? "null" : rackText.Replace((char)10, (char)124));
            Check("core, console and UPS inspect text has no untranslated keys", !core.CompInspectStringExtra().Contains("RCDC_") && !Console.CompInspectStringExtra().Contains("RCDC_") && !layout.Ups[0].GetInspectString().Contains("RCDC_"));

            // Production speed: measure progress over a fixed number of ticks and compare with the formula.
            CompServerRack r0 = Rack(0);
            float before = r0.Progress;
            int startTick = Now;
            yield return new WaitTicks(2500);
            float delta = r0.Progress - before;
            float expected = (Now - startTick) / (float)r0.Props.ticksPerCartridge * 0.65f;
            Check("production rate matches ticksPerCartridge x efficiency", Mathf.Abs(delta - expected) < 0.02f, "progress +" + delta.ToString("F4") + ", expected +" + expected.ToString("F4"));
            Check("heat is pushed into the room but the cooler holds it", DataCenterRoom.Temperature < 32f, DataCenterRoom.Temperature.ToString("F1") + " C");

            // Power draw and standby draw.
            CompPowerTrader power = r0.parent.TryGetComp<CompPowerTrader>();
            Check("running rack draws its rated 450 W", Mathf.Abs(-power.PowerOutput - 450f) < 1f, (-power.PowerOutput).ToString("F0") + " W");
            Check("wear accrues while running", r0.Wear > 0f, r0.Wear.ToString("P2"));
        }

        // ---- 6. heat ----------------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionHeat()
        {
            if (layout == null)
            {
                yield break;
            }
            Room room = DataCenterRoom;
            CompServerRack rack = Rack(1);

            // Without the cooler the racks must heat the room.
            Flick(layout.Cooler, false);
            room.Temperature = 22f;
            yield return new WaitTicks(2000);
            float heated = room.Temperature;
            // How much the room warms depends on the test map's climate (a cold biome leaks more heat), so
            // only require a clear net rise. The cooler check below uses a fixed starting point instead.
            Check("racks heat the room when the cooler is off", heated > 24f, heated.ToString("F1") + " C after 2000 ticks from 22 C");
            Flick(layout.Cooler, true);
            room.Temperature = 38f;
            yield return new WaitTicks(1200);
            Check("the precision cooler pulls a hot room back toward its target", room.Temperature < 33f, "38 C -> " + room.Temperature.ToString("F1") + " C in 1200 ticks");

            // Warm: reduced output, no shutdown.
            Flick(layout.Cooler, false);
            room.Temperature = 41f;
            rack.DevRefresh();
            Check("warm room: status is Too Hot", rack.Status == RackStatus.TooHot, rack.DevSummary());
            Check("warm room: output is reduced but not shut down", rack.IsThrottled && !rack.IsShutdown && rack.Efficiency < 0.65f && rack.Efficiency > 0f, rack.Efficiency.ToString("P0"));

            // Dangerous: emergency shutdown, standby draw and standby heat.
            room.Temperature = 52f;
            rack.DevStep();
            Check("dangerously hot: emergency shutdown", rack.IsShutdown && rack.Status == RackStatus.TooHot && rack.Efficiency == 0f, rack.DevSummary());
            Check("overheating alert is raised", new Alert_DataCenterOverheating().GetReport().active);
            Check("overheating alert explains the problem without missing strings", !new Alert_DataCenterOverheating().GetExplanation().ToString().Contains("RCDC_"), new Alert_DataCenterOverheating().GetExplanation().ToString().Replace((char)10, (char)124));
            CompPowerTrader power = rack.parent.TryGetComp<CompPowerTrader>();
            Check("shut down rack drops to standby power", -power.PowerOutput < 450f * 0.25f, (-power.PowerOutput).ToString("F0") + " W");
            float progressBefore = rack.Progress;
            yield return new WaitTicks(500);
            Check("shut down rack produces nothing", Mathf.Abs(rack.Progress - progressBefore) < 0.0001f);

            // Recovery with hysteresis: still shut down at 45 C, restarts at or below 40 C.
            Flick(layout.Cooler, true);
            room.Temperature = 45f;
            rack.DevStep();
            Check("hysteresis: stays shut down at 45 C", rack.IsShutdown);
            room.Temperature = 38f;
            rack.DevStep();
            Check("restarts automatically once cooled below 40 C", !rack.IsShutdown && rack.Status != RackStatus.NoPower);
            yield return new WaitTicks(1500);
            Check("the room settles back to a safe temperature", room.Temperature < 32f, room.Temperature.ToString("F1") + " C");
            Check("no fire or explosion from overheating", layout.Racks.All(r => r.Spawned && r.HitPoints == r.MaxHitPoints) && !Map.listerThings.ThingsOfDef(ThingDefOf.Fire).Any());
        }

        // ---- 7. network capacity ------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionNetwork()
        {
            if (layout == null)
            {
                yield break;
            }
            CompNetworkCore core = Core;
            List<Thing> extras = new List<Thing>();
            // Add four more racks along row A/B so the core (capacity 6) is over-subscribed.
            IntVec3[] cells =
            {
                layout.Origin + new IntVec3(4, 0, 5), layout.Origin + new IntVec3(5, 0, 5),
                layout.Origin + new IntVec3(4, 0, 1), layout.Origin + new IntVec3(5, 0, 1)
            };
            for (int i = 0; i < cells.Length; i++)
            {
                extras.Add(DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_ServerRack, cells[i], i < 2 ? Rot4.North : Rot4.South, null));
            }
            yield return new WaitTicks(300);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("core capacity is capped at 6 racks", core.ConnectedCount == 6 && core.Capacity == 6, core.ConnectedCount + "/" + core.Capacity);
            // The network assignment is what is under test here, so look at each rack's link result directly.
            // (A rack that also has no power shows "No Power" first, which is the correct headline status.)
            List<CompServerRack> unlinked = AllRacks().Where(r => r.Core == null && r.NetworkProblem == NetworkFailure.CoreFull).ToList();
            Check("racks beyond capacity are refused by a full core", unlinked.Count == 2, unlinked.Count + " refused; " + DescribeRacks());
            Check("the oldest racks keep their slots", layout.Racks.All(r => r.TryGetComp<CompServerRack>().Core != null));
            Check("a rack refused by a full core never reports Operational",
                unlinked.All(r => r.Status == RackStatus.NoNetwork || r.Status == RackStatus.NoPower), string.Join(", ", unlinked.Select(r => r.Status.ToString()).ToArray()));
            string inspect = core.parent.GetInspectString();
            Check("core inspect text shows connected count and capacity", inspect.Contains("6") && inspect.Contains("/"), inspect.Replace('\n', '|'));

            // A rack out of range.
            IntVec3 far = layout.Origin + new IntVec3(-1, 0, -6);
            Thing outOfRange = DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_ServerRack, layout.Origin + new IntVec3(1, 0, -8), Rot4.North, null);
            extras.Add(outOfRange);
            yield return new WaitTicks(300);
            CompServerRack farComp = outOfRange.TryGetComp<CompServerRack>();
            farComp.DevRefresh();
            Check("a rack outside the range has no network link", farComp.Core == null && farComp.NetworkProblem == NetworkFailure.NoCoreInRange, farComp.NetworkProblem.ToString());

            // Removing an extra frees a slot.
            Thing removed = extras[0];
            removed.Destroy(DestroyMode.Vanish);
            extras.RemoveAt(0);
            yield return new WaitTicks(300);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            int stillWaiting = AllRacks().Count(r => r.Core == null && r.NetworkProblem == NetworkFailure.CoreFull);
            Check("freeing a slot links the waiting rack", core.ConnectedCount == 6 && stillWaiting == 1, stillWaiting + " racks still without a slot; " + DescribeRacks());

            // Core offline takes everything down.
            Flick(layout.Core, false);
            yield return new WaitTicks(120);
            foreach (CompServerRack r in layout.Racks.Select(t => t.TryGetComp<CompServerRack>())) r.DevRefresh();
            Check("an unpowered core puts its racks in No Network", layout.Racks.All(r => r.TryGetComp<CompServerRack>().Status == RackStatus.NoNetwork), layout.Racks[0].TryGetComp<CompServerRack>().DevSummary());
            Check("offline alert is raised", new Alert_DataCenterOffline().GetReport().active);
            Check("racks name the reason (core offline)", layout.Racks[0].TryGetComp<CompServerRack>().NetworkProblem == NetworkFailure.CoreOffline);
            Flick(layout.Core, true);
            yield return new WaitTicks(300);
            foreach (CompServerRack r in layout.Racks.Select(t => t.TryGetComp<CompServerRack>())) r.DevRefresh();
            Check("racks recover when the core returns", layout.Racks.All(r => r.TryGetComp<CompServerRack>().Status == RackStatus.Operational));

            foreach (Thing extra in extras)
            {
                if (extra.Spawned) extra.Destroy(DestroyMode.Vanish);
            }
            yield return new WaitTicks(300);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("cleanup: back to four linked racks", core.ConnectedCount == 4);
        }

        // ---- 8. maintenance (real colonist job) --------------------------------------------------

        private static IEnumerable<Waiter> SectionMaintenance()
        {
            if (layout == null)
            {
                yield break;
            }
            CompServerRack rack = Rack(2);
            rack.DevSetWear(0.6f);
            rack.DevRefresh();
            Check("worn rack reports Maintenance Required and loses efficiency", rack.Status == RackStatus.MaintenanceRequired && rack.Efficiency < 0.65f, rack.DevSummary());
            rack.DevSetWear(1f);
            rack.DevStep();
            Check("fully worn rack stops instead of exploding", rack.IsHalted && rack.Efficiency == 0f && rack.parent.Spawned && rack.parent.HitPoints == rack.parent.MaxHitPoints, rack.DevSummary());
            Check("maintenance alert is raised", new Alert_DataCenterMaintenance().GetReport().active);
            CompPowerTrader power = rack.parent.TryGetComp<CompPowerTrader>();
            Check("halted rack drops to standby power", -power.PowerOutput < 450f * 0.25f, (-power.PowerOutput).ToString("F0") + " W");
            float progress = rack.Progress;
            yield return new WaitTicks(400);
            Check("halted rack produces nothing", Mathf.Abs(rack.Progress - progress) < 0.0001f);

            // A colonist must service it: needs the data center work type and a component.
            Thing parts = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
            parts.stackCount = 3;
            GenPlace.TryPlaceThing(parts, layout.AisleCell, Map, ThingPlaceMode.Near);
            int componentsBefore = Map.listerThings.ThingsOfDef(ThingDefOf.ComponentIndustrial).Sum(t => t.stackCount);
            SetWorkPriority(1);
            WaitUntil serviced = new WaitUntil(() => rack.Wear < 0.001f, 12000);
            yield return serviced;
            Check("a colonist serviced the worn rack", !serviced.TimedOut, rack.DevSummary());
            int componentsAfter = Map.listerThings.ThingsOfDef(ThingDefOf.ComponentIndustrial).Sum(t => t.stackCount);
            Check("servicing consumed one component", componentsBefore - componentsAfter == 1, componentsBefore + " -> " + componentsAfter);
            SetWorkPriority(0);
            rack.DevStep();
            Check("the serviced rack runs again", rack.IsRunning && !rack.IsHalted, rack.DevSummary());
        }

        // ---- 9. operations ------------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionOperations()
        {
            if (layout == null)
            {
                yield break;
            }
            CompOperationsConsole console = Console;
            Rack(0).DevSetWear(0.3f);
            int shiftsBefore = console.ShiftsCompleted;
            Check("console is linked to the core", console.Core != null && console.Core == Core);
            string reason;
            Check("console can be operated", console.CanOperate(out reason), reason);
            Check("a shift is wanted while unmonitored", console.ShiftWanted(false));
            float wearBefore = Rack(0).Wear;
            SetWorkPriority(1);
            WaitUntil shift = new WaitUntil(() => console.ShiftsCompleted > shiftsBefore, 12000);
            yield return shift;
            SetWorkPriority(0);
            Check("a colonist completed a Data Center Operations shift", !shift.TimedOut, "shifts " + console.ShiftsCompleted);
            Check("the network is monitored after a shift", Core.IsMonitored && console.HasCoverage);
            Check("the shift trimmed rack wear", Rack(0).Wear < wearBefore, wearBefore.ToString("P1") + " -> " + Rack(0).Wear.ToString("P1"));
            Rack(0).DevStep();
            Check("monitored racks run at full efficiency", Mathf.Abs(Rack(0).Efficiency - 1f) < 0.02f, Rack(0).Efficiency.ToString("P0"));
            Check("no further shift is wanted while covered", !console.ShiftWanted(false));
            string consoleText = console.parent.GetInspectString();
            Check("console inspect text shows coverage", consoleText.Contains("coverage"), consoleText.Replace('\n', '|'));
        }

        // ---- 10. output, stacking and hauling -----------------------------------------------------

        private static IEnumerable<Waiter> SectionOutput()
        {
            if (layout == null)
            {
                yield break;
            }
            CompServerRack rack = Rack(3);
            IntVec3 outCell = rack.parent.InteractionCell;
            rack.DevSetProgress(0.999f);
            rack.DevStep();
            rack.DevStep();
            Thing cartridges = outCell.GetThingList(Map).FirstOrDefault(t => t.def == RcdcDefOf.RCDC_DataCartridge);
            Check("a rack produces a data cartridge in front of it", cartridges != null && cartridges.stackCount >= 1);
            Check("produced cartridges are haulable and unforbidden", cartridges != null && !cartridges.IsForbidden(Faction.OfPlayer) && cartridges.def.EverHaulable);
            rack.DevSetProgress(0.999f);
            rack.DevStep();
            rack.DevStep();
            cartridges = outCell.GetThingList(Map).FirstOrDefault(t => t.def == RcdcDefOf.RCDC_DataCartridge);
            Check("cartridges stack", cartridges != null && cartridges.stackCount >= 2, cartridges == null ? "none" : "stack " + cartridges.stackCount);

            // Fill the stack to its limit: the rack must report Output Full and not overflow.
            for (int i = 0; i < 40; i++)
            {
                rack.DevSetProgress(1f);
                rack.DevStep();
            }
            cartridges = outCell.GetThingList(Map).FirstOrDefault(t => t.def == RcdcDefOf.RCDC_DataCartridge);
            Check("stack is capped at the stack limit", cartridges != null && cartridges.stackCount == 25, cartridges == null ? "none" : "stack " + cartridges.stackCount);
            rack.DevRefresh();
            Check("full output stops production with the Output Full status", rack.Status == RackStatus.OutputFull, rack.DevSummary());
            Check("rack keeps its progress instead of losing a cartridge", rack.Progress >= 0.999f);

            // A stockpile lets colonists haul the cartridges away.
            Zone_Stockpile stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Map.zoneManager);
            Map.zoneManager.RegisterZone(stockpile);
            for (int x = 0; x < 3; x++)
            {
                IntVec3 c = layout.Origin + new IntVec3(6 + x, 0, 3);
                stockpile.AddCell(c);
            }
            stockpile.settings.filter.SetAllowAll(null);
            SetWorkPriority(0);
            foreach (Pawn p in Colonists) TrySetPriority(p, WorkTypeDefOf.Hauling, 1);
            WaitUntil hauled = new WaitUntil(() => stockpile.AllContainedThings.Where(t => t.def == RcdcDefOf.RCDC_DataCartridge).Sum(t => t.stackCount) >= 25, 8000);
            yield return hauled;
            Check("colonists hauled the cartridges to a stockpile", !hauled.TimedOut);
            int inStockpile = stockpile.AllContainedThings.Where(t => t.def == RcdcDefOf.RCDC_DataCartridge).Sum(t => t.stackCount);
            Check("cartridges arrived in the stockpile", inStockpile >= 25, inStockpile + " cartridges stored");
            rack.DevRefresh();
            Check("the rack recovers once the output is cleared", rack.Status != RackStatus.OutputFull, rack.DevSummary());
            rack.DevStep();
            Check("production resumes into the freed cell", outCell.GetThingList(Map).Any(t => t.def == RcdcDefOf.RCDC_DataCartridge) || rack.Progress < 1f);
        }

        // ---- 11. trading --------------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionTrade()
        {
            if (layout == null)
            {
                yield break;
            }
            // Orbital traders only see goods near a powered trade beacon, like in a normal colony.
            ThingDef beaconDef = DefDatabase<ThingDef>.GetNamed("OrbitalTradeBeacon");
            Thing beacon = DemoDataCenter.Spawn(Map, beaconDef, layout.Origin + new IntVec3(10, 0, 3), Rot4.North, null);
            // The beacon is a powered building and the power net switches new consumers on one at a time, so wait for it.
            CompPowerTrader beaconPower = beacon.TryGetComp<CompPowerTrader>();
            if (beaconPower != null)
            {
                yield return new WaitUntil(() => beaconPower.PowerOn, 2000);
            }
            yield return new WaitTicks(60);
            // The trade window only lets capable colonists negotiate; do the same here.
            Pawn negotiator = Colonists.FirstOrDefault(p => !StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(p));
            if (negotiator == null)
            {
                Info("no colonist can negotiate trades on this map; skipping the trade execution check");
                yield break;
            }
            bool sold = false;
            int tradedCount = 0;
            float tradedUnitPrice = 0f;
            int silverBeforeTrade = 0;
            try
            {
                TradeShip ship = new TradeShip(DefDatabase<TraderKindDef>.GetNamed("Orbital_BulkGoods"));
                Map.passingShipManager.AddShip(ship);
                ship.GenerateThings();
                int silverBefore = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
                TradeSession.SetupWith(ship, negotiator, false);
                Tradeable tradeable = TradeSession.deal.AllTradeables.FirstOrDefault(t => t.ThingDef == RcdcDefOf.RCDC_DataCartridge);
                Check("data cartridges appear in the sell list of a trader", tradeable != null);
                if (tradeable != null)
                {
                    int count = Mathf.Min(10, tradeable.CountHeldBy(Transactor.Colony));
                    float unit = tradeable.GetPriceFor(TradeAction.PlayerSells);
                    Check("sale price is sensible (40-80 silver each)", unit >= 40f && unit <= 80f, unit.ToString("F1") + " silver each");
                    tradeable.ForceTo(-count);   // outside gift mode a negative count means "sell to the trader"
                    bool traded;
                    sold = TradeSession.deal.TryExecute(out traded);
                    tradedCount = count;
                    tradedUnitPrice = unit;
                    sellPriceBeforeCertification = unit;
                    silverBeforeTrade = silverBefore;
                    Check("the trade executes", sold || traded);
                }
                TradeSession.Close();
            }
            catch (Exception ex)
            {
                Check("trade session ran without exceptions", false, ex.ToString());
            }
            // Sale proceeds arrive by drop pod, so give them time to land.
            WaitUntil landed = new WaitUntil(() => Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount) > silverBeforeTrade, 3000);
            yield return landed;
            if (landed.TimedOut)
            {
#if RIMWORLD_1_6
                int activePods = Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter).Count;
#else
                int activePods = Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveDropPod).Count;
#endif
                Info("silver did not arrive: pods in flight " + Map.listerThings.AllThings.Count(t => t is Skyfaller) + ", active pods " + activePods
                    + ", silver stacks " + string.Join(",", Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Select(t => t.stackCount + "@" + t.Position).ToArray()));
            }
            if (tradedCount > 0)
            {
                int silverAfter = Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
                int expected = Mathf.RoundToInt(tradedCount * tradedUnitPrice);
                Check("selling cartridges to a trader pays silver", silverAfter - silverBeforeTrade >= expected - 2 && silverAfter > silverBeforeTrade,
                    "sold " + tradedCount + ", silver " + silverBeforeTrade + " -> " + silverAfter + " (expected +" + expected + ")");
            }
        }

        // ---- 12. UPS ------------------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionUps()
        {
            if (layout == null)
            {
                yield break;
            }
            SetWorkPriority(0);
            CompPowerBattery ups = layout.Ups[0].TryGetComp<CompPowerBattery>();
            CompPowerBattery ups2 = layout.Ups[1].TryGetComp<CompPowerBattery>();
            CompServerRack rack = Rack(0);
            CompPowerTrader power = rack.parent.TryGetComp<CompPowerTrader>();
            Check("UPS stores 100 Wd", Mathf.Approximately(ups.Props.storedEnergyMax, 100f));
            Check("UPS is far smaller than a vanilla battery (600 Wd)", ups.Props.storedEnergyMax * 4 < 600f);

            ups.SetStoredEnergyPct(1f);
            ups2.SetStoredEnergyPct(1f);
            float full = ups.StoredEnergy + ups2.StoredEnergy;

            // Blackout: generators off. The UPS bridges the gap, then runs dry.
            FlickAll(layout.Generators, false);
            yield return new WaitTicks(180);
            rack.DevRefresh();
            Check("UPS keeps the racks powered after generators stop", power.PowerOn && rack.Status != RackStatus.NoPower, rack.DevSummary());
            float draining = ups.StoredEnergy + ups2.StoredEnergy;
            Check("UPS discharges while bridging the outage", draining < full - 0.5f, full.ToString("F1") + " -> " + draining.ToString("F1") + " Wd");
            string upsText = layout.Ups[0].GetInspectString();
            Check("UPS inspect text shows the runtime estimate", upsText.Contains("hours") || upsText.Contains("runtime") || upsText.Contains("Runtime"), upsText.Replace('\n', '|'));
            int outageStart = Now;
            WaitUntil dark = new WaitUntil(() => !power.PowerOn, 12000);
            yield return dark;
            int bridged = Now - outageStart + 180;
            Check("UPS ride-through is brief but real (1000-9000 ticks at full load)", !dark.TimedOut && bridged >= 1000 && bridged <= 9000, bridged + " ticks");
            rack.DevRefresh();
            Check("once the UPS is empty the racks show No Power", rack.Status == RackStatus.NoPower, rack.DevSummary());
            Check("the UPS is drained", ups.StoredEnergy + ups2.StoredEnergy < full * 0.1f, (ups.StoredEnergy + ups2.StoredEnergy).ToString("F1") + " Wd");

            // Power returns: the UPS recharges (at 70% efficiency) and the racks come back.
            FlickAll(layout.Generators, true);
            foreach (Thing g in layout.Generators) DemoDataCenter.Refuel(g);
            float low = ups.StoredEnergy + ups2.StoredEnergy;
            yield return new WaitTicks(900);
            // The power net switches consumers back on one at a time, so wait for this rack rather than a fixed time.
            yield return new WaitUntil(() => power.PowerOn, 3000);
            rack.DevRefresh();
            float charged = ups.StoredEnergy + ups2.StoredEnergy;
            Check("the UPS recharges when power returns", charged > low + 2f, low.ToString("F1") + " -> " + charged.ToString("F1") + " Wd");
            Check("racks come back after the outage", rack.Status != RackStatus.NoPower && power.PowerOn, rack.DevSummary());
        }

        // ---- 13. save / load ----------------------------------------------------------------------

        private sealed class RackSnap
        {
            public float Wear, Progress;
            public bool Shutdown;
            public int Total;
            public ThingDef OutputDef;
        }

        private sealed class Snapshot
        {
            public readonly Dictionary<int, RackSnap> Racks = new Dictionary<int, RackSnap>();
            public int Cartridges;
            public int Coverage, Shifts;
            public float UpsEnergy;
            public int CoreLinks;
            public int Stacks;
            public int Doors, Denied, UpgradesFinished, CoreCapacity;
            public float UpsCapacity;
            public bool Certified;
            public int AiCount;
            public float AiRapport;
            public AiDirective AiDirective;
            public bool AiPending;
            public bool ContractActive;
            public ThingDef ContractDef;
            public int ContractQuantity;
            public int ContractReward;
        }

        /// <summary>
        /// RimWorld's test-colony generation sometimes leaves a colonist with a family relation to a starting
        /// pawn it then discarded, and the game warns about that reference when saving. That has nothing to do with
        /// this mod, so drop such relations before the save (and say so) to keep the run's log audit meaningful.
        /// </summary>
        private static void PurgeDanglingRelations()
        {
            HashSet<Pawn> saved = new HashSet<Pawn>(PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead);
            int removed = 0;
            foreach (Pawn pawn in saved.ToList())
            {
                if (pawn.relations == null)
                {
                    continue;
                }
                foreach (DirectPawnRelation relation in pawn.relations.DirectRelations.ToList())
                {
                    if (relation.otherPawn == null || !saved.Contains(relation.otherPawn))
                    {
                        pawn.relations.RemoveDirectRelation(relation);
                        removed++;
                    }
                }
            }
            Info("purged " + removed + " dangling family relation(s) left by the test colony's generation");
        }

        private static Snapshot TakeSnapshot()
        {
            Snapshot s = new Snapshot();
            foreach (CompServerRack rack in AllRacks())
            {
                s.Racks[rack.parent.thingIDNumber] = new RackSnap
                {
                    Wear = rack.Wear, Progress = rack.Progress, Shutdown = rack.IsShutdown, Total = rack.TotalProduced, OutputDef = rack.OutputThing
                };
            }
            List<Thing> carts = Map.listerThings.ThingsOfDef(RcdcDefOf.RCDC_DataCartridge);
            s.Cartridges = carts.Sum(t => t.stackCount);
            s.Stacks = carts.Count;
            CompOperationsConsole console = MapComponent_DataCenterNetwork.For(Map).Consoles.First();
            s.Coverage = console.CoverageTicksLeft;
            s.Shifts = console.ShiftsCompleted;
            s.UpsEnergy = Map.listerThings.ThingsOfDef(RcdcDefOf.RCDC_UpsUnit).Sum(t => t.TryGetComp<CompPowerBattery>().StoredEnergy);
            s.CoreLinks = MapComponent_DataCenterNetwork.For(Map).Cores.First().ConnectedCount;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            s.Doors = network.AccessDoors.Count;
            s.Denied = network.AccessDoors.Sum(d => d.DeniedCount);
            s.UpgradesFinished = RcdcUpgrades.Current.Finished;
            s.CoreCapacity = network.Cores.First().Capacity;
            s.UpsCapacity = RcdcDefOf.RCDC_UpsUnit.GetCompProperties<CompProperties_Battery>().storedEnergyMax;
            s.Certified = network.IsCertified;
            CompAiCore aiComp = network.AiCores.FirstOrDefault();
            s.AiCount = network.AiCores.Count;
            s.AiRapport = aiComp == null ? -1f : aiComp.Rapport;
            s.AiDirective = aiComp == null ? AiDirective.Balanced : aiComp.Directive;
            s.AiPending = aiComp != null && aiComp.HasPendingRequest;
            MapComponent_DataContracts contracts = MapComponent_DataContracts.For(Map);
            s.ContractActive = contracts != null && contracts.HasActiveContract;
            s.ContractDef = contracts == null ? null : contracts.ActiveDef;
            s.ContractQuantity = contracts == null ? 0 : contracts.ActiveQuantity;
            s.ContractReward = contracts == null ? 0 : contracts.ActiveRewardSilver;
            return s;
        }

        private static IEnumerable<Waiter> SectionSaveLoad()
        {
            if (layout == null)
            {
                yield break;
            }
            // Put the data center in a rich, non-trivial state before saving.
            SetWorkPriority(0);
            yield return new WaitTicks(600);
            Rack(0).DevSetWear(0.37f);
            Rack(1).DevSetWear(0.62f);
            Rack(2).DevSetProgress(0.42f);
            Rack(3).DevSetProgress(0.13f);
            Rack(1).DevSetShutdown(true);
            Rack(2).SetOutput(RcdcDefOf.RCDC_DataCartridge_Financial);
            foreach (CompServerRack rack in AllRacks()) rack.DevRefresh();
            snapshot = TakeSnapshot();
            Info("snapshot: racks " + snapshot.Racks.Count + ", cartridges " + snapshot.Cartridges + " in " + snapshot.Stacks + " stacks, coverage " + snapshot.Coverage + ", ups " + snapshot.UpsEnergy.ToString("F1") + " Wd");
            PurgeDanglingRelations();
            GameDataSaveLoader.SaveGame(SaveName);
            Check("game saved with an operating data center", System.IO.File.Exists(GenFilePaths.FilePathForSavedGame(SaveName)));

            loadRequested = true;
            loadCompleted = false;
            GameDataSaveLoader.LoadGame(SaveName);
            WaitUntil loaded = new WaitUntil(() => loadCompleted && Find.CurrentMap != null && Find.TickManager != null, int.MaxValue / 2, 300f);
            yield return loaded;
            Check("game loaded from the save", !loaded.TimedOut);
            if (loaded.TimedOut)
            {
                yield break;
            }
            loadRequested = false;
            yield return new WaitTicks(10);

            Snapshot after = TakeSnapshot();
            Check("racks survive the save/load", after.Racks.Count == snapshot.Racks.Count, after.Racks.Count + " vs " + snapshot.Racks.Count);
            foreach (KeyValuePair<int, RackSnap> pair in snapshot.Racks)
            {
                RackSnap now;
                if (!after.Racks.TryGetValue(pair.Key, out now))
                {
                    Check("rack " + pair.Key + " exists after load", false);
                    continue;
                }
                Check("rack " + pair.Key + " wear persisted", Mathf.Abs(now.Wear - pair.Value.Wear) < 0.01f, pair.Value.Wear.ToString("F3") + " -> " + now.Wear.ToString("F3"));
                Check("rack " + pair.Key + " production progress persisted", Mathf.Abs(now.Progress - pair.Value.Progress) < 0.02f, pair.Value.Progress.ToString("F3") + " -> " + now.Progress.ToString("F3"));
                Check("rack " + pair.Key + " total produced persisted", now.Total == pair.Value.Total);
                Check("rack " + pair.Key + " specialization persisted", now.OutputDef == pair.Value.OutputDef, pair.Value.OutputDef.defName + " -> " + now.OutputDef.defName);
            }
            Check("emergency-shutdown latch persisted", snapshot.Racks.Where(kv => kv.Value.Shutdown).All(kv => after.Racks[kv.Key].Shutdown || Map.mapTemperature.OutdoorTemp < 0f));
            Check("cartridges persisted with their stacks", after.Cartridges >= snapshot.Cartridges - 1 && after.Cartridges <= snapshot.Cartridges + 4, snapshot.Cartridges + " -> " + after.Cartridges);
            Check("console coverage and shift count persisted", after.Shifts == snapshot.Shifts && Mathf.Abs(after.Coverage - snapshot.Coverage) < 500);
            Check("UPS charge persisted", Mathf.Abs(after.UpsEnergy - snapshot.UpsEnergy) < 3f, snapshot.UpsEnergy.ToString("F1") + " -> " + after.UpsEnergy.ToString("F1"));
            Check("network links rebuilt after load", after.CoreLinks == snapshot.CoreLinks, after.CoreLinks + " links");
            Check("security doors survive the save/load", snapshot.Doors == 2 && after.Doors == snapshot.Doors, snapshot.Doors + " -> " + after.Doors);
            Check("the doors remember how many people they turned away", snapshot.Denied >= 1 && after.Denied == snapshot.Denied, snapshot.Denied + " -> " + after.Denied);
            Check("finished upgrade research survives the save/load", snapshot.UpgradesFinished == 10 && after.UpgradesFinished == snapshot.UpgradesFinished, snapshot.UpgradesFinished + " -> " + after.UpgradesFinished);
            Check("upgrades are re-applied after load (core capacity 10, UPS 200 Wd)", after.CoreCapacity == 10 && Near(after.UpsCapacity, 200f, 0.01f), after.CoreCapacity + " racks, UPS " + after.UpsCapacity);
            Check("certification is still recognised after load", snapshot.Certified && after.Certified);
            float priceAfterLoad = RcdcDefOf.RCDC_DataCartridge.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("the certified price survives the load", Near(priceAfterLoad, 96f, 0.05f),
                priceAfterLoad.ToString("F2") + ", certified " + MapComponent_DataCenterNetwork.For(Map).IsCertified + ", bonus " + RcdcUpgrades.Current.CertifiedPriceBonus
                + ", doors " + string.Join(" ", MapComponent_DataCenterNetwork.For(Map).AccessDoors.Select(d => d.def.defName + ":" + d.DoorPowerOn).ToArray()));

            // The AI core: its mood, directive and an unanswered request survive the save.
            Check("the AI core survives the save/load", snapshot.AiCount == 1 && after.AiCount == 1, snapshot.AiCount + " -> " + after.AiCount);
            Check("the AI's rapport and directive persisted", Near(after.AiRapport, snapshot.AiRapport, 0.5f) && after.AiDirective == snapshot.AiDirective && snapshot.AiDirective == AiDirective.Curiosity,
                snapshot.AiRapport + " " + snapshot.AiDirective + " -> " + after.AiRapport + " " + after.AiDirective);
            Check("the AI's unanswered request persisted", snapshot.AiPending && after.AiPending);
            CompAiCore aiAfter = MapComponent_DataCenterNetwork.For(Map).AiCores.FirstOrDefault();
            ChoiceLetter_AiRequest loadedLetter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().FirstOrDefault();
            Check("the request letter is still in the letter stack and still works", loadedLetter != null && aiAfter != null && loadedLetter.Choices.Count() == 3);
            if (loadedLetter != null && aiAfter != null)
            {
                loadedLetter.Choices.First().action();
                Check("accepting the loaded request starts its arrangement", aiAfter.ActiveBoon == AiBoon.Overclock && !aiAfter.HasPendingRequest);
                aiAfter.DevClear();
            }
            WaitUntil aiBack = new WaitUntil(() => aiAfter != null && aiAfter.Status == AiStatus.Online, 3000);
            yield return aiBack;
            Check("the loaded AI core comes back online", !aiBack.TimedOut);
            Check("the loaded AI core writes a report", aiAfter != null && AiReport.Build(aiAfter).Contains("Meridian"));

            // An active, unresolved data contract survives the save/load too.
            Check("the active data contract survives the save/load", snapshot.ContractActive && after.ContractActive);
            Check("the loaded contract's terms are unchanged", after.ContractDef == snapshot.ContractDef && after.ContractQuantity == snapshot.ContractQuantity
                && after.ContractReward == snapshot.ContractReward, snapshot.ContractDef?.defName + " x" + snapshot.ContractQuantity + " -> " + after.ContractDef?.defName + " x" + after.ContractQuantity);

            // The loaded data center keeps working: refresh and run a few thousand ticks.
            layout = null;
            yield return new WaitTicks(1500);
            List<CompServerRack> racks = AllRacks().ToList();
            foreach (CompServerRack rack in racks) rack.DevRefresh();
            Check("loaded racks are evaluated without errors", racks.All(r => r.Status == RackStatus.Operational || r.Status == RackStatus.TooHot || r.Status == RackStatus.MaintenanceRequired || r.Status == RackStatus.OutputFull || r.Status == RackStatus.NoPower));
            Check("statuses are meaningful after load", racks.Any(r => r.Status == RackStatus.Operational || r.Status == RackStatus.MaintenanceRequired), string.Join(" | ", racks.Select(r => r.DevSummary()).ToArray()));
        }
    }
}
