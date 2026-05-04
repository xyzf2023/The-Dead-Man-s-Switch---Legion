using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 骑士长枪绘制旋转组件的 Def 属性。
    /// 当 pawn 面向 South 时贴图顺时针旋转，面向 North 时逆时针旋转。
    /// </summary>
    public class CompProperties_LanceDrawRotation : CompProperties
    {
        /// <summary>面向 South 时的贴图旋转偏移（度）。负值=顺时针，默认 -90°（贴图原文件顺时针转 90°）。</summary>
        public float southRotationOffset = -90f;

        /// <summary>面向 North 时的贴图旋转偏移（度）。正值=逆时针，默认 90°（贴图原文件逆时针转 90°）。</summary>
        public float northRotationOffset = 90f;

        /// <summary>面向 East 时的绘制位置偏移 X（世界单位，正值=向右）。默认 0。</summary>
        public float eastDrawOffsetX = 0f;

        /// <summary>面向 East 时的绘制位置偏移 Z（世界单位）。默认 0。</summary>
        public float eastDrawOffsetZ = 0f;

        /// <summary>面向 West 时的绘制位置偏移 X（世界单位，XML 中可用负值向左）。默认 0。</summary>
        public float westDrawOffsetX = 0f;

        /// <summary>面向 West 时的绘制位置偏移 Z（世界单位）。默认 0。</summary>
        public float westDrawOffsetZ = 0f;

        /// <summary>面向 South 时的绘制位置偏移 X（世界单位）。默认 0。</summary>
        public float southDrawOffsetX = 0f;

        /// <summary>面向 South 时的绘制位置偏移 Z（世界单位）。默认 0。</summary>
        public float southDrawOffsetZ = 0f;

        /// <summary>面向 North 时的绘制位置偏移 X（世界单位）。默认 0。</summary>
        public float northDrawOffsetX = 0f;

        /// <summary>面向 North 时的绘制位置偏移 Z（世界单位）。默认 0。</summary>
        public float northDrawOffsetZ = 0f;

        public CompProperties_LanceDrawRotation()
        {
            compClass = typeof(CompLanceDrawRotation);
        }
    }
}
