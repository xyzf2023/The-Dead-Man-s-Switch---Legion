using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 装填 AXF12 航弹：完全接管流程 — 靠近航弹 → 拾取 → 靠近 AXF12 → 读条装填 → 放入 Comp。
    /// Job：Target A = 容器建筑，Target B = 当前航弹，targetQueueB = 其余航弹。
    /// </summary>
    public class JobDriver_FillAXF12Ammo : JobDriver
    {
        /// <summary>每颗航弹装填读条耗时（tick）。</summary>
        private const int LoadDurationTicksPerBomb = 300;

        private Thing ContainerBuilding => job.GetTarget(TargetIndex.A).Thing;
        private CompAXF12AmmoReserve? ReserveComp => ContainerBuilding?.TryGetComp<CompAXF12AmmoReserve>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (ReserveComp == null || !pawn.Reserve(ContainerBuilding, job, 1, -1, null, errorOnFailed))
                return false;
            int count = job.count > 0 ? job.count : 1;
            Thing ammo = job.GetTarget(TargetIndex.B).Thing;
            if (ammo == null || !pawn.Reserve(ammo, job, 1, count, null, errorOnFailed))
                return false;
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => ReserveComp == null || ReserveComp.AmountToAutofill <= 0);

            Toil loopStart = Toils_General.Label();

            yield return loopStart;
            yield return Toils_Reserve.Reserve(TargetIndex.B);

            // 1. 靠近航弹：站到航弹格以便拾取（仅此时对 B 做 FailOn，拾取后 B 已入手不再检查）
            Toil goToAmmo = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return goToAmmo;

            // 2. 拾取：手动从地图放入手中
            Toil pickUp = ToilMaker.MakeToil("FillAXF12_PickUp");
            pickUp.initAction = () =>
            {
                Thing? ammo = job.GetTarget(TargetIndex.B).Thing;
                if (ammo == null || !ammo.Spawned)
                    return;
                int toTake = job.count > 0 ? Mathf.Min(job.count, ammo.stackCount) : ammo.stackCount;
                if (toTake <= 0)
                    return;
                int taken = pawn.carryTracker.TryStartCarry(ammo, toTake);
                if (taken > 0)
                    job.count -= taken;
            };
            pickUp.defaultCompleteMode = ToilCompleteMode.Instant;
            pickUp.FailOnDestroyedOrNull(TargetIndex.B);
            pickUp.FailOnForbidden(TargetIndex.B);
            yield return pickUp;

            // 3. 靠近 AXF12（不再对 B 做 FailOn，B 已在手中）
            Toil goToContainer = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return goToContainer;

            // 4. 逐颗读条装填：每颗 300 tick 读条 → 放入 1 颗到储备 → 若手中还有且储备未满则重复
            Toil loadOneStart = Toils_General.Label();
            yield return loadOneStart;

            Toil loadBar = Toils_General.Wait(LoadDurationTicksPerBomb, TargetIndex.None)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);
            yield return loadBar;

            Toil depositOne = ToilMaker.MakeToil("FillAXF12_DepositOne");
            depositOne.initAction = () =>
            {
                Thing? carried = pawn.carryTracker?.CarriedThing;
                if (carried == null || carried.stackCount < 1)
                    return;
                var comp = ReserveComp;
                if (comp == null || comp.AmountToAutofill <= 0)
                    return;
                Thing? one = carried.SplitOff(1);
                if (one != null)
                    comp.GetDirectlyHeldThings().TryAddOrTransfer(one, true);
            };
            depositOne.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return depositOne;

            yield return Toils_Jump.JumpIf(loadOneStart, () =>
                (pawn.carryTracker?.CarriedThing?.stackCount ?? 0) > 0 && (ReserveComp?.AmountToAutofill ?? 0) > 0);

            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);
            yield return Toils_Jump.JumpIf(loopStart, () => !job.GetTargetQueue(TargetIndex.B).NullOrEmpty());
        }
    }
}
