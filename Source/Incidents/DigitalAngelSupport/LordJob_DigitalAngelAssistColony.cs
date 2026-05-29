// ============================================================================
// 文件：LordJob_DigitalAngelAssistColony.cs
// 说明：继承原版 LordJob_AssistColony；存在普通活跃敌人时完全交给原版援军 AI。
//       猎杀人类动物由 JobGiver_DigitalAngelFightManhunterAnimals（挂于 HuntEnemiesIndividual）处理。
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.DigitalAngelSupport
{
    public class LordJob_DigitalAngelAssistColony : LordJob_AssistColony
    {
        public LordJob_DigitalAngelAssistColony()
        {
        }

        public LordJob_DigitalAngelAssistColony(Faction faction, IntVec3 exitSpot)
            : base(faction, exitSpot)
        {
        }

        internal static bool HasNormalActiveThreat(Map map)
        {
            HashSet<IAttackTarget>? hostileTargets = map.attackTargetsCache?.TargetsHostileToColony;
            if (hostileTargets == null)
                return false;

            foreach (IAttackTarget t in hostileTargets)
            {
                if (t?.Thing == null)
                    continue;

                Thing thing = t.Thing;
                if (!thing.Spawned || thing.Destroyed)
                    continue;

                if (!GenHostility.IsActiveThreatToPlayer(t))
                    continue;

                if (thing is Pawn pawn)
                {
                    if (pawn.Dead || pawn.Downed)
                        continue;

                    if (IsValidManhunterAnimal(pawn))
                        continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 扫描地图上的合法猎杀人类动物；后续可在此加入 300 tick 缓存供 JobGiver 复用。
        /// </summary>
        internal static void ForEachValidManhunterAnimal(Map map, System.Action<Pawn> action)
        {
            IReadOnlyList<Pawn>? spawned = map.mapPawns?.AllPawnsSpawned;
            if (spawned == null)
                return;

            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn pawn = spawned[i];
                if (IsValidManhunterAnimal(pawn))
                    action(pawn);
            }
        }

        internal static bool IsValidManhunterAnimal(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
                return false;

            if (pawn.RaceProps == null || !pawn.RaceProps.Animal)
                return false;

            if (!pawn.InMentalState || pawn.MentalStateDef == null)
                return false;

            if (pawn.Faction == Faction.OfPlayer)
                return false;

            string defName = pawn.MentalStateDef.defName;
            if (string.IsNullOrEmpty(defName))
                return false;

            return defName == "Manhunter" ||
                   defName == "ManhunterPermanent" ||
                   defName.Contains("Manhunter");
        }
    }
}
