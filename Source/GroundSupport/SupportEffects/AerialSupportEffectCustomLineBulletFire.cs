using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 机炮支援（自定义直线子弹发射）效果的配置
    /// 使用原版 Projectile 系统
    /// </summary>
    public class CompProperties_AerialSupportEffect_CustomLineBulletFire : CompProperties
    {
        /// <summary>总共发射多少发子弹</summary>
        public int bulletCount = 10;

        /// <summary>发射间隔（基于用户线段上的相对进度 0~1）</summary>
        public float fireInterval = 0.1f;

        /// <summary>每发子弹的飞行射程（格）</summary>
        public float bulletRange = 20f;

        /// <summary>子弹 ThingDef（使用原版 Projectile 定义）</summary>
        public ThingDef? bulletThingDef;

        /// <summary>并排发射的子弹数量（1=单发，2=并排2发，以此类推）</summary>
        public int bulletsPerShot = 1;

        /// <summary>并排子弹之间的间距（格）</summary>
        public float bulletSpacing = 0.5f;

        public CompProperties_AerialSupportEffect_CustomLineBulletFire()
        {
            compClass = typeof(CompAerialSupportEffect_CustomLineBulletFire);
        }
    }

    /// <summary>
    /// 自定义直线支援子弹发射效果组件
    /// 仅通过静态 UpdateDuringFlight 被反射调用
    /// </summary>
    public class CompAerialSupportEffect_CustomLineBulletFire : ThingComp
    {
        /// <summary>
        /// 在自定义直线支援飞行过程中持续调用，用于按进度发射子弹
        /// 符合 README.md 中的接口规范（注意参数顺序和 ref 状态字段）
        /// </summary>
        public static void UpdateDuringFlight(
            CustomLineFlight flight,
            float progress,
            float startProgress,
            float endProgress,
            AerialSupportTypeDef supportType,
            Map map,
            CompProperties_AerialSupportEffect_CustomLineBulletFire props,
            ref float lastFireProgress,
            ref int firedBulletCount)
        {
            if (flight == null || map == null || props == null)
            {
                return;
            }

            // 不在用户线段范围内则不发射
            if (progress < startProgress || progress > endProgress)
            {
                return;
            }

            // 已经发完
            if (firedBulletCount >= props.bulletCount)
            {
                return;
            }

            float range = endProgress - startProgress;
            if (Mathf.Abs(range) < 0.0001f)
            {
                return;
            }

            // 将当前进度映射到 [0,1] 相对进度
            float relativeProgress = (progress - startProgress) / range;

            // 判断是否到达下一次发射间隔：
            // 只要当前相对进度比上一次发射进度多了 fireInterval，就再发一发
            float delta = relativeProgress - lastFireProgress;
            if (lastFireProgress < 0f || delta >= props.fireInterval)
            {
                // 已发数量与上限双重检查
                if (firedBulletCount >= props.bulletCount)
                {
                    return;
                }

                // 发射并排的子弹
                for (int i = 0; i < props.bulletsPerShot; i++)
                {
                    FireBullet(flight, map, props, supportType, i, props.bulletsPerShot);
                }

                lastFireProgress = relativeProgress;
                firedBulletCount++;
            }
        }

        /// <summary>
        /// 使用原版 Projectile 系统发射子弹
        /// Projectile 会自动由 RimWorld 引擎管理（更新、绘制、销毁）
        /// </summary>
        private static void FireBullet(
            CustomLineFlight flight,
            Map map,
            CompProperties_AerialSupportEffect_CustomLineBulletFire props,
            AerialSupportTypeDef supportType,
            int bulletIndex,
            int totalBullets)
        {
            if (map == null || props == null || props.bulletThingDef == null || supportType == null)
            {
                Log.Warning("[DMS_Legion] 发射子弹失败：参数为null");
                return;
            }

            // 1. 计算飞机当前位置（世界坐标）
            Vector3 aircraftPos = flight.CurrentPosition;
            IntVec3 aircraftCell = aircraftPos.ToIntVec3();

            // 2. 计算发射方向（沿飞机飞行方向）
            Vector3 flightDirection = (flight.endPos - flight.startPos).normalized;
            if (flightDirection == Vector3.zero)
            {
                flightDirection = Vector3.forward;
            }

            // 3. 计算并排偏移（垂直于飞行方向）
            Vector3 rightVector = Vector3.Cross(flightDirection, Vector3.up).normalized;
            if (rightVector == Vector3.zero)
            {
                rightVector = Vector3.right;
            }
            
            // 计算当前子弹的偏移位置（居中排列）
            // 注意：只有起始位置有偏移，目标位置都应该是飞机前方，不应该有偏移
            float offsetDistance = (bulletIndex - (totalBullets - 1) * 0.5f) * props.bulletSpacing;
            Vector3 offsetPos = aircraftPos + rightVector * offsetDistance;
            IntVec3 offsetCell = offsetPos.ToIntVec3();

            // 4. 计算目标位置（飞机前方 bulletRange 距离，所有子弹都朝同一方向）
            // 注意：目标位置不应该应用偏移，所有并排子弹都应该朝着飞机前方发射
            Vector3 targetPos = aircraftPos + flightDirection * props.bulletRange;
            IntVec3 targetCell = targetPos.ToIntVec3();

            // 5. 创建并生成 Projectile（使用原版方式）
            Projectile projectile = (Projectile)GenSpawn.Spawn(
                props.bulletThingDef,
                offsetCell,
                map,
                WipeMode.Vanish
            );

            // 6. 发射 Projectile（使用原版 Launch 方法）
            // 注意：launcher 设为 null，因为这是空中支援发射的，不是某个具体单位
            // Projectile 会自动由 RimWorld 引擎管理，无需手动跟踪
            // 子弹速度在XML中定义，无需在C#中修改
            LocalTargetInfo usedTarget = new LocalTargetInfo(targetCell);
            LocalTargetInfo intendedTarget = usedTarget;

            projectile.Launch(
                launcher: null,
                origin: offsetPos,
                usedTarget: usedTarget,
                intendedTarget: intendedTarget,
                hitFlags: ProjectileHitFlags.All,
                preventFriendlyFire: false,
                equipment: null,
                targetCoverDef: null
            );
        }
    }

}

