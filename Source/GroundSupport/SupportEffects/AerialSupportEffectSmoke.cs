using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：圆形范围烟雾（BlindSmoke，阻挡炮台开火并降低射击精度）
    /// </summary>
    public class CompProperties_AerialSupportEffect_Smoke : CompProperties
    {
        /// <summary>
        /// 烟雾半径（格数）
        /// </summary>
        public float smokeRadius = 5f;

        public CompProperties_AerialSupportEffect_Smoke()
        {
            compClass = typeof(CompAerialSupportEffect_Smoke);
        }
    }

    /// <summary>
    /// 空中支援效果组件：圆形范围烟雾
    /// </summary>
    public class CompAerialSupportEffect_Smoke : ThingComp
    {
        public CompProperties_AerialSupportEffect_Smoke Props => (CompProperties_AerialSupportEffect_Smoke)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）
        /// 在目标位置生成圆形 BlindSmoke 烟雾，阻挡炮台开火并降低射击精度。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_Smoke props)
        {
            if (map == null || props == null)
                return;

            float radius = props.smokeRadius > 0f ? props.smokeRadius : 5f;

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
                postExplosionGasType: GasType.BlindSmoke,
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
