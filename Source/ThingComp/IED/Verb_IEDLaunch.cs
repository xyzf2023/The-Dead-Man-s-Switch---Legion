using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// IED 发射用 Verb：单把武器最多部署 4 个未引爆 IED，达到上限时禁止发射并提示。
    /// </summary>
    public class Verb_IEDLaunch : Verb_LaunchProjectile
    {
        public const int MaxDeployedPerWeapon = 4;

        public override bool Available()
        {
            if (!base.Available())
                return false;
            Thing? eq = EquipmentSource;
            if (eq == null)
                return true;
            Map? map = CasterPawn?.Map;
            if (map == null)
                return true;
            ThingDef? projDef = verbProps.defaultProjectile;
            if (projDef == null)
                return true;
            int count = Projectile_IED.GetDeployedCount(map, projDef, eq.thingIDNumber);
            return count < MaxDeployedPerWeapon;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
                return false;
            Thing eq = EquipmentSource;
            Pawn? caster = CasterPawn;
            if (eq != null && caster != null && caster.Map != null && verbProps.defaultProjectile != null)
            {
                int count = Projectile_IED.GetDeployedCount(caster.Map, verbProps.defaultProjectile, eq.thingIDNumber);
                if (count >= MaxDeployedPerWeapon)
                {
                    if (showMessages)
                        Messages.Message("DMSL_IED_MaxDeployed".Translate(), new LookTargets(caster, target.ToTargetInfo(caster.Map)), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
            }
            return true;
        }
    }
}
