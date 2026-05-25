using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥鸟未征召时原地等待的组件属性；可通过 XML 配置检查间隔与等待时长。
    /// </summary>
    public class CompProperties_CommandingBirdIdleWait : CompProperties
    {
        public int checkIntervalTicks = 60;
        public int waitDurationTicks = 180;
        public string returnToPlatformJobDefName = "FFF_ReturnToDronePlatform";

        public CompProperties_CommandingBirdIdleWait()
        {
            compClass = typeof(CompCommandingBirdIdleWait);
        }
    }
}
