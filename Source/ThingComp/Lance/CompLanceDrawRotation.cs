using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 骑士长枪绘制旋转组件。用于标记需要按持枪者朝向调整贴图旋转的武器，
    /// 实际旋转逻辑由 Harmony 补丁在 PawnRenderUtility 中实现。
    /// </summary>
    public class CompLanceDrawRotation : ThingComp
    {
        public CompProperties_LanceDrawRotation Props => (CompProperties_LanceDrawRotation)props;
    }
}
