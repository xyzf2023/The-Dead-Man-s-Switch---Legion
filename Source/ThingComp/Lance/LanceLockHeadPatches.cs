using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    internal static class LanceLockHeadConstants
    {
        internal const string LanceDefName = "DMSL_Weapon_Lance";
        internal const float MoveSpeedThreshold = 40f;
    }

    /// <summary>
    /// 低风险入口：仅在原版生成 DamageInfo 后按条件调整命中部位，原版命中/闪避/日志/冷却流程保持不变。
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class Patch_Verb_MeleeAttackDamage_DamageInfosToApply_LanceLockHead
    {
        [HarmonyPostfix]
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target, ref IEnumerable<DamageInfo> __result)
        {
            try
            {
                if (__instance == null || __result == null)
                {
                    return;
                }
                __result = AdjustDamageInfos(__instance, target, __result);
            }
            catch (Exception ex)
            {
                Log.WarningOnce(
                    "[DMS_Legion] LanceLockHead DamageInfosToApply postfix failed. Fallback to vanilla damage infos. " + ex,
                    99110422);
            }
        }

        private static IEnumerable<DamageInfo> AdjustDamageInfos(
            Verb_MeleeAttackDamage verb,
            LocalTargetInfo target,
            IEnumerable<DamageInfo> original)
        {
            foreach (DamageInfo dinfo in original)
            {
                DamageInfo adjusted = dinfo;
                try
                {
                    if (ShouldForceLanceHitPart(verb, target, out BodyPartRecord? hitPart) && hitPart != null)
                    {
                        adjusted.SetHitPart(hitPart);
                    }
                }
                catch (Exception ex)
                {
                    Log.WarningOnce(
                        "[DMS_Legion] Failed to adjust lance hit part. Fallback to original DamageInfo. " + ex,
                        99110423);
                }

                yield return adjusted;
            }
        }

        private static bool ShouldForceLanceHitPart(
            Verb_MeleeAttackDamage verb,
            LocalTargetInfo target,
            out BodyPartRecord? hitPart)
        {
            hitPart = null;

            if (!DMSL_CEUtility.IsCEActive)
            {
                return false;
            }

            if (verb == null)
            {
                return false;
            }

            Pawn casterPawn = verb.CasterPawn;
            if (casterPawn == null || casterPawn.Dead || casterPawn.Downed || !casterPawn.Spawned)
            {
                return false;
            }

            Thing equipment = verb.EquipmentSource;
            if (equipment?.def == null)
            {
                return false;
            }

            if (equipment.def.defName != LanceLockHeadConstants.LanceDefName)
            {
                return false;
            }

            if (casterPawn.GetStatValue(StatDefOf.MoveSpeed)
                < LanceLockHeadConstants.MoveSpeedThreshold)
            {
                return false;
            }

            Thing targetThing = target.Thing;
            if (targetThing == null || targetThing.Destroyed)
            {
                return false;
            }

            Pawn? targetPawn = targetThing as Pawn;
            if (targetPawn == null || targetPawn.Dead || targetPawn.health?.hediffSet == null)
            {
                return false;
            }

            return TryChooseLanceHitPart(targetPawn, out hitPart);
        }

        private static bool TryChooseLanceHitPart(Pawn pawn, out BodyPartRecord? hitPart)
        {
            hitPart = null;
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            HediffSet hediffSet = pawn.health.hediffSet;
            BodyPartRecord? brain = hediffSet.GetBrain();
            if (brain != null)
            {
                hitPart = brain;
                return true;
            }

            IEnumerable<BodyPartRecord> partsEnumerable = hediffSet.GetNotMissingParts();
            if (partsEnumerable == null)
            {
                return false;
            }

            List<BodyPartRecord> parts = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in partsEnumerable)
            {
                if (part != null)
                {
                    parts.Add(part);
                }
            }

            if (parts.Count == 0)
            {
                return false;
            }

            try
            {
                foreach (BodyPartRecord part in parts)
                {
                    if (part != null && part.IsInGroup(BodyPartGroupDefOf.FullHead))
                    {
                        hitPart = part;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            RaceProperties raceProps = pawn.RaceProps;
            BodyPartRecord? corePart = raceProps?.body?.corePart;
            if (corePart != null && parts.Contains(corePart))
            {
                hitPart = corePart;
                return true;
            }

            hitPart = parts.RandomElement();
            return hitPart != null;
        }
    }
}
