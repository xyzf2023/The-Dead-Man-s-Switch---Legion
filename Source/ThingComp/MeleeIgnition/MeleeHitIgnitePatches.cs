using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 近战命中点燃：当带有 CompMeleeHitIgnite 的单位近战命中目标时，按概率点燃目标。
    /// 性能：先用 defName 快速排除非纵火者，再取 Comp，避免对每次近战都做 GetComp。
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class MeleeHitIgnitePatches
    {
        /// <summary>拥有近战点燃能力的种族 defName，用于快速排除，减少 GetComp 调用。</summary>
        private const string ArsonistDefName = "DMSL_Mech_Arsonist";

        [HarmonyPostfix]
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            Pawn caster = __instance.CasterPawn;
            if (caster == null || caster.Destroyed)
                return;

            // 快速路径：仅当 caster 为纵火者时才查 Comp，避免绝大多数近战攻击的 GetComp 开销
            if (caster.def.defName != ArsonistDefName)
                return;

            CompMeleeHitIgnite comp = caster.GetComp<CompMeleeHitIgnite>();
            if (comp == null)
                return;

            if (!Rand.Chance(comp.Props.igniteChance))
                return;

            Thing thing = target.Thing;
            if (thing == null || thing.Destroyed || !thing.Spawned)
                return;

            thing.TryAttachFire(comp.Props.fireSize, caster);
        }
    }
}
