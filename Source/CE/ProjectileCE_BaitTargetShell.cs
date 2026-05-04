using Verse;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// CE 环境专用诱饵弹投射物
    /// 继承 CE 的 ProjectileCE_Explosive，在爆炸时设置诱饵坐标
    /// 仅在 CE 激活时由 CombatExtended/Assemblies/ 加载
    /// </summary>
    public class ProjectileCE_BaitTargetShell : CombatExtended.ProjectileCE_Explosive
    {
        public override void Impact(Thing hitThing)
        {
            var map = Map;
            IntVec3 position = Position;

            base.Impact(hitThing);

            if (!DMSL_CEUtility.IsCEActive)
                return;

            try
            {
                if (map != null && position.IsValid && position.InBounds(map))
                {
                    var baitComponent = AerialRaidBaitTargetComponent.GetOrCreate(map);
                    baitComponent?.SetBaitTarget(position);
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[DMS_Legion] CE诱饵弹设置目标失败: {ex.Message}", thingIDNumber);
            }
        }
    }
}
