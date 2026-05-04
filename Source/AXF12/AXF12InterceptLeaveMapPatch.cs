using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMS_Legion.AerialRaid.AerialRaidComponents;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 拦截发射时：飞出地图不创建 WorldObject，改为将运输体写入拦截缓存并计算返航时间。
    /// 流程依据原版 RimWorld.FlyShipLeaving.LeaveMap()（Rimworld-Source\1.6\Code\Assembly-CSharp\RimWorld\FlyShipLeaving.cs）。
    /// </summary>
    [HarmonyPatch]
    public static class AXF12InterceptLeaveMapPatch
    {
        private static MethodBase? _targetMethod;
        private static MethodBase TargetMethod()
        {
            if (_targetMethod != null) return _targetMethod;
            var type = AccessTools.TypeByName("RimWorld.FlyShipLeaving");
            if (type == null) return null!;
            _targetMethod = AccessTools.Method(type, "LeaveMap");
            return _targetMethod;
        }

        static bool Prepare() => TargetMethod() != null;

        [HarmonyTargetMethod]
        static MethodBase PatchTarget() => TargetMethod();

        [HarmonyPrefix]
        static bool Prefix(object __instance)
        {
            if (!AXF12LaunchContext.IsInterceptLaunch)
                return true;

            // 原版：groupID、destinationTile 为 public 字段；Contents 为 public 属性；alreadyLeft 为 private 字段
            var flyType = __instance.GetType();
            var groupIDField = AccessTools.Field(flyType, "groupID");
            var destinationTileField = AccessTools.Field(flyType, "destinationTile");
            var contentsProp = AccessTools.Property(flyType, "Contents");
            var alreadyLeftField = AccessTools.Field(flyType, "alreadyLeft");

            if (groupIDField == null || destinationTileField == null || contentsProp == null)
            {
                Log.Warning("[DMS_Legion][AXF12] 拦截 LeaveMap：反射 groupID/destinationTile/Contents 失败，按原逻辑处理。");
                return true;
            }

            int groupID = (int)groupIDField.GetValue(__instance);
            var destinationTile = (PlanetTile)(destinationTileField.GetValue(__instance) ?? PlanetTile.Invalid);

            if (groupID < 0)
            {
                Log.Warning("[DMS_Legion][AXF12] 拦截 LeaveMap：groupID 无效，按原逻辑处理。");
                return true;
            }
            if (!destinationTile.Valid)
            {
                Log.Warning("[DMS_Legion][AXF12] 拦截 LeaveMap：destinationTile 无效，按原逻辑处理。");
                return true;
            }

            var map = (__instance as Thing)?.Map;
            if (map == null)
            {
                Log.Warning("[DMS_Legion][AXF12] 拦截 LeaveMap：Map 为空。");
                return true;
            }

            // 原版：Lord 通过 TransporterUtility.FindLord(groupID, Map) 获取并移除
            Lord? lord = TransporterUtility.FindLord(groupID, map);
            if (lord != null)
                map.lordManager.RemoveLord(lord);

            // 原版：tmpActiveTransporters.AddRange(Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter))，再筛 FlyShipLeaving 且 groupID 相同
            var entry = new AXF12InterceptCacheEntry
            {
                OriginMap = AXF12LaunchContext.OriginMap,
                OriginCell = AXF12LaunchContext.OriginCell,
                TransportShipDefName = "DMSL_AXF12_OffsetConfig"
            };

            var transporters = map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter);
            foreach (var thing in transporters.ToList())
            {
                if (thing.GetType() != flyType) continue;
                int g = (int)groupIDField.GetValue(thing);
                if (g != groupID) continue;

                var c = contentsProp.GetValue(thing) as ActiveTransporterInfo;
                if (c == null) continue;

                // 原版：对 AddTransporter(contents, true) 等价——对舱内 Pawn 调 ExitMap；此处只收集 Contents，ExitMap 在 AddTransporter 内做，我们下面统一做
                var inner = c.innerContainer;
                if (inner != null)
                {
                    foreach (var p in inner.ToList())
                    {
                        if (p is Pawn pawn)
                            pawn.ExitMap(false, Rot4.Invalid);
                    }
                }
                entry.Transporters.Add(c);

                alreadyLeftField?.SetValue(thing, true);
                if (contentsProp.CanWrite)
                    contentsProp.SetValue(thing, null);
                thing.Destroy(DestroyMode.Vanish);
            }

            int nowTick = Find.TickManager.TicksGame;
            Map? targetMap = null;
            int remainingTicks = 0;
            foreach (var m in Find.Maps)
            {
                var comp = m?.GetComponent<AerialRaidPrePhaseComponent>();
                if (comp == null) continue;
                int r = comp.GetRemainingTicks();
                if (r > 0)
                {
                    targetMap = m;
                    remainingTicks = r;
                    break;
                }
            }
            if (targetMap == null)
                targetMap = AXF12LaunchContext.OriginMap ?? Find.CurrentMap;
            entry.TargetMap = targetMap;

            // 执行时间 = ceil((空袭倒计时 - 600) / 2)；不足 600 tick 时取 0；无倒计时时 600 tick 后再执行降落流程
            int delayTicks = remainingTicks > 600
                ? (int)System.Math.Ceiling((remainingTicks - 600) / 2.0)
                : (remainingTicks == 0 ? 600 : 0);
            entry.EndTick = nowTick + delayTicks;

            if (entry.Transporters.Count == 0)
                Log.Warning("[DMS_Legion][AXF12] 拦截 LeaveMap：未收集到任何运输体，缓存项将不被加入。");

            AXF12InterceptCache.Instance?.AddEntry(entry);
            AXF12LaunchContext.Reset();
            return false;
        }
    }
}
