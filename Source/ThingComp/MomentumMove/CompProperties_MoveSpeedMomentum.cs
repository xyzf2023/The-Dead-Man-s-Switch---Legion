using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 移动动量组件的 Def 属性。移动时随移动格数线性提升移动速度，达到指定格数时达到上限倍率。
    /// </summary>
    public class CompProperties_MoveSpeedMomentum : CompProperties
    {
        /// <summary>达到最大加速所需移动的格数。</summary>
        public int cellsToMaxSpeed = 10;

        /// <summary>达到 cellsToMaxSpeed 格时的移动速度倍率（如 3 表示 300%）。</summary>
        public float maxSpeedFactor = 3f;

        /// <summary>停止移动后经过多少 tick 再清零动量（默认 120）。</summary>
        public int ticksToResetMomentum = 120;

        public CompProperties_MoveSpeedMomentum()
        {
            compClass = typeof(CompMoveSpeedMomentum);
        }
    }
}
