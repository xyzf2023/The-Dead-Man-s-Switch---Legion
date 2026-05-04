// [此组件暂时未被使用] 当前没有任何 XML Def 或 Patch 引用 CompDraftable / CompProperties_Draftable，

/*
using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 可征召标记组件。挂载此组件的机械体将显示征召按钮并可被征召（效果类似原版战争爪牙等可征召机械体）。
    /// 使用静态 HashSet 缓存所有持有此组件的 pawn ID，避免高频 GetComp 遍历。
    /// </summary>
    public class CompDraftable : ThingComp
    {
        private static readonly HashSet<int> _draftableIds = new HashSet<int>();

        private void Register()
        {
            if (parent != null)
                _draftableIds.Add(parent.thingIDNumber);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            Register();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Register();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Register();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (parent != null)
                _draftableIds.Remove(parent.thingIDNumber);
        }

        /// <summary>
        /// 在 Game.FinalizeInit 时由 Harmony 补丁调用，防止跨存档 thingIDNumber 碰撞导致误判。
        /// </summary>
        public static void ClearCache()
        {
            _draftableIds.Clear();
        }

        public static bool PawnIsDraftable(Pawn pawn)
        {
            return pawn != null && _draftableIds.Contains(pawn.thingIDNumber);
        }
    }
}
*/
