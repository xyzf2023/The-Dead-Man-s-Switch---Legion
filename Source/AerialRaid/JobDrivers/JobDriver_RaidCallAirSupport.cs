using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid.JobDrivers
{
    /// <summary>
    /// 呼叫空袭的 Job（移动到集合点→等待→读条→销毁传呼器→启动 AerialRaidPrePhaseComponent）
    /// </summary>
    public class JobDriver_RaidCallAirSupport : JobDriver
    {
        private const int WaitAtRallyDuration = 1800; // 在集合点等待 1800 tick（30秒）
        private const int CallDuration = 900; // 呼叫读条 900 tick

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 如果 targetB 是集合点，需要预留
            if (job.targetB.IsValid)
            {
                return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
            }
            return true;
        }

        public override string GetReport()
        {
            // 根据当前阶段返回不同的描述
            // 如果 targetB 是集合点且不在集合点，显示"前往集合点"
            if (job.targetB.IsValid && pawn.Position != job.targetB.Cell)
            {
                return "DMSL_RaidCall_ReportGotoRally".Translate();
            }

            return "DMSL_RaidCall_ReportCalling".Translate();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 只有携带者执行
            this.FailOn(() => pawn.Dead);
            this.FailOn(() => pawn.Downed);
            this.FailOn(() => !HasPager());
            this.FailOn(() => !HasRequiredCapacities());

            // 阶段1：如果 targetB 是集合点，移动到集合点
            if (job.targetB.IsValid)
            {
                Toil gotoRally = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                gotoRally.FailOn(() => pawn.Dead || pawn.Downed || !HasPager() || !HasRequiredCapacities());
                yield return gotoRally;
            }

            // 阶段2：在集合点等待一段时间（让大部队有时间集结）
            Toil waitAtRally = Toils_General.Wait(WaitAtRallyDuration);
            waitAtRally.FailOn(() => pawn.Dead || pawn.Downed || !HasPager() || !HasRequiredCapacities());
            waitAtRally.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return waitAtRally;

            // 阶段3：呼叫读条
            Toil callWait = Toils_General.Wait(CallDuration);
            callWait.FailOn(() => pawn.Dead || pawn.Downed || !HasPager() || !HasRequiredCapacities());
            callWait.AddFinishAction(OnCallFinished);
            yield return callWait;
        }

        private bool HasPager()
        {
            return pawn.apparel?.WornApparel.Any(a => a.def.defName == "DMSL_AirSupportPager") == true
                || pawn.inventory?.innerContainer.Any(t => t.def.defName == "DMSL_AirSupportPager") == true;
        }

        private bool HasRequiredCapacities()
        {
            var health = pawn.health?.capacities;
            if (health == null) return false;
            return health.CapableOf(PawnCapacityDefOf.Manipulation)
                   && health.CapableOf(PawnCapacityDefOf.Talking)
                   && health.CapableOf(PawnCapacityDefOf.Sight);
        }

        private void OnCallFinished()
        {
            if (pawn.Dead || pawn.Downed || !HasPager() || !HasRequiredCapacities())
            {
                NotifyFail();
                return;
            }

            // 销毁传呼器
            DestroyPager();

            // 移除传呼器携带者标记 Hediff
            RemovePagerMarkerHediff();

            // 启动空袭倒计时（固定 3 次）
            var comp = AerialRaidPrePhaseComponent.GetOrCreate(pawn.Map);
            if (comp != null)
            {
                // 倒计时沿用 Army 的随机范围
                int countdownTicks = Rand.RangeInclusive(7500, 12500);
                comp.SetRemainingTicks(countdownTicks);
                comp.SetExecutionCount(3);
                // 设置支援类型为 AncientCorpsRaid
                comp.SetSupportTypeDefName("DMSL_AerialSupport_AncientCorpsRaid");
            }
            else
            {
                Messages.Message("DMSL_RaidCall_NoPrePhaseComponent".Translate(), MessageTypeDefOf.RejectInput);
                NotifyFail();
                return;
            }

            // 通知 Lord
            pawn.GetLord()?.ReceiveMemo(LordJob_PagerRaid.MemoCallDone);
        }

        private void DestroyPager()
        {
            // 优先穿戴，次之物品栏
            if (pawn.apparel != null)
            {
                var apparel = pawn.apparel.WornApparel.FirstOrDefault(a => a.def.defName == "DMSL_AirSupportPager");
                if (apparel != null)
                {
                    apparel.Destroy(DestroyMode.Vanish);
                    return;
                }
            }

            if (pawn.inventory != null)
            {
                var item = pawn.inventory.innerContainer.FirstOrDefault(t => t.def.defName == "DMSL_AirSupportPager");
                item?.Destroy(DestroyMode.Vanish);
            }
        }

        private void NotifyFail()
        {
            pawn.GetLord()?.ReceiveMemo(LordJob_PagerRaid.MemoCallFailed);
        }

        /// <summary>
        /// 移除传呼器携带者标记 Hediff
        /// </summary>
        private void RemovePagerMarkerHediff()
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffDef? markerDef = DefDatabase<HediffDef>.GetNamed("DMSL_PagerCarrierMarker", false);
            if (markerDef == null)
            {
                return;
            }

            Hediff? markerHediff = pawn.health.hediffSet.GetFirstHediffOfDef(markerDef);
            if (markerHediff != null)
            {
                pawn.health.RemoveHediff(markerHediff);
            }
        }
    }
}
