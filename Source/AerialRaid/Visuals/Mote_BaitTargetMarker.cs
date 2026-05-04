using RimWorld;
using Verse;
using UnityEngine;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 诱饵目标标记 Mote
    /// 自定义 Mote 类，添加额外的 null 检查以防止更新时出现空引用异常
    /// </summary>
    public class Mote_BaitTargetMarker : MoteThrown
    {
        protected override void TimeInterval(float deltaTime)
        {
            try
            {
                // 检查基本状态
                if (Destroyed || Map == null)
                {
                    return;
                }

                // 检查图形是否有效
                if (Graphic == null)
                {
                    Destroy(DestroyMode.Vanish);
                    return;
                }

                // 调用基类更新（处理位置、速度等）
                base.TimeInterval(deltaTime);
            }
            catch (System.Exception ex)
            {
                // 捕获异常，防止循环报错
                Log.Warning($"[DMS_Legion]诱饵标记Mote：更新时发生异常：{ex.Message}");
                // 如果出现异常，销毁 Mote 以防止继续报错
                if (!Destroyed && Map != null)
                {
                    try
                    {
                        Destroy(DestroyMode.Vanish);
                    }
                    catch
                    {
                        // 忽略销毁时的异常
                    }
                }
            }
        }
    }
}
