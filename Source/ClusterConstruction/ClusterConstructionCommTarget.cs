// ============================================================================
// 文件：ClusterConstructionCommTarget.cs
// 说明：集群建设通讯目标
// 功能：实现ICommunicable接口，用于在通讯台联络列表中显示"集群建设"选项
// ============================================================================

using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 集群建设通讯目标
    /// 实现ICommunicable接口，用于在通讯台联络列表中显示
    /// </summary>
    public class ClusterConstructionCommTarget : ICommunicable
    {
        /// <summary>
        /// 获取选项显示的文本
        /// 在右键菜单的派系列表中显示
        /// </summary>
        public string GetCallLabel()
        {
            return "DMSL_ClusterConstruction".Translate();
        }

        /// <summary>
        /// 获取选项的描述信息（鼠标悬停时显示）
        /// </summary>
        public string GetInfoText()
        {
            return "DMSL_ClusterConstructionDesc".Translate();
        }

        /// <summary>
        /// 点击选项后执行的操作
        /// 打开集群建设管理界面
        /// </summary>
        /// <param name="negotiator">执行联络的小人（此功能不需要小人，但接口要求此参数）</param>
        public void TryOpenComms(Pawn negotiator)
        {
            // 直接打开自定义UI窗口
            Find.WindowStack.Add(new ModularOperationWindow());
        }

        /// <summary>
        /// 获取关联的派系
        /// 集群建设不需要关联派系，返回null
        /// </summary>
        public Faction? GetFaction()
        {
            return null;
        }

        /// <summary>
        /// 获取右键菜单选项（可选方法）
        /// 如果实现了此方法，GetFloatMenuOptions会使用此方法创建菜单项
        /// 否则会使用默认方式（调用GiveUseCommsJob）
        /// </summary>
        /// <param name="console">通讯台建筑</param>
        /// <param name="negotiator">执行联络的小人</param>
        /// <returns>FloatMenuOption对象</returns>
        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            // 直接打开窗口，不需要小人执行工作
            return new FloatMenuOption(
                GetCallLabel(),
                () => TryOpenComms(negotiator),
                MenuOptionPriority.Default
            );
        }
    }
}

