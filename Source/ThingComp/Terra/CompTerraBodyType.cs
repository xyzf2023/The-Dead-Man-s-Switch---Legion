using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 尘世机械体：提供 story/bodyType（男性）以便人类护甲正确渲染，死亡时掉落装备。
    /// </summary>
    public class CompTerraBodyType : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureStory(respawningAfterLoad);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureStory(false);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (parent is Pawn pawn)
                DropAllEquipment(pawn);
        }

        static void DropAllEquipment(Pawn pawn)
        {
            if (pawn.equipment != null && pawn.equipment.AllEquipmentListForReading.Count > 0)
            {
                var list = new List<ThingWithComps>(pawn.equipment.AllEquipmentListForReading);
                foreach (var eq in list)
                    pawn.equipment.TryDropEquipment(eq, out _, pawn.Position, false);
            }
            if (pawn.apparel != null && pawn.apparel.WornApparelCount > 0)
            {
                var list = new List<Apparel>(pawn.apparel.WornApparel);
                foreach (var ap in list)
                    pawn.apparel.TryDrop(ap, out _, pawn.Position, false);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureStory(true);
        }

        void EnsureStory(bool _)
        {
            if (parent is not Pawn pawn || !TerraDefNames.IsTerra(pawn))
                return;
            if (pawn.story == null)
                pawn.story = new Pawn_StoryTracker(pawn);
            pawn.story.bodyType = BodyTypeDefOf.Male;
        }
    }
}
