// ============================================================================
// 不绘制红/黄/绿状态条的 CompBandNode，用于频段增幅装置（状态由自定义充能条表示）。
// 与 DMSL_BandNode_NoEffect 同理：通过使用本 Comp 而非原版 CompBandNode，仅用 Def 即可不显示状态条，无需 Harmony。
// 额外：蓄能耗尽时移除带宽 hediff，避免原版每 tick 重新添加导致带宽持续存在（见类注释）。
// ============================================================================

using RimWorld;
using Verse;
using Verse.Sound;

namespace DMS_Legion
{
    /// <summary>
    /// 继承 CompBandNode，重写 PostDraw 为空；对频段增幅装置重写 CompTick，使其电力消耗完全由充能缓冲控制，
    /// 同时在蓄能耗尽时移除 hediff。调谐 Gizmo 与原版一致。
    /// </summary>
    public class CompBandNode_NoBar : CompBandNode
    {
        /// <summary>
        /// 本类自用的特效实例，避免访问基类的私有字段。
        /// </summary>
        private Effecter? _effecter;

        /// <summary>
        /// 仅基于 tunedTo / tuningTo 计算当前状态，与原版 State 逻辑保持一致。
        /// </summary>
        private BandNodeState CurrentState
        {
            get
            {
                if (tunedTo != null && tuningTo != null)
                    return BandNodeState.Retuning;
                if (tuningTo != null)
                    return BandNodeState.Tuning;
                if (tunedTo != null)
                    return BandNodeState.Tuned;
                return BandNodeState.Untuned;
            }
        }

        public override void PostDraw()
        {
            // 不调用 base.PostDraw()，即不绘制状态条；其余调谐逻辑、Gizmo、电力等均由基类处理。
        }

        public override void CompTick()
        {
            // 对除频段增幅装置外的其他使用本 Comp 的建筑，保持原版行为不变。
            if (parent?.def?.defName != "DMSL_Building_BandwidthAmplifier")
            {
                base.CompTick();
                return;
            }

            // ---------- 以下逻辑参考原版 CompBandNode.CompTick，移除了对 PowerOutput 的控制 ----------

            // 清理死亡目标
            if (tunedTo != null && tunedTo.Dead)
                tunedTo = null;
            if (tuningTo != null && tuningTo.Dead)
                tuningTo = null;

            // 断电时停止特效并提前返回
            var powerTrader = parent.TryGetComp<CompPowerTrader>();
            if (powerTrader != null && !powerTrader.PowerOn)
            {
                _effecter?.Cleanup();
                _effecter = null;
                return;
            }

            // 调谐计时与完成逻辑
            if (tuningTo != null)
            {
                tuningTimeLeft--;
                if (tuningTimeLeft <= 0)
                {
                    tunedTo = tuningTo;
                    tuningTo = null;
                    if (Props.tuningCompleteSound != null)
                    {
                        Props.tuningCompleteSound.PlayOneShot(parent);
                    }
                }
            }

            // 确保调谐完成后 hediff 存在
            if (tuningTo == null && tunedTo != null && !tunedTo.health.hediffSet.HasHediff(Props.hediff))
            {
                tunedTo.health.AddHediff(Props.hediff, tunedTo.health.hediffSet.GetBrain());
            }

            // 状态对应的持续特效（与原版保持一致，只是使用本类自己的 Effecter 字段）
            BandNodeState state = CurrentState;
            if (state == BandNodeState.Untuned)
            {
                if (_effecter == null || _effecter.def != Props.untunedEffect)
                {
                    _effecter?.Cleanup();
                    _effecter = Props.untunedEffect.Spawn();
                }
            }
            else if (state == BandNodeState.Tuned)
            {
                if (_effecter == null || _effecter.def != Props.tunedEffect)
                {
                    _effecter?.Cleanup();
                    _effecter = Props.tunedEffect.Spawn();
                }
            }
            else if (state == BandNodeState.Tuning)
            {
                if (_effecter == null || _effecter.def != Props.tuningEffect)
                {
                    _effecter?.Cleanup();
                    _effecter = Props.tuningEffect.Spawn();
                }
            }
            else if (state == BandNodeState.Retuning)
            {
                if (_effecter == null || _effecter.def != Props.retuningEffect)
                {
                    _effecter?.Cleanup();
                    _effecter = Props.retuningEffect.Spawn();
                }
            }
            else
            {
                _effecter?.Cleanup();
                _effecter = null;
            }

            _effecter?.EffectTick(parent, parent);

            // 频段增幅装置：蓄能耗尽时不应再提供带宽。
            // 原版 CompBandNode 会在每 tick 检查并重新添加 hediff，
            // 导致本 mod 的 hediff 因 ShouldRemove 被移除后立刻被加回，带宽持续存在。
            // 此处主动移除，避免被重新添加后的“残留”。
            var buffer = parent.TryGetComp<CompBandwidthAmplifierBuffer>();
            if (buffer == null || buffer.AllowBandwidth || tunedTo == null)
                return;
            var h = tunedTo.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
            if (h == null)
                return;
            h.PreRemoved();
            tunedTo.health.hediffSet.hediffs.Remove(h);
            h.PostRemoved();
            tunedTo.mechanitor?.Notify_BandwidthChanged();
        }
    }
}
