using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 武器模式切换组件属性
    /// 用于在XML中配置次要攻击模式的参数
    /// </summary>
    public class CompProperties_SecondaryVerb_Rework : CompProperties
    {
        /// <summary>
        /// 主模式按钮图标路径
        /// </summary>
        public string mainCommandIcon = "";

        /// <summary>
        /// 主模式按钮标签文本
        /// </summary>
        public string mainWeaponLabel = "";

        /// <summary>
        /// 次模式按钮图标路径
        /// </summary>
        public string secondaryCommandIcon = "";

        /// <summary>
        /// 次模式按钮标签文本
        /// </summary>
        public string secondaryWeaponLabel = "";

        /// <summary>
        /// 按钮描述文本
        /// </summary>
        public string description = "";

        /// <summary>
        /// 次要攻击模式的Verb属性
        /// 在XML中通过verbProps标签配置
        /// </summary>
        public VerbProperties verbProps = new VerbProperties();

        public CompProperties_SecondaryVerb_Rework()
        {
            compClass = typeof(CompSecondaryVerb_Rework);
        }
    }
}
