using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// AXF12 航弹储备组件：复刻原版战争女皇钢铁储备逻辑。
    /// 实现 IThingHolder，使用 innerContainer 存储航弹，WorkGiver 使用 HaulAIUtility.FindFixedIngredientCount + HaulToContainerJob，原版 JobDriver_HaulToContainer 装填。
    /// </summary>
    public class CompAXF12AmmoReserve : ThingComp, IThingHolder
    {
        private ThingOwner_AXF12AmmoReserve? innerContainer;
        private string? reserveThingDefNameCached;

        private ThingOwner_AXF12AmmoReserve InnerContainer => innerContainer ??= new ThingOwner_AXF12AmmoReserve(this);

        public CompProperties_AXF12AmmoReserve Props => (CompProperties_AXF12AmmoReserve)props;

        /// <summary>当前储备数量（innerContainer 中储备物数量）。</summary>
        public int CurrentCount => ReserveThingDef != null ? InnerContainer.TotalStackCountOfDef(ReserveThingDef) : 0;

        /// <summary>装填目标数量（玩家可拖拽滑条设置，原版 maxToFill）。</summary>
        public int maxToFill;

        /// <summary>尚可装填数量（原版 AmountToAutofill）。</summary>
        public int AmountToAutofill => Mathf.Max(0, maxToFill - CurrentCount);

        /// <summary>当前储备占上限的比例（0～1），用于 Gizmo 滑条显示。</summary>
        public float PercentageFull => Props.maxCount > 0 ? Mathf.Clamp01((float)CurrentCount / Props.maxCount) : 0f;

        /// <summary>储备物 Def（用于 Job 与 Gizmo）。</summary>
        public ThingDef? ReserveThingDef
        {
            get
            {
                if (string.IsNullOrEmpty(Props.reserveThingDefName))
                    return null;
                if (reserveThingDefNameCached != Props.reserveThingDefName)
                {
                    reserveThingDefNameCached = Props.reserveThingDefName;
                    _reserveThingDefCached = DefDatabase<ThingDef>.GetNamedSilentFail(Props.reserveThingDefName);
                }
                return _reserveThingDefCached;
            }
        }

        [Unsaved(false)]
        private ThingDef? _reserveThingDefCached;

        /// <summary>是否仍可装填（未满）。</summary>
        public bool NeedsFill => AmountToAutofill > 0 && ReserveThingDef != null;

        /// <summary>DEV：将航弹储备补满至上限。仅上帝模式 Gizmo 调用。</summary>
        public void DevFillAmmoToMax()
        {
            if (ReserveThingDef == null || Props.maxCount <= 0)
                return;
            int need = Props.maxCount - CurrentCount;
            if (need <= 0)
                return;
            int stackLimit = ReserveThingDef.stackLimit > 0 ? ReserveThingDef.stackLimit : 1;
            while (need > 0)
            {
                int toAdd = Mathf.Min(need, stackLimit);
                Thing t = ThingMaker.MakeThing(ReserveThingDef);
                t.stackCount = toAdd;
                if (!InnerContainer.TryAdd(t))
                    break;
                need -= toAdd;
            }
        }

        /// <summary>扣减储备（轰炸等逻辑调用），返回实际扣减数量。</summary>
        public int ConsumeAmmo(int count)
        {
            if (ReserveThingDef == null || count <= 0)
                return 0;
            int have = InnerContainer.TotalStackCountOfDef(ReserveThingDef);
            int consume = Mathf.Min(count, have);
            if (consume <= 0)
                return 0;
            List<Thing> list = InnerContainer.ToList();
            int left = consume;
            for (int i = 0; i < list.Count && left > 0; i++)
            {
                Thing t = list[i];
                if (t.def != ReserveThingDef)
                    continue;
                int take = Mathf.Min(left, t.stackCount);
                Thing taken = InnerContainer.Take(t, take);
                if (taken != null)
                {
                    taken.Destroy(DestroyMode.Vanish);
                    left -= take;
                }
            }
            return consume - left;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return InnerContainer;
        }

        public new IThingHolder? ParentHolder
        {
            get
            {
                if (parent is IThingHolder holder)
                    return holder;
                if (parent?.Map != null)
                    return parent.Map;
                return null;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner_AXF12AmmoReserve(this);
            }
            if (maxToFill <= 0)
                maxToFill = Props.maxCount;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "ammoReserveInnerContainer", this);
            Scribe_Values.Look(ref maxToFill, "ammoReserveMaxToFill", Props.maxCount);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
                innerContainer = new ThingOwner_AXF12AmmoReserve(this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && maxToFill <= 0)
                maxToFill = Props.maxCount;
        }

        /// <summary>
        /// 右键建筑时提供「优先装填 AXF-12」选项：用自定义 Job DMSL_Job_FillAXF12Ammo 生成装填任务并 DecoratePrioritizedTask。
        /// </summary>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (AmountToAutofill <= 0 || ReserveThingDef == null)
                yield break;

            string label = "DMSL_AXF12_FillAmmo_Label".Translate();
            if (!selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }
            if (parent.IsForbidden(selPawn) || !selPawn.CanReserve(parent, 1, -1, null, true))
                yield break;

            List<Thing> list = HaulAIUtility.FindFixedIngredientCount(selPawn, ReserveThingDef, AmountToAutofill);
            if (list.NullOrEmpty())
            {
                yield return new FloatMenuOption(label + ": " + "DMSL_AXF12_FillAmmo_NoAmmo".Translate(), null);
                yield break;
            }

            JobDef fillDef = DefDatabase<JobDef>.GetNamedSilentFail("DMSL_Job_FillAXF12Ammo");
            if (fillDef == null)
                yield break;
            Job job = JobMaker.MakeJob(fillDef, parent, list[0]);
            job.count = Mathf.Min(list[0].stackCount, AmountToAutofill);
            job.targetQueueB = list.Skip(1).Select(thing => new LocalTargetInfo(thing)).ToList();

            Action action = () => selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), selPawn, parent);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            if (parent.Faction != Faction.OfPlayer || !parent.Spawned)
                yield break;

            // DEV：与原版 Building_GravEngine、CompAtomizer 等一致，使用 DebugSettings.ShowDevGizmos（= Prefs.DevMode && DebugSettings.godMode）
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: 补满航弹",
                    defaultDesc = "将 AXF-12 航弹储备补满至上限",
                    action = () => DevFillAmmoToMax()
                };
            }

            yield return new Gizmo_AXF12AmmoReserve(this);
        }

        public override string CompInspectStringExtra()
        {
            if (ReserveThingDef == null)
                return null!;
            return "DMSL_AXF12_AmmoReserve_Inspect".Translate(ReserveThingDef.label, CurrentCount, Props.maxCount);
        }
    }

    /// <summary>
    /// 仅接受指定 Def、且总数量不超过 maxCount 的 ThingOwner，供 HaulToContainer 使用。
    /// </summary>
    public class ThingOwner_AXF12AmmoReserve : ThingOwner<Thing>
    {
        private readonly CompAXF12AmmoReserve comp;

        public ThingOwner_AXF12AmmoReserve(CompAXF12AmmoReserve comp)
            : base(comp, LookMode.Deep, true)
        {
            this.comp = comp ?? throw new ArgumentNullException(nameof(comp));
        }

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            if (item == null || item.stackCount <= 0 || comp.ReserveThingDef == null)
                return 0;
            if (item.def != comp.ReserveThingDef)
                return 0;
            int space = comp.AmountToAutofill;
            if (space <= 0)
                return 0;
            return Mathf.Min(item.stackCount, space);
        }
    }

    public class CompProperties_AXF12AmmoReserve : CompProperties
    {
        public string reserveThingDefName = "DMSL_AerialBomb";
        public int maxCount = 3;

        public CompProperties_AXF12AmmoReserve()
        {
            compClass = typeof(CompAXF12AmmoReserve);
        }
    }

    /// <summary>
    /// 航弹储备 Gizmo：两条分割线将条均分为三份（每份对应一颗航弹），滑条仅在 0、1、2、3 四档。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Gizmo_AXF12AmmoReserve : Gizmo
    {
        private readonly CompAXF12AmmoReserve comp;
        private float targetValue;
        private float lastTargetValue;

        private static readonly Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));
        private static readonly Texture2D BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));
        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
        private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));
        private static bool draggingBar;
        /// <summary>两条分割线位置：1/3、2/3，将条均分为三份（每份对应一颗航弹）。</summary>
        private static readonly List<float> BandPercentages = new List<float> { 1f / 3f, 2f / 3f };
        /// <summary>滑条仅 4 档：0、1、2、3（对应 0、1/3、2/3、1）。</summary>
        private const int Increments = 3;

        public Gizmo_AXF12AmmoReserve(CompAXF12AmmoReserve comp)
        {
            this.comp = comp;
            this.targetValue = comp.Props.maxCount > 0 ? (float)comp.maxToFill / comp.Props.maxCount : 1f;
            Order = -99f;
        }

        public override float GetWidth(float maxWidth) => 160f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect rect2 = rect.ContractedBy(10f);
            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;
            TaggedString labelCap = comp.ReserveThingDef != null ? comp.ReserveThingDef.LabelCap : (comp.Props.reserveThingDefName ?? "Ammo");
            float num = Text.CalcHeight(labelCap, rect2.width);
            Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, num);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect3, labelCap);
            Text.Anchor = TextAnchor.UpperLeft;

            if (!draggingBar)
                targetValue = comp.Props.maxCount > 0 ? (float)comp.maxToFill / comp.Props.maxCount : 1f;
            lastTargetValue = targetValue;

            float num2 = rect2.height - rect3.height;
            float num3 = num2 - 4f;
            float num4 = (num2 - num3) / 2f;
            Rect rect4 = new Rect(rect2.x, rect3.yMax + num4, rect2.width, num3);

            Widgets.DraggableBar(rect4, BarTex, BarHighlightTex, EmptyBarTex, DragBarTex, ref draggingBar, comp.PercentageFull, ref targetValue, BandPercentages, Increments, 0f, 1f);

            Text.Anchor = TextAnchor.MiddleCenter;
            rect4.y -= 2f;
            Widgets.Label(rect4, comp.CurrentCount.ToString() + " / " + comp.Props.maxCount.ToString());
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect4, () => GetResourceBarTip(), Gen.HashCombineInt(comp.GetHashCode(), 34242369));

            if (lastTargetValue != targetValue)
                comp.maxToFill = Mathf.Clamp(Mathf.RoundToInt(targetValue * comp.Props.maxCount), 0, comp.Props.maxCount);

            return new GizmoResult(GizmoState.Clear);
        }

        private string GetResourceBarTip()
        {
            return "DMSL_AXF12_AmmoReserve_Tooltip".Translate();
        }
    }
}
