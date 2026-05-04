using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.RoyalPermits
{
    /// <summary>
    /// 皇权支援 Worker：点击后让调用者进入选点 Job，选点完成并成功召唤空中支援后才消耗（进 CD、扣好感）。
    /// 关联方式唯一：在皇权支援 Def 的 modExtensions 中直接填写 aerialSupportTypeDefName。
    /// </summary>
    public class RoyalTitlePermitWorker_AerialSupport : RoyalTitlePermitWorker
    {
        public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
        {
            if (!ModsConfig.RoyaltyActive)
                yield break;
            if (map == null || pawn == null || faction == null)
                yield break;
            if (map.generatorDef?.isUnderground ?? false)
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidMapUnreachable".Translate(faction.Named("FACTION")), null);
                yield break;
            }
            if (faction.HostileTo(Faction.OfPlayer))
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidFactionHostile".Translate(faction.Named("FACTION")), null);
                yield break;
            }

            var extension = def.GetModExtension<RoyalTitlePermitExtension_AerialSupport>();
            if (extension == null || string.IsNullOrEmpty(extension.aerialSupportTypeDefName))
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "(aerialSupportTypeDefName not set)", null);
                yield break;
            }

            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamedSilentFail(extension.aerialSupportTypeDefName);
            if (supportType == null)
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "(AerialSupportTypeDef not found: " + extension.aerialSupportTypeDefName + ")", null);
                yield break;
            }

            string description = def.LabelCap + ": ";
            Action? action = null;
            if (FillAidOption(pawn, faction, ref description, out bool free))
            {
                action = () =>
                {
                    var renderer = map.GetComponent<AerialSupportRenderer>();
                    if (renderer == null)
                    {
                        renderer = new AerialSupportRenderer(map);
                        map.components.Add(renderer);
                    }
                    renderer.SetRoyalPermitContext(pawn, def, faction, free);
                    renderer.SetSelectedSupportType(supportType);

                    JobDef jobDef = (supportType.flightPathType == "CustomLine" || supportType.flightPathType == "MultiTarget")
                        ? DMSL_JobDefOf.DMSL_AerialSupport_SelectCustomLine
                        : DMSL_JobDefOf.DMSL_AerialSupport_SelectTarget;
                    Job job = JobMaker.MakeJob(jobDef);
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                };
            }

            yield return new FloatMenuOption(description, action, faction.def.FactionIcon, faction.Color);
        }
    }
}
