using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 体型近战加伤逻辑组件。挂在被攻击方。
    /// </summary>
    public class Comp_BodySizeMeleeBonus : ThingComp
    {
        private const float DamageGapOver2 = 500f;
        private const float DamageGap1To2 = 250f;
        private const float DamageGap0To1 = 50f;

        public CompProperties_BodySizeMeleeBonus Props => (CompProperties_BodySizeMeleeBonus)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;

            // 全局开关：坦克碾压效果关闭时直接跳过
            if (DMSL_ModSettings.settings?.enableTankCrushEffect != true)
                return;

            // C - 仅近战：原版近战会 SetTool(tool)，远程/爆炸不设 Tool
            if (dinfo.Tool == null)
                return;

            // A - 判定顺序
            if (dinfo.Instigator is not Pawn attacker)
                return;
            if (parent is not Pawn victim)
                return;
            if (attacker.BodySize <= victim.BodySize)
                return;
            if (attacker.GetComp<Comp_BodySizeMeleeMarker>() == null)
                return;

            float gap = attacker.BodySize - victim.BodySize;
            float amount = gap > 2f ? DamageGapOver2
                : gap > 1f ? DamageGap1To2
                : DamageGap0To1;

            // 追加伤害，Instigator 传 null 避免再次进入本 comp 时重复追加（防重入）
            var extra = new DamageInfo(DamageDefOf.Blunt, amount, 0f, -1f, null);
            parent.TakeDamage(extra);
        }
    }
}
