using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 机械师身份与带宽/控制组标记组件。挂载此组件的机械体被游戏视为机械师，
    /// 拥有 Pawn_MechanitorTracker 与 Pawn_RelationsTracker，并提供额外带宽与控制组。
    /// </summary>
    public class CompMechCommanderMarker : ThingComp
    {
        public CompProperties_MechCommanderMarker Props => (CompProperties_MechCommanderMarker)props;

        /// <summary>缓存带 Marker 的 Pawn，避免热路径上重复 GetComp 遍历。在 PostSpawnSetup 注册，在 PawnHasMarker 中遇 Destroyed 时清理。</summary>
        private static readonly HashSet<Pawn> MarkedPawnsCache = new HashSet<Pawn>();

        public static bool PawnHasMarker(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (MarkedPawnsCache.Contains(pawn))
            {
                if (pawn.Destroyed)
                {
                    MarkedPawnsCache.Remove(pawn);
                    return false;
                }
                return true;
            }

            var comp = pawn.GetComp<CompMechCommanderMarker>();
            if (comp != null)
            {
                MarkedPawnsCache.Add(pawn);
                return true;
            }
            return false;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent is Pawn p)
            {
                MarkedPawnsCache.Add(p);
                if (p.mechanitor != null)
                {
                    p.mechanitor.Notify_BandwidthChanged();
                    AccessTools.Method(typeof(Pawn_MechanitorTracker), "Notify_ControlGroupAmountMayChanged")
                        ?.Invoke(p.mechanitor, null);
                }
            }
        }
    }
}
