using RimWorld;
using Verse;
using UnityEngine;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 传呼器携带者标记 Mote：在 pawn 头顶显示红色三角
    /// 使用 MoteThrown 手动跟踪 pawn 位置
    /// </summary>
    public class Mote_PagerCarrierMarker : MoteThrown
    {
        /// <summary>
        /// 关联的 pawn（从 Hediff 传递）
        /// </summary>
        public Pawn? linkedPawn;

        protected override void TimeInterval(float deltaTime)
        {
            // 先设置速度为 0，防止物理计算
            velocity = Vector3.zero;

            // 如果链接的 pawn 不存在或已死亡，销毁 Mote
            if (linkedPawn == null || linkedPawn.Dead || linkedPawn.Downed || linkedPawn.Map == null || !linkedPawn.Spawned)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            // 更新位置（在 base.TimeInterval 之前，确保位置被设置）
            UpdatePosition();
            
            // 调用基类更新（但我们已经设置了 velocity = 0，所以不会移动）
            base.TimeInterval(deltaTime);
            
            // 再次更新位置（确保 base.TimeInterval 不会覆盖我们的位置）
            UpdatePosition();
        }


        /// <summary>
        /// 更新 Mote 的位置
        /// </summary>
        private void UpdatePosition()
        {
            if (linkedPawn == null || !linkedPawn.Spawned)
            {
                return;
            }

            // 获取 pawn 的绘制位置
            Vector3 pawnDrawPos = linkedPawn.DrawPos;
            
            // 计算目标位置（pawn 位置 + Z方向偏移）
            // Z方向偏移：增加 2.0f 的 Z 偏移，让三角形向上偏移
            Vector3 targetPos = pawnDrawPos;
            targetPos.z += 2.0f; // Z方向偏移，让三角形向上偏移
            
            // 更新 Mote 的位置（通过精确位置）
            exactPosition = targetPos;
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedPawn, "linkedPawn", false);
        }
    }
}
