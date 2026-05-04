using RimWorld;
using Verse;
using UnityEngine;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 传呼器携带者标记：管理 pawn 头顶的红色三角 Mote
    /// </summary>
    public class Hediff_PagerCarrierMarker : Hediff
    {
        /// <summary>
        /// 关联的 Mote（用于绘制红色三角）
        /// </summary>
        private Thing? attachedMote;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            
            // 创建并附加 Mote
            CreateAttachedMote();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            
            // 销毁关联的 Mote
            if (attachedMote != null && !attachedMote.Destroyed)
            {
                attachedMote.Destroy(DestroyMode.Vanish);
                attachedMote = null;
            }
        }

        /// <summary>
        /// 创建并附加 Mote 到 pawn
        /// </summary>
        private void CreateAttachedMote()
        {
            if (pawn == null || pawn.Map == null || pawn.Dead || pawn.Downed)
            {
                return;
            }

            ThingDef? moteDef = DefDatabase<ThingDef>.GetNamed("Mote_DMSL_PagerCarrierMarker", false);
            if (moteDef == null)
            {
                Log.Warning("[DMS_Legion]传呼器标记：未找到 Mote_DMSL_PagerCarrierMarker 定义");
                return;
            }

            // 创建 Mote 并设置关联的 pawn
            Thing mote = ThingMaker.MakeThing(moteDef);
            GenSpawn.Spawn(mote, pawn.Position, pawn.Map);

            // 设置关联的 pawn（用于跟踪位置）
            if (mote is Mote_PagerCarrierMarker markerMote)
            {
                markerMote.linkedPawn = pawn;
                attachedMote = markerMote;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref attachedMote, "attachedMote", false);
        }
    }
}
