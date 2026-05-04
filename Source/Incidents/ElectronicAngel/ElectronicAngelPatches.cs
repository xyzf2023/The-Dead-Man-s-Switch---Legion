// ============================================================================
// 文件：ElectronicAngelPatches.cs
// 说明：钩住原版黑衣人事件（StrangerInBlackJoin），在其触发时
//       改为发送「讯息：电子天使」选择信，由玩家决定请求庇护或保持沉默。
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion.Incidents.ElectronicAngel
{
    // Harmony 通过方法名字符串反射内部受保护方法，避免直接引用导致的访问级别编译错误
    [HarmonyPatch(typeof(IncidentWorker_WandererJoin), "TryExecuteWorker")]
    public static class Patch_IncidentWorker_WandererJoin_TryExecuteWorker_ElectronicAngel
    {
        public static bool Prefix(IncidentWorker_WandererJoin __instance, ref bool __result, IncidentParms parms)
        {
            // 只拦截黑衣人事件 StrangerInBlackJoin，其余 WandererJoin 保持原样
            if (__instance.def == null || __instance.def.defName != "StrangerInBlackJoin")
                return true;

            // 检查 MOD 设置：若未开启电子天使支援，则直接走原版黑衣人流程
            if (DMSL_ModSettings.settings == null || !DMSL_ModSettings.settings.enableElectronicAngelSupport)
                return true;

            // 如未解除叙事者限制，仅在特定叙事者（艾丽萨 / 拉斐尔）时启用电子天使分支
            if (!DMSL_ModSettings.settings.electronicAngelNoStorytellerLimit)
            {
                StorytellerDef? storytellerDef = Find.Storyteller?.def;
                if (storytellerDef == null)
                    return true;
                string defName = storytellerDef.defName ?? string.Empty;
                if (string.IsNullOrEmpty(defName) || (defName != "DMSL_Storyteller_Elisa" && defName != "DMSL_Storyteller_Raphael"))
                    return true;
            }

            if (parms.target is not Map map || !map.IsPlayerHome)
                return true;

            // 发送「讯息：电子天使」倒计时信
            var letter = new ChoiceLetter_ElectronicAngelSignal(map);
            letter.Send();

            // 若该地图存在半数倒地“未知讯号”生成状态组件，则直接进入冷却，避免短时间内再次生成未知讯号
            var halfDownedState = map.GetComponent<DMS_Legion.Incidents.UnknownMechSupport.DMSL_MapComponent_HalfDownedLetterState>();
            if (halfDownedState == null)
            {
                halfDownedState = new DMS_Legion.Incidents.UnknownMechSupport.DMSL_MapComponent_HalfDownedLetterState(map);
                map.components.Add(halfDownedState);
            }
            halfDownedState.MarkSent();

            // 若 300 tick 延迟内已经排队了“未知讯号”信件，则将其从信件栈中移除，避免之后仍然弹出
            LetterStack letterStack = Find.LetterStack;
            if (letterStack != null)
            {
                var letters = letterStack.LettersListForReading;
                for (int i = letters.Count - 1; i >= 0; i--)
                {
                    if (letters[i] is DMS_Legion.Incidents.UnknownMechSupport.ChoiceLetter_UnknownSignal unknown &&
                        unknown.triggerMap == map)
                    {
                        letterStack.RemoveLetter(letters[i]);
                    }
                }
            }

            // 视为事件已被自定义逻辑处理，阻止原版立即生成黑衣人
            __result = true;
            return false;
        }
    }
}

