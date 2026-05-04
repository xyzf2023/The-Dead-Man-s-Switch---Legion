using RimWorld;
using Verse;
using System.Collections.Generic;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：轰炸
    /// </summary>
    public class CompProperties_AerialSupportEffect_Bombing : CompProperties
    {
        /// <summary>
        /// 爆炸范围半径（格数）
        /// </summary>
        public float explosionRadius = 1f;

        /// <summary>
        /// 爆炸伤害量
        /// </summary>
        public int damageAmount = 50;

        /// <summary>
        /// 爆炸次数
        /// </summary>
        public int explosionCount = 3;

        /// <summary>
        /// 目标区域搜索半径（爆炸将在此半径内随机分布）
        /// </summary>
        public float targetAreaRadius = 5f;

        /// <summary>
        /// 每次tick产生的爆炸数量
        /// </summary>
        public int explosionsPerTick = 2;

        /// <summary>
        /// 爆炸间隔时间（秒）
        /// </summary>
        public float explosionIntervalSeconds = 0.2f;

        /// <summary>
        /// 伤害类型 defName（默认 Bomb）；解析后通过 DamageDefResolved 访问。
        /// </summary>
        public string damageDefDefName = "Bomb";

        /// <summary>
        /// 解析后的伤害类型（首次访问时按 damageDefDefName 解析，避免 DefOf 未初始化）
        /// </summary>
        [Unsaved(false)]
        private DamageDef? _damageDefCached;

        /// <summary>
        /// 获取伤害类型，未解析时按 damageDefDefName 解析并缓存。
        /// </summary>
        public DamageDef DamageDefResolved
        {
            get
            {
                if (_damageDefCached == null)
                    _damageDefCached = DefDatabase<DamageDef>.GetNamedSilentFail(damageDefDefName) ?? DamageDefOf.Bomb;
                return _damageDefCached;
            }
        }

        public CompProperties_AerialSupportEffect_Bombing()
        {
            this.compClass = typeof(CompAerialSupportEffect_Bombing);
        }
    }

    /// <summary>
    /// 空中支援效果组件：轰炸
    /// </summary>
    public class CompAerialSupportEffect_Bombing : ThingComp
    {
        public CompProperties_AerialSupportEffect_Bombing Props => (CompProperties_AerialSupportEffect_Bombing)props;

        /// <summary>
        /// 执行效果
        /// </summary>
        public void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map)
        {
            // 生成爆炸位置列表
            List<IntVec3> explosionPositions = new List<IntVec3>();

            for (int i = 0; i < Props.explosionCount; i++)
            {
                // 在目标区域内随机选择位置
                IntVec3 explosionPos;
                int attempts = 0;
                const int maxAttempts = 50;

                do
                {
                    explosionPos = targetPos + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(Props.targetAreaRadius))];
                    attempts++;
                }
                while (!explosionPos.InBounds(map) && attempts < maxAttempts);

                // 如果找不到有效位置，使用目标位置
                if (!explosionPos.InBounds(map))
                {
                    explosionPos = targetPos;
                }

                explosionPositions.Add(explosionPos);
            }

            // 将爆炸任务交给渲染器管理，实现间隔执行
            AerialSupportRenderer renderer = map.GetComponent<AerialSupportRenderer>();
            if (renderer != null)
            {
                renderer.StartBombingSequence(explosionPositions, Props, supportType, targetPos);
            }
            else
            {
                Log.Error("[DMS_Legion] AerialSupportRenderer not found, falling back to immediate explosions");

                // 备用方案：立即执行所有爆炸
                foreach (IntVec3 explosionPos in explosionPositions)
                {
                    ExecuteSingleExplosion(explosionPos, map, Props);
                }
            }
        }

        /// <summary>
        /// 执行单个爆炸
        /// </summary>
        public static void ExecuteSingleExplosion(IntVec3 explosionPos, Map map, CompProperties_AerialSupportEffect_Bombing props)
        {
            // 使用简化的爆炸调用，添加必需的instigator参数
            GenExplosion.DoExplosion(
                center: explosionPos,
                map: map,
                radius: props.explosionRadius,
                damType: props.DamageDefResolved,
                instigator: null,  // 空中支援没有具体引发者
                damAmount: props.damageAmount,
                armorPenetration: -1f
            );
        }

    }
}
