using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 尘世：允许右键装备/穿戴/放下人类武器与护甲。
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider), "SelectedPawnValid")]
    public static class TerraPatch_FloatMenuOptionProvider_SelectedPawnValid
    {
        static bool IsEquipmentRelated(FloatMenuOptionProvider p)
        {
            return p is FloatMenuOptionProvider_Equip
                || p is FloatMenuOptionProvider_Wear
                || p is FloatMenuOptionProvider_DropEquipment
                || p is FloatMenuOptionProvider_FromThing;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result, FloatMenuOptionProvider __instance)
        {
            if (__result || pawn == null || !TerraDefNames.IsTerra(pawn)) return;
            if (!IsEquipmentRelated(__instance)) return;
            if (!pawn.RaceProps.IsMechanoid) return;

            var tr = Traverse.Create(__instance);
            if (tr.Property("MechanoidCanDo").GetValue<bool>()) return;

            try
            {
                bool drafted = tr.Property("Drafted").GetValue<bool>();
                bool undrafted = tr.Property("Undrafted").GetValue<bool>();
                bool requiresManipulation = tr.Property("RequiresManipulation").GetValue<bool>();
                bool draftOk = (drafted || !pawn.Drafted) && (undrafted || pawn.Drafted);
                bool manipOk = !requiresManipulation || (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);
                if (draftOk && manipOk) __result = true;
            }
            catch { }
        }
    }

    /// <summary>
    /// 尘世：允许创建装备渲染节点（HumanlikeOnly 时也创建）。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), "SetupDynamicNodes")]
    public static class TerraPatch_SetupDynamicNodes_Apparel
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode != OpCodes.Callvirt || codes[i].operand is not MethodInfo mi
                    || mi.Name != "get_Humanlike" || mi.DeclaringType != typeof(RaceProperties))
                    continue;

                var pawnField = typeof(PawnRenderTree).GetField("pawn", BindingFlags.Public | BindingFlags.Instance);
                var isTerra = typeof(TerraDefNames).GetMethod("IsTerra", BindingFlags.Public | BindingFlags.Static);
                if (pawnField == null || isTerra == null) break;

                var ins = new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, pawnField),
                    new CodeInstruction(OpCodes.Call, isTerra),
                    new CodeInstruction(OpCodes.Or)
                };
                codes.InsertRange(i + 1, ins);
                break;
            }
            return codes;
        }
    }

    /// <summary>
    /// 尘世：护甲图形使用男性体型获取。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNode_Apparel), "GraphicsFor")]
    public static class TerraPatch_GraphicsFor_Apparel
    {
        [HarmonyPrefix]
        static bool Prefix(ref IEnumerable<Graphic> __result, Pawn pawn, PawnRenderNode_Apparel __instance)
        {
            if (!TerraDefNames.IsTerra(pawn) || __instance.apparel == null)
                return __instance.apparel == null ? false : true;

            bool forStatue = pawn?.Drawer?.renderer?.StatueColor != null;
            if (ApparelGraphicRecordGetter.TryGetGraphicApparel(__instance.apparel, BodyTypeDefOf.Male, forStatue, out var rec) && rec.graphic != null)
                __result = new[] { rec.graphic };
            else
                __result = System.Linq.Enumerable.Empty<Graphic>();
            return false;
        }
    }

    /// <summary>
    /// 尘世：身体与头部装备节点允许绘制。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Body), "CanDrawNow")]
    public static class TerraPatch_CanDrawNow_Body
    {
        [HarmonyPrefix]
        static bool Prefix(PawnRenderNode node, PawnDrawParms parms, ref bool __result)
        {
            if (TerraDefNames.IsTerra(parms.pawn)) { __result = true; return false; }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Head), "CanDrawNow")]
    public static class TerraPatch_CanDrawNow_Head
    {
        [HarmonyPrefix]
        static bool Prefix(PawnRenderNode n, PawnDrawParms parms, ref bool __result)
        {
            if (TerraDefNames.IsTerra(parms.pawn)) { __result = true; return false; }
            return true;
        }
    }

    /// <summary>
    /// 尘世：将装备节点加入渲染树。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), "ShouldAddNodeToTree")]
    public static class TerraPatch_ShouldAddNodeToTree
    {
        [HarmonyPostfix]
        static void Postfix(ref bool __result, PawnRenderNodeProperties props, PawnRenderTree __instance)
        {
            if (__result) return;
            if ((props.workerClass != typeof(PawnRenderNodeWorker_Apparel_Body) && props.workerClass != typeof(PawnRenderNodeWorker_Apparel_Head)))
                return;
            if (TerraDefNames.IsTerra(__instance.pawn)) __result = true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderTree), "SetupDynamicNodes")]
    public static class TerraPatch_SetupDynamicNodes_Postfix
    {
        [HarmonyPostfix]
        static void Postfix(PawnRenderTree __instance)
        {
            if (!TerraDefNames.IsTerra(__instance.pawn)) return;
            Traverse.Create(__instance).Method("SetupApparelNodes").GetValue();
        }
    }
}
