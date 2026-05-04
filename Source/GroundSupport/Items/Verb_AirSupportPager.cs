using System.Linq;
using RimWorld;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.GroundSupport.Items
{
    /// <summary>
    /// 空中支援传呼器动词类
    /// 简化版本：直接调用空中支援框架，使用原版轰炸流程
    /// </summary>
    public class Verb_AirSupportPager : Verb_Bombardment
    {
        /// <summary>
        /// 检查Verb是否可用
        /// 重写以检查充能
        /// </summary>
        public override bool Available()
        {
            var pawn = CasterPawn;
            if (pawn == null || pawn.Map == null || pawn.apparel == null)
            {
                return false;
            }

            // 检查充能
            var pagerApparel = pawn.apparel.WornApparel.FirstOrDefault(a => 
                a.def.defName == "DMSL_AirSupportPager");
            
            if (pagerApparel == null)
            {
                return false;
            }
            
            var reloadable = pagerApparel.GetComp<CompApparelReloadable>();
            if (reloadable == null || reloadable.RemainingCharges <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试执行轰炸动作
        /// 重写以调用我们的空中支援框架而不是原版轨道轰炸
        /// </summary>
        protected override bool TryCastShot()
        {
            var pawn = CasterPawn;
            if (pawn == null || pawn.Map == null)
            {
                return false;
            }

            // 检查充能并消耗
            if (pawn.apparel != null)
            {
                var pagerApparel = pawn.apparel.WornApparel.FirstOrDefault(a => 
                    a.def.defName == "DMSL_AirSupportPager");
                
                if (pagerApparel != null)
                {
                    var reloadable = pagerApparel.GetComp<CompApparelReloadable>();
                    reloadable?.UsedOnce();
                }
            }

            // 获取目标位置
            if (!currentTarget.IsValid)
            {
                return false;
            }

            IntVec3 targetCell = currentTarget.Cell;

            // 获取支援类型定义
            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed("DMSL_AerialSupport_AncientCorps", false);
            if (supportType == null)
            {
                Log.Error("[DMS_Legion]Verb_AirSupportPager：未找到支援类型定义 DMSL_AerialSupport_AncientCorps");
                return false;
            }

            // 通过协调器请求支援，遵循renderDelayTicks/soundDelayTicks配置
            var coordinator = Current.Game.GetComponent<AerialSupportCoordinator>();
            if (coordinator == null)
            {
                Log.Error("[DMS_Legion]Verb_AirSupportPager：未找到 AerialSupportCoordinator 组件");
                return false;
            }

            coordinator.RequestSupportAt(targetCell, pawn.Map, supportType);

            // 发送提示消息（选点完成）
            Messages.Message("请求已发送，支援即将到达".Translate(),
                new TargetInfo(targetCell, pawn.Map),
                MessageTypeDefOf.NeutralEvent);

            return true;
        }

        /// <summary>
        /// 禁用范围高亮（不显示选点范围圈）
        /// </summary>
        public override void DrawHighlight(LocalTargetInfo target)
        {
            // 只绘制目标高亮，不绘制范围圈
            GenDraw.DrawTargetHighlight(target);
        }
    }
}
