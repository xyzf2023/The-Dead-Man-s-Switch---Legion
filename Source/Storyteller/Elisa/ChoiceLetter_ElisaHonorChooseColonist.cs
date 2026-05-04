// ============================================================================
// 艾丽萨青睐：选择一名殖民者接受武装殖民舰队授予的荣誉点数（皇权）
// ============================================================================

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 选择一名殖民者接受荣誉点数的信件。选中后对该殖民者调用 GainFavor。
    /// </summary>
    public class ChoiceLetter_ElisaHonorChooseColonist : Verse.ChoiceLetter
    {
        public List<Pawn> colonists = new List<Pawn>();
        public Faction? faction;
        public int honorAmount;

        public override IEnumerable<Verse.DiaOption> Choices
        {
            get
            {
                if (!ArchivedOnly)
                {
                    for (int i = 0; i < colonists.Count; i++)
                    {
                        Pawn p = colonists[i];
                        if (p != null && !p.DestroyedOrNull())
                            yield return Option_ChooseColonist(p);
                    }
                    yield return base.Option_Postpone;
                }
                else
                {
                    yield return base.Option_Close;
                }
                if (lookTargets.IsValid())
                    yield return base.Option_JumpToLocationAndPostpone;
            }
        }

        private Verse.DiaOption Option_ChooseColonist(Pawn p)
        {
            return new Verse.DiaOption(p.LabelCap)
            {
                action = () =>
                {
                    if (ModsConfig.RoyaltyActive && p.royalty != null && faction != null && honorAmount > 0)
                        p.royalty.GainFavor(faction, honorAmount);
                    Find.LetterStack.RemoveLetter(this);
                },
                resolveTree = true
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref colonists, "colonists", Verse.LookMode.Reference);
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref honorAmount, "honorAmount", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                colonists?.RemoveAll(x => x == null);
        }
    }
}
