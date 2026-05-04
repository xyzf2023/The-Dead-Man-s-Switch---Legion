using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 武器模式切换Gizmo补丁
    /// 当选中装备了CompSecondaryVerb_Rework武器的Pawn时，将武器的模式切换Gizmo转发到Pawn的Gizmo列表中
    /// 无需修改任何种族定义，完全通过武器绑定实现
    /// </summary>
    [HarmonyPatch(typeof(Thing), "GetGizmos")]
    public static class Thing_GetGizmos_WeaponModeSwitch_Patch
    {
        static void Postfix(Thing __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance is not Pawn pawn || pawn.equipment?.Primary is not ThingWithComps weapon)
                return;

            var comp = weapon.GetComp<CompSecondaryVerb_Rework>();
            if (comp == null) return;

            var extra = comp.CompGetGizmosExtra();
            if (extra == null) return;

            __result = __result == null ? extra : __result.Concat(extra);
        }
    }
}
