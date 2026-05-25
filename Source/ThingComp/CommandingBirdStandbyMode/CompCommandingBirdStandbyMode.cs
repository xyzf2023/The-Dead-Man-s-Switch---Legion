using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥鸟待命模式：玩家可通过 Gizmo 开关在非征召状态下阻止移动。
    /// 仅作用于 DMSL_Drone_CommandingBird，不分配 Job、不修改征召状态。
    /// </summary>
    public class CompCommandingBirdStandbyMode : ThingComp
    {
        private const string CommandingBirdDefName = "DMSL_Drone_CommandingBird";
        private const string GizmoIconPath = "UI/Commands/LaunchReport";
        private const string GizmoLabel = "待命模式";
        private const string GizmoDesc =
            "开启后，阻止指挥鸟在非征召状态下的移动。\n\n注意：此开关可能导致指挥鸟无法在电量耗尽前返回。";

        private bool standbyModeEnabled;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref standbyModeEnabled, "standbyModeEnabled", false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            if (parent is not Pawn pawn)
                yield break;
            if (pawn.def?.defName != CommandingBirdDefName)
                yield break;
            if (pawn.Faction != Faction.OfPlayer)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = GizmoLabel,
                defaultDesc = GizmoDesc,
                icon = ContentFinder<Texture2D>.Get(GizmoIconPath, true),
                isActive = () => standbyModeEnabled,
                toggleAction = () => standbyModeEnabled = !standbyModeEnabled
            };
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!standbyModeEnabled || parent == null)
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
            if (pawn.Drafted)
                return;

            pawn.pather?.StopDead();
        }
    }
}
