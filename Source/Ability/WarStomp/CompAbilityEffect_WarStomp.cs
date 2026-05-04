using System.Collections.Generic;
using Verse;
using RimWorld;

namespace DMS_Legion
{
    /// <summary>
    /// 战争践踏：跳跃落地时对落点范围内造成钝器碾压伤害。
    /// 通过 ICompAbilityEffectOnJumpCompleted 在 PawnFlyer 落地时触发。
    /// 按格遍历、去重并设置护甲穿透，保证伤害稳定生效且不播放爆炸音效。
    /// </summary>
    public class CompAbilityEffect_WarStomp : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        private new CompProperties_AbilityWarStomp Props => (CompProperties_AbilityWarStomp)props;

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            Map? map = caster?.Map;
            if (map == null || !target.Cell.IsValid || !target.Cell.InBounds(map))
                return;

            float radius = Props.radius;
            int damAmount = Props.damageAmount;
            DamageDef damDef = Props.damageDef ?? DamageDefOf.Blunt;
            float armorPen = Props.armorPenOverride >= 0f ? Props.armorPenOverride : (float)damAmount * Props.armorPenFactor;
            if (armorPen < 0f) armorPen = 0f;

            var damaged = new HashSet<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Cell, radius, true))
            {
                if (!cell.InBounds(map))
                    continue;
                List<Thing> list = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];
                    if (thing == caster || thing.DestroyedOrNull())
                        continue;
                    if (thing.def.category == ThingCategory.Mote || thing.def.category == ThingCategory.Ethereal)
                        continue;
                    if (damaged.Contains(thing))
                        continue;
                    damaged.Add(thing);
                    var dinfo = new DamageInfo(damDef, damAmount, armorPen, -1f, caster, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
                    thing.TakeDamage(dinfo);
                }
            }
        }
    }
}
