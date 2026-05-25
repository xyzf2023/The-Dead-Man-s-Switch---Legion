using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥鸟未征召时分配原地等待 Job，避免进入 FFF 默认闲逛逻辑。
    /// 仅作用于 DMSL_Drone_CommandingBird，不打断返回平台或已有等待 Job。
    /// </summary>
    public class CompCommandingBirdIdleWait : ThingComp
    {
        private const string CommandingBirdDefName = "DMSL_Drone_CommandingBird";
        private const string WaitJobDefName = "Wait";
        private const string WaitMaintainPostureJobDefName = "Wait_MaintainPosture";
        private const string DefaultReturnToPlatformJobDefName = "FFF_ReturnToDronePlatform";
        private const int DefaultCheckIntervalTicks = 60;
        private const int DefaultWaitDurationTicks = 180;

        public CompProperties_CommandingBirdIdleWait? Props => props as CompProperties_CommandingBirdIdleWait;

        private int CheckIntervalTicks
        {
            get
            {
                int interval = Props?.checkIntervalTicks ?? DefaultCheckIntervalTicks;
                return interval > 0 ? interval : DefaultCheckIntervalTicks;
            }
        }

        private int WaitDurationTicks
        {
            get
            {
                int duration = Props?.waitDurationTicks ?? DefaultWaitDurationTicks;
                return duration > 0 ? duration : DefaultWaitDurationTicks;
            }
        }

        private string ReturnToPlatformJobDefName
        {
            get
            {
                string name = Props?.returnToPlatformJobDefName ?? string.Empty;
                return string.IsNullOrEmpty(name) ? DefaultReturnToPlatformJobDefName : name;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (Props == null || parent == null)
                return;
            if (!parent.IsHashIntervalTick(CheckIntervalTicks))
                return;
            if (parent is not Pawn pawn)
                return;
            if (pawn.def?.defName != CommandingBirdDefName)
                return;
            if (pawn.Faction != Faction.OfPlayer)
                return;
            if (!pawn.Spawned || pawn.Map == null)
                return;
            if (pawn.DeadOrDowned)
                return;
            if (pawn.jobs == null)
                return;
            if (pawn.Drafted)
                return;

            JobDef? curJobDef = pawn.CurJobDef;
            string? curJobDefName = curJobDef?.defName;
            if (curJobDefName == ReturnToPlatformJobDefName)
                return;
            if (curJobDefName == WaitJobDefName || curJobDefName == WaitMaintainPostureJobDefName)
                return;

            JobDef? waitDef = DefDatabase<JobDef>.GetNamedSilentFail(WaitMaintainPostureJobDefName);
            if (waitDef == null)
                waitDef = JobDefOf.Wait;
            if (waitDef == null)
                return;

            Job job = JobMaker.MakeJob(waitDef);
            job.expiryInterval = WaitDurationTicks;
            job.checkOverrideOnExpire = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
