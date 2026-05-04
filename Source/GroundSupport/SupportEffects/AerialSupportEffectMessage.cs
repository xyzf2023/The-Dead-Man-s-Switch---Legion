using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：发送消息
    /// </summary>
    public class CompProperties_AerialSupportEffect_Message : CompProperties
    {
        /// <summary>
        /// 要发送的消息文本
        /// </summary>
        public string message = "{0}";


        public CompProperties_AerialSupportEffect_Message()
        {
            this.compClass = typeof(CompAerialSupportEffect_Message);
        }
    }

    /// <summary>
    /// 空中支援效果组件：发送消息
    /// </summary>
    public class CompAerialSupportEffect_Message : ThingComp
    {
        public CompProperties_AerialSupportEffect_Message Props => (CompProperties_AerialSupportEffect_Message)props;

        /// <summary>
        /// 执行效果
        /// </summary>
        public void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map)
        {
            ExecuteMessageEffect(targetPos, supportType, map, Props);
        }

        /// <summary>
        /// 静态方法执行消息效果
        /// </summary>
        public static void ExecuteMessageEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_Message props)
        {
            string formattedMessage = string.Format(props.message, supportType.label);
            Messages.Message(formattedMessage, new TargetInfo(targetPos, map), MessageTypeDefOf.NeutralEvent);
        }
    }
}
