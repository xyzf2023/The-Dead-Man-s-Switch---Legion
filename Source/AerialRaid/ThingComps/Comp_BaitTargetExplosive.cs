using RimWorld;
using Verse;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 诱饵目标爆炸组件
    /// 在物品被摧毁（爆炸）时设置诱饵坐标
    /// 参考原版 CompExplosive 的实现方式
    /// </summary>
    public class Comp_BaitTargetExplosive : CompExplosive
    {
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // 参考原版实现：在调用基类方法之前保存位置信息
            // 原版 CompExplosive.PostDestroy 会在满足条件时调用 Detonate，此时 parent 可能已被销毁
            IntVec3? savedPosition = null;
            if (previousMap != null && parent != null && !parent.Destroyed && parent.Position.IsValid && parent.Position.InBounds(previousMap))
            {
                savedPosition = parent.Position;
            }

            // 调用基类的 PostDestroy 方法（可能会触发 Detonate 并销毁 parent）
            base.PostDestroy(mode, previousMap);

            // 在基类调用后设置诱饵坐标（使用保存的位置信息）
            if (savedPosition.HasValue && savedPosition.Value.IsValid && previousMap != null && savedPosition.Value.InBounds(previousMap))
            {
                SetBaitTargetAtPosition(previousMap, savedPosition.Value);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            // 参考原版实现：在调用 Detonate 之前保存位置和地图信息
            // 原版 CompExplosive.PostPreApplyDamage 在满足条件时会立即调用 Detonate(parent.MapHeld)
            IntVec3? savedPosition = null;
            Map? savedMap = null;
            
            if (CanEverExplodeFromDamage && parent != null && !parent.Destroyed && parent.MapHeld != null)
            {
                IntVec3 position = parent.Position;
                if (position.IsValid && position.InBounds(parent.MapHeld))
                {
                    // 检查伤害类型是否允许爆炸（复制基类的逻辑）
                    bool canExplodeFromDamageType = Props.requiredDamageTypeToExplode == null || Props.requiredDamageTypeToExplode == dinfo.Def;
                    bool isExternalViolence = dinfo.Def.ExternalViolenceFor(parent);
                    bool isExcessiveDamage = dinfo.Amount >= parent.HitPoints;
                    
                    if (isExternalViolence && isExcessiveDamage && canExplodeFromDamageType)
                    {
                        // 会在基类方法中立即调用 Detonate，所以先保存位置和地图
                        savedPosition = position;
                        savedMap = parent.MapHeld;
                    }
                }
            }

            // 调用基类的 PostPreApplyDamage 方法（可能会触发 Detonate）
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            // 在基类调用后设置诱饵坐标（使用保存的位置和地图信息）
            if (savedPosition.HasValue && savedMap != null && savedPosition.Value.IsValid && savedPosition.Value.InBounds(savedMap))
            {
                SetBaitTargetAtPosition(savedMap, savedPosition.Value);
            }
        }

        /// <summary>
        /// 在指定位置设置诱饵坐标（辅助方法）
        /// </summary>
        private void SetBaitTargetAtPosition(Map map, IntVec3 position)
        {
            if (map != null && position.IsValid && position.InBounds(map))
            {
                var baitComponent = AerialRaidBaitTargetComponent.GetOrCreate(map);
                if (baitComponent != null)
                {
                    baitComponent.SetBaitTarget(position);
                }
                else
                {
                    Log.Warning($"[DMS_Legion]诱饵弹组件：无法获取或创建诱饵组件");
                }
            }
        }
    }
}
