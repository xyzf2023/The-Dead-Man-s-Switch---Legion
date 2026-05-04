using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 自定义直线支援的轰炸效果组件属性
    /// </summary>
    public class CompProperties_AerialSupportEffect_CustomLineBombing : CompProperties
    {
        /// <summary>
        /// 爆炸次数
        /// </summary>
        public int explosionCount = 5;

        /// <summary>
        /// 每次爆炸的伤害值
        /// </summary>
        public int damageAmount = 5;

        /// <summary>
        /// 每次爆炸的范围
        /// </summary>
        public float explosionRadius = 5f;

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

        public CompProperties_AerialSupportEffect_CustomLineBombing()
        {
            this.compClass = typeof(CompAerialSupportEffect_CustomLineBombing);
        }
    }

    /// <summary>
    /// 自定义直线支援的轰炸效果组件
    /// </summary>
    public class CompAerialSupportEffect_CustomLineBombing : ThingComp
    {
        public CompProperties_AerialSupportEffect_CustomLineBombing Props => (CompProperties_AerialSupportEffect_CustomLineBombing)props;

        /// <summary>
        /// 获取自定义直线轰炸组件的配置
        /// </summary>
        /// <param name="supportType">支援类型</param>
        /// <returns>轰炸配置，如果未找到则返回null</returns>
        public static CompProperties_AerialSupportEffect_CustomLineBombing? GetBombingProps(AerialSupportTypeDef supportType)
        {
            if (supportType.effectComps != null)
            {
                foreach (var compProps in supportType.effectComps)
                {
                    if (compProps is CompProperties_AerialSupportEffect_CustomLineBombing props)
                    {
                        return props;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 初始化爆炸位置（在飞行创建时调用）
        /// 计算所有爆炸位置并缓存，与原有 CacheExplosionPositions() 逻辑完全一致
        /// </summary>
        /// <param name="userStart">用户选择的起点</param>
        /// <param name="userEnd">用户选择的终点</param>
        /// <param name="props">轰炸效果组件属性</param>
        /// <param name="explosionPositions">输出的爆炸位置列表</param>
        public static void InitializeExplosionPositions(
            IntVec3 userStart,
            IntVec3 userEnd,
            CompProperties_AerialSupportEffect_CustomLineBombing props,
            out List<IntVec3> explosionPositions)
        {
            explosionPositions = new List<IntVec3>();

            // 计算所有爆炸位置（在用户选择的直线上均匀分布，包括起点和终点）
            // 逻辑与原有 CacheExplosionPositions() 完全一致
            int totalExplosions = props.explosionCount;
            for (int i = 0; i < totalExplosions; i++)
            {
                // 均匀分布：第一个爆炸在起点，最后一个爆炸在终点
                float explosionProgress = (float)i / (totalExplosions - 1);
                Vector3 startPos = userStart.ToVector3();
                Vector3 endPos = userEnd.ToVector3();
                Vector3 explosionPos = Vector3.Lerp(startPos, endPos, explosionProgress);

                IntVec3 finalExplosionPos = explosionPos.ToIntVec3();
                explosionPositions.Add(finalExplosionPos);
            }
        }

        /// <summary>
        /// 每帧更新（在飞行Tick中调用）
        /// 根据飞行进度检查并执行爆炸，与原有 CheckAndExecuteExplosions() 逻辑完全一致
        /// </summary>
        /// <param name="flight">自定义直线飞行对象</param>
        /// <param name="progress">当前飞行进度 (0.0 ~ 1.0)</param>
        /// <param name="startProgress">用户线段起点进度</param>
        /// <param name="endProgress">用户线段终点进度</param>
        /// <param name="supportType">支援类型定义</param>
        /// <param name="map">当前地图</param>
        /// <param name="props">轰炸效果组件属性</param>
        /// <param name="cachedExplosionPositions">缓存的爆炸位置列表（ref参数，可修改）</param>
        /// <param name="executedExplosionIndices">已执行的爆炸索引集合（ref参数，可修改）</param>
        public static void UpdateDuringFlight(
            CustomLineFlight flight,
            float progress,
            float startProgress,
            float endProgress,
            AerialSupportTypeDef supportType,
            Map map,
            CompProperties_AerialSupportEffect_CustomLineBombing props,
            ref List<IntVec3> cachedExplosionPositions,
            ref HashSet<int> executedExplosionIndices)
        {
            // 安全检查：确保必要数据已初始化
            if (cachedExplosionPositions == null || cachedExplosionPositions.Count == 0)
            {
                Log.Warning("[DMS_Legion] 爆炸位置缓存为空，跳过爆炸检查");
                return;
            }

            // 确保executedExplosionIndices不为null
            if (executedExplosionIndices == null)
            {
                executedExplosionIndices = new HashSet<int>();
            }

            // 如果已经执行完所有爆炸，则不需要检查
            if (executedExplosionIndices.Count >= cachedExplosionPositions.Count)
            {
                return;
            }

            // 计算当前应该执行的最大爆炸索引
            // 逻辑与原有 CheckAndExecuteExplosions() 完全一致
            int maxExplosionIndex;

            if (progress >= endProgress)
            {
                // 如果已经飞过用户线段末端，执行所有剩余爆炸
                maxExplosionIndex = cachedExplosionPositions.Count - 1;
            }
            else if (progress >= startProgress)
            {
                // 计算飞机在用户线段上的相对进度 (0-1)
                float relativeProgress = Mathf.Clamp01((progress - startProgress) / (endProgress - startProgress));
                // 计算当前应该执行的爆炸索引（向下取整，确保只执行已到达的爆炸）
                int totalExplosions = cachedExplosionPositions.Count;
                maxExplosionIndex = Mathf.FloorToInt(relativeProgress * (totalExplosions - 1));
                // 确保不超过总爆炸数
                maxExplosionIndex = Mathf.Min(maxExplosionIndex, totalExplosions - 1);
            }
            else
            {
                // 还没进入用户线段，不执行爆炸
                return;
            }

            // 执行所有应该执行但还未执行的爆炸（顺序执行）
            // 逻辑与原有 CheckAndExecuteExplosions() 完全一致
            for (int i = 0; i <= maxExplosionIndex; i++)
            {
                if (executedExplosionIndices.Contains(i))
                {
                    continue; // 已执行过
                }

                IntVec3 explosionPos = cachedExplosionPositions[i];
                ExecuteExplosionAtPosition(explosionPos, i, props, map);
                executedExplosionIndices.Add(i);
            }
        }

        /// <summary>
        /// 在指定位置执行单个爆炸
        /// 逻辑与原有 ExecuteExplosionAtPosition() 完全一致
        /// </summary>
        /// <param name="position">爆炸位置</param>
        /// <param name="explosionIndex">爆炸索引</param>
        /// <param name="props">轰炸效果组件属性</param>
        /// <param name="map">当前地图</param>
        private static void ExecuteExplosionAtPosition(IntVec3 position, int explosionIndex,
            CompProperties_AerialSupportEffect_CustomLineBombing props, Map map)
        {
            // 执行爆炸
            if (map != null)
            {
                GenExplosion.DoExplosion(
                    center: position,
                    map: map,
                    radius: props.explosionRadius,
                    damType: props.DamageDefResolved,
                    instigator: null,
                    damAmount: props.damageAmount,
                    armorPenetration: -1f
                );
            }
        }
    }
}