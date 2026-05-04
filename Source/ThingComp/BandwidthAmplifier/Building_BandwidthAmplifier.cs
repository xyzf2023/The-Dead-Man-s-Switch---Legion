// ============================================================================
// 频段增幅装置：自定义建筑类，在绘制时叠加充能条（参考原版 Building_Battery）。
// ============================================================================

using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 频段增幅装置建筑，在 DrawAt 中绘制充能缓冲条（黄/灰），与蓄电池电量条一致。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_BandwidthAmplifier : Building
    {
        // ---------- 充能条外观（可调） ----------
        /// <summary>充能条大小 (高, 宽)，单位：格。</summary>
        private static readonly Vector2 BarSize = new Vector2(0.9f, 0.055f);
        /// <summary>已填充段颜色（黄条）。</summary>
        private static readonly Material BarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.9f, 0.85f, 0.2f));
        /// <summary>未填充段颜色（#262626）。</summary>
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(38f / 255f, 38f / 255f, 38f / 255f));

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            var buffer = GetComp<CompBandwidthAmplifierBuffer>();
            if (buffer == null)
                return;
            int maxCharge = buffer.MaxChargeTicks;
            // ---------- 充能条比例：当前充能/最大充能（0~1），可改为其他计算方式 ----------
            float fillPercent = maxCharge > 0 ? Mathf.Clamp01(buffer.ChargeTicks / (float)maxCharge) : 0f;
            var r = new GenDraw.FillableBarRequest
            {
                // ---------- 条中心：建筑绘制点 + 偏移（上 0.1、再上 Z+0.2、左 X-0.5），可调 ----------
                center = drawLoc + new Vector3(-0.5f, 0.1f, 0.15f),
                // ---------- 条大小：见上方 BarSize ----------
                size = BarSize,
                // ---------- 条填充比例：见上方 fillPercent ----------
                fillPercent = fillPercent,
                filledMat = BarFilledMat,
                unfilledMat = BarUnfilledMat
            };
            Rot4 rot = Rotation;
            rot.Rotate(RotationDirection.Clockwise);
            r.rotation = rot;
            GenDraw.DrawFillableBar(r);
        }
    }
}
