using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimCore.DataCenters
{
    /// <summary>
    /// A small, themed "smash and grab" raid: a handful of pawns from a faction already hostile to the player
    /// walk in, loot what they can carry (valuable data cartridges included) and try to leave rather than fight
    /// to the last pawn. It is built entirely from vanilla raid machinery (<see cref="LordJob_AssaultColony"/>
    /// with looting and fleeing enabled, the normal pawn-group and arrival-mode utilities) — no custom pathing
    /// or combat code. Colony doors already refuse non-colonists, so a locked server room forces them to break
    /// in like any other obstacle; that is standard <c>Building_Door</c> behaviour and needs nothing extra here.
    /// Only ever fires on a map that already has a spawned server rack, and only if a hostile faction exists to
    /// send (like any vanilla raid, it simply does not fire that time if one does not).
    /// </summary>
    public class IncidentWorker_Espionage : IncidentWorker
    {
        private const float MaxPoints = 700f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }
            Map map = parms.target as Map;
            if (map == null || !map.IsPlayerHome)
            {
                return false;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            return network != null && network.Racks.Any(r => r != null && r.parent != null && r.parent.Spawned);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Faction faction;
            bool found = PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup(parms.points, out faction,
                f => f != Faction.OfPlayer && !f.def.hidden, allowNonHostileToPlayer: false, allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
            if (!found || faction == null)
            {
                return false;
            }

            float points = System.Math.Min(parms.points, MaxPoints);
            PawnGroupMakerParms groupParms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = points,
                generateFightersOnly = true
            };
            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(groupParms, false).ToList();
            if (pawns.Count == 0)
            {
                return false;
            }

            parms.faction = faction;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                return false;
            }
            parms.raidArrivalMode.Worker.Arrive(pawns, parms);

            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, true, false, true, true, false, false), map, pawns);

            SendStandardLetter("RCDC_EspionageLetterLabel".Translate(faction.Name),
                "RCDC_EspionageLetterText".Translate(faction.Name, pawns.Count), RcdcDefOf.RCDC_EspionageLetter, parms, new LookTargets(pawns), new NamedArgument[0]);

            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            CompAiCore ai = network == null ? null : network.ActiveAi;
            if (ai != null && ai.IsActive)
            {
                AiVoice.Announce(ai.parent, AiVoice.Line("Intrusion"), MessageTypeDefOf.ThreatBig);
            }
            return true;
        }
    }
}
