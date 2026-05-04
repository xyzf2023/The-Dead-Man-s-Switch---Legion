using RimWorld;
using Verse;
using System.Collections.Generic;

namespace DMS_Legion
{
    /// <summary>
    /// 万心失聪能力效果：对自身及半径内的友方机械体施加心灵迷彩效果
    /// </summary>
    public class CompAbilityEffect_OmnimindSilence : CompAbilityEffect
    {
        public new CompProperties_AbilityOmnimindSilence Props
        {
            get
            {
                return (CompProperties_AbilityOmnimindSilence)this.props;
            }
        }

        /// <summary>
        /// 应用能力效果：对自身及半径内的友方机械体施加Hediff
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = this.parent.pawn;

            if (caster == null || this.Props.hediffDef == null)
            {
                return;
            }

            // 获取施法者位置
            IntVec3 casterPos = caster.Position;
            Map map = caster.Map;

            if (map == null)
            {
                return;
            }

            // 在半径内查找所有友方机械体（包括自身）
            // 遍历范围内的所有格子（第三个参数true表示包含中心格子）
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(casterPos, this.Props.radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                // 检查该格子上的所有单位
                List<Thing> thingsAtCell = map.thingGrid.ThingsListAt(cell);
                foreach (Thing thing in thingsAtCell)
                {
                    if (thing is Pawn pawn && 
                        pawn.RaceProps.IsMechanoid && // 必须是机械体
                        pawn.Faction != null && 
                        pawn.Faction == caster.Faction && // 必须是友方
                        !pawn.Dead && // 必须活着
                        pawn.health != null) // 必须有健康系统
                    {
                        // 对找到的友方机械体（包括自身）施加Hediff
                        Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, pawn, null);
                        pawn.health.AddHediff(hediff, null, null, null);
                    }
                }
            }
        }
    }
}
