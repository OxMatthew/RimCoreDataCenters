using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimCore.DataCenters
{
    public enum AccessDoorKind
    {
        /// <summary>Facial scanner: only colony members get through.</summary>
        Biometric,

        /// <summary>Metal detector gate: colony members and unarmed visitors get through.</summary>
        MetalDetector
    }

    /// <summary>Marks a door def as one of the security doors and says which kind (set in XML).</summary>
    public class AccessDoorExtension : DefModExtension
    {
        public AccessDoorKind kind;
    }

    /// <summary>
    /// A powered door that screens whoever tries to walk through it. It is an ordinary vanilla door (hold-open,
    /// temperature, fire, HP, pathing all work as usual) that only adds one rule on top of the vanilla
    /// "who may open me": <see cref="PawnCanOpen"/> is a virtual method in the game, so no Harmony patch is needed.
    ///
    /// Rules, for humanlike pawns (animals and mechs keep the vanilla behaviour, so pets are never trapped):
    ///  * colony members (colonists, and slaves owned by the colony) always pass;
    ///  * a biometric door refuses everyone else (visitors, guests, prisoners);
    ///  * a metal detector gate lets an unarmed non-colonist through and refuses an armed one;
    ///  * with no power a scanner cannot recognise anyone, so it fails secure: colony members only.
    /// Raiders and other hostile pawns are still handled by the vanilla door rules (they cannot open it).
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_AccessDoor : Building_Door
    {
        private const int SamePawnCooldownTicks = 2500;
        private const int ScanIntervalTicks = 20;

        private static readonly Material LedOk = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 1f, 0.45f));
        private static readonly Material LedDenied = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.2f, 0.16f));
        private static readonly Material LedOff = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.1f, 0.11f, 0.12f));

        // ---- saved ------------------------------------------------------------------------------
        private int deniedCount;
        private int lastDeniedTick = -999999;
        private string lastDeniedWho;
        private string lastDeniedReason;

        // ---- runtime ----------------------------------------------------------------------------
        private readonly Dictionary<int, int> lastLogged = new Dictionary<int, int>();
        private bool lastPowerOn;
        private Map registeredMap;

        public AccessDoorKind Kind
        {
            get
            {
                AccessDoorExtension ext = def.GetModExtension<AccessDoorExtension>();
                return ext == null ? AccessDoorKind.Biometric : ext.kind;
            }
        }

        public int DeniedCount
        {
            get { return deniedCount; }
        }

        public int LastDeniedTick
        {
            get { return lastDeniedTick; }
        }

        public string LastDeniedWho
        {
            get { return lastDeniedWho; }
        }

        public string LastDeniedReason
        {
            get { return lastDeniedReason; }
        }

        public bool DeniedRecently(int withinTicks)
        {
            return Find.TickManager != null && Find.TickManager.TicksGame - lastDeniedTick <= withinTicks;
        }

        // ---- lifecycle --------------------------------------------------------------------------

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            registeredMap = map;
            lastPowerOn = DoorPowerOn;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            if (network != null)
            {
                network.Register(this);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(registeredMap);
            if (network != null)
            {
                network.Unregister(this);
            }
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref deniedCount, "rcdcDenied", 0);
            Scribe_Values.Look(ref lastDeniedTick, "rcdcLastDeniedTick", -999999);
            Scribe_Values.Look(ref lastDeniedWho, "rcdcLastDeniedWho");
            Scribe_Values.Look(ref lastDeniedReason, "rcdcLastDeniedReason");
        }

        // ---- the screening rule -----------------------------------------------------------------

        /// <summary>Colonists, and slaves the colony owns, are on the staff list.</summary>
        public static bool IsStaff(Pawn p)
        {
            return p != null && p.Faction == Faction.OfPlayer;
        }

        /// <summary>The first weapon the pawn is wearing, holding or carrying in a pack, or null.</summary>
        public static ThingWithComps FindWeapon(Pawn p)
        {
            if (p == null)
            {
                return null;
            }
            if (p.equipment != null && p.equipment.Primary != null)
            {
                return p.equipment.Primary;
            }
            if (p.inventory != null)
            {
                List<Thing> items = p.inventory.innerContainer.InnerListForReading;
                for (int i = 0; i < items.Count; i++)
                {
                    ThingWithComps weapon = items[i] as ThingWithComps;
                    if (weapon != null && weapon.def.IsWeapon)
                    {
                        return weapon;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Whether this door admits the pawn, on top of the vanilla rules. Pure and cheap (pathfinding calls it
        /// a lot). When it says no, <paramref name="reasonKey"/> and <paramref name="reasonArg"/> explain why.
        /// </summary>
        public bool Screen(Pawn p, out string reasonKey, out string reasonArg)
        {
            reasonKey = null;
            reasonArg = null;
            if (p == null || !p.RaceProps.Humanlike || IsStaff(p))
            {
                return true;
            }
            if (Kind == AccessDoorKind.Biometric)
            {
                reasonKey = "RCDC_DenyNotStaff";
                return false;
            }
            if (!DoorPowerOn)
            {
                reasonKey = "RCDC_DenyOffline";
                return false;
            }
            ThingWithComps weapon = FindWeapon(p);
            if (weapon != null)
            {
                reasonKey = "RCDC_DenyArmed";
                reasonArg = weapon.LabelNoParenthesisCap;
                return false;
            }
            return true;
        }

        public override bool PawnCanOpen(Pawn p)
        {
            if (!base.PawnCanOpen(p))
            {
                return false;
            }
            string key;
            string arg;
            return Screen(p, out key, out arg);
        }

        // ---- watching the door --------------------------------------------------------------------

#if RIMWORLD_1_6
        protected override void Tick()
        {
            base.Tick();
#else
        public override void Tick()
        {
            base.Tick();
#endif
            if (!Spawned)
            {
                return;
            }
            bool powerNow = DoorPowerOn;
            if (powerNow != lastPowerOn)
            {
                lastPowerOn = powerNow;
                // The rule for who may pass just changed, so pathing must re-check every route through here.
                Map.reachability.ClearCache();
            }
            if (this.IsHashIntervalTick(ScanIntervalTicks))
            {
                LookForDeniedPawns();
            }
        }

        private void LookForDeniedPawns()
        {
            foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(this))
            {
                if (!cell.InBounds(Map))
                {
                    continue;
                }
                List<Thing> things = cell.GetThingList(Map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
                    {
                        continue;
                    }
                    // Only people the vanilla door would let in and this scanner turns away. Hostile pawns
                    // are stopped (and dealt with) by the normal door rules, not by the scanner.
                    string key;
                    string arg;
                    if (base.PawnCanOpen(pawn) && !Screen(pawn, out key, out arg))
                    {
                        LogDenied(pawn, key, arg);
                    }
                }
            }
        }

        private void LogDenied(Pawn pawn, string reasonKey, string reasonArg)
        {
            int now = Find.TickManager.TicksGame;
            int last;
            if (lastLogged.TryGetValue(pawn.thingIDNumber, out last) && now - last < SamePawnCooldownTicks)
            {
                return;
            }
            lastLogged[pawn.thingIDNumber] = now;
            deniedCount++;
            lastDeniedTick = now;
            lastDeniedWho = pawn.LabelShortCap;
            lastDeniedReason = reasonArg == null ? reasonKey.Translate().ToString() : reasonKey.Translate(reasonArg).ToString();
            if (Faction == Faction.OfPlayer)
            {
                Messages.Message("RCDC_MsgAccessDenied".Translate(LabelShort, lastDeniedWho, lastDeniedReason), this, MessageTypeDefOf.NegativeEvent, false);
                if (RcdcDefOf.RCDC_AccessDenied != null)
                {
                    RcdcDefOf.RCDC_AccessDenied.PlayOneShot(new TargetInfo(Position, Map));
                }
            }
        }

        // ---- inspection and drawing ---------------------------------------------------------------

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }
            sb.Append((Kind == AccessDoorKind.Biometric ? "RCDC_AccessRuleBiometric" : "RCDC_AccessRuleDetector").Translate());
            sb.Append('\n');
            if (DoorPowerOn)
            {
                sb.Append("RCDC_ScannerOnline".Translate());
            }
            else
            {
                sb.Append("RCDC_ScannerOffline".Translate());
            }
            if (deniedCount > 0)
            {
                sb.Append('\n').Append("RCDC_AccessBlocked".Translate(deniedCount, lastDeniedWho, lastDeniedReason));
            }
            return sb.ToString();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (!Spawned || OpenPct > 0.05f)
            {
                return;
            }
            Material led;
            if (!DoorPowerOn)
            {
                led = LedOff;
            }
            else if (DeniedRecently(600) && (Find.TickManager.TicksGame / 15) % 2 == 0)
            {
                led = LedDenied;
            }
            else
            {
                led = LedOk;
            }
            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.14f, 1f, 0.14f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, led, 0);
        }
    }
}
