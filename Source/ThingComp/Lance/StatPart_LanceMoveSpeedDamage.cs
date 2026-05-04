using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 骑士长枪移速增伤 StatPart：装备骑士长枪时，MeleeDamageFactor 按当前面板移速增加。
    /// 从 10 格/秒起，每多 0.1 格/秒 +5% 最终伤害（乘数 = 1 + (移速 - 10) * 0.5）。
    /// </summary>
    public class StatPart_LanceMoveSpeedDamage : StatPart
    {
        private const float MoveSpeedBase = 10f;
        private const float DamagePercentPer01Cps = 0.05f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return;

            Thing? primary = pawn.equipment?.Primary;
            if (primary == null || primary.def.defName != "DMSL_Weapon_Lance")
                return;

            float moveSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            if (moveSpeed < MoveSpeedBase)
                return;

            float multiplier = 1f + (moveSpeed - MoveSpeedBase) / 0.1f * DamagePercentPer01Cps;
            val *= multiplier;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return null!;

            Thing? primary = pawn.equipment?.Primary;
            if (primary == null || primary.def.defName != "DMSL_Weapon_Lance")
                return null!;

            float moveSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            if (moveSpeed < MoveSpeedBase)
                return null!;

            float multiplier = 1f + (moveSpeed - MoveSpeedBase) / 0.1f * DamagePercentPer01Cps;
            return "DMSL_LanceMoveSpeedDamage".Translate(moveSpeed.ToString("F1"), multiplier.ToStringPercent());
        }
    }
}
