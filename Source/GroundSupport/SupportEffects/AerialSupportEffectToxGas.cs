using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：圆形范围毒气（ToxGas，需生物科技 DLC，造成刺激与毒质累积）
    /// </summary>
    public class CompProperties_AerialSupportEffect_ToxGas : CompProperties
    {
        /// <summary>
        /// 毒气半径（格数）
        /// </summary>
        public float gasRadius = 5f;

        public CompProperties_AerialSupportEffect_ToxGas()
        {
            compClass = typeof(CompAerialSupportEffect_ToxGas);
        }
    }

    /// <summary>
    /// 空中支援效果组件：圆形范围毒气
    /// </summary>
    public class CompAerialSupportEffect_ToxGas : ThingComp
    {
        public CompProperties_AerialSupportEffect_ToxGas Props => (CompProperties_AerialSupportEffect_ToxGas)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）
        /// 在目标位置生成圆形 ToxGas 毒气，对范围内生物造成刺激与毒质累积。需要生物科技 DLC。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_ToxGas props)
        {
            if (map == null || props == null)
                return;
            if (!ModsConfig.BiotechActive)
                return;

            float radius = props.gasRadius > 0f ? props.gasRadius : 5f;

            GenExplosion.DoExplosion(
                center: targetPos,
                map: map,
                radius: radius,
                damType: DamageDefOf.Smoke,
                instigator: null,
                damAmount: -1,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: GasType.ToxGas,
                postExplosionGasRadiusOverride: null,
                postExplosionGasAmount: 255,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                affectedAngle: null,
                doVisualEffects: true,
                propagationSpeed: 1f,
                excludeRadius: 0f,
                doSoundEffects: true,
                postExplosionSpawnThingDefWater: null,
                screenShakeFactor: 1f,
                flammabilityChanceCurve: null,
                overrideCells: null,
                postExplosionSpawnSingleThingDef: null,
                preExplosionSpawnSingleThingDef: null
            );
        }
    }
}
