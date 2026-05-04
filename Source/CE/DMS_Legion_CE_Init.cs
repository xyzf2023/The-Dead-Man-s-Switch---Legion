using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// CE 兼容层启动入口
    /// 此程序集仅在 LoadFolders.xml 检测到 CE 激活时加载
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DMS_Legion_CE_Init
    {
        static DMS_Legion_CE_Init()
        {
            if (!DMSL_CEUtility.IsCEActive)
            {
                Log.Warning("[DMS_Legion] CE兼容已加载，但未检测到Combat Extended。这可能是配置错误。");
                return;
            }

            Log.Message("[DMS_Legion] Combat Extended兼容已加载。");
        }
    }
}
