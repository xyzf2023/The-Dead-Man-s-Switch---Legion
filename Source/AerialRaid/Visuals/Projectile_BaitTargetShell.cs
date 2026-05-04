using RimWorld;
using Verse;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 诱饵目标迫击炮弹药
    /// 在爆炸时设置诱饵坐标，用于改变空袭目标
    /// 参考原版 Projectile_Explosive.Explode 的实现方式
    /// </summary>
    public class Projectile_BaitTargetShell : Projectile_Explosive
    {
        protected override void Explode()
        {
            // 参考原版实现：在 Destroy() 之前保存 map 和 position 信息
            // 原版 Projectile_Explosive.Explode 会先保存 map = Map，然后调用 Destroy()
            var map = Map;
            IntVec3 position = Position;

            // 调用基类的 Explode 方法（会销毁炮弹并执行爆炸）
            base.Explode();

            // 在基类调用后设置诱饵坐标（使用保存的位置和地图信息）
            if (map != null && position.IsValid && position.InBounds(map))
            {
                var baitComponent = AerialRaidBaitTargetComponent.GetOrCreate(map);
                if (baitComponent != null)
                {
                    baitComponent.SetBaitTarget(position);
                }
            }
        }
    }
}
