using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 消防泡沫波纹：灭火与污渍生成工具（逻辑完全分离）。
    /// </summary>
    public static class DMSL_FirefoamUtility
    {
        /// <summary>
        /// 足以熄灭最大 fireSize（1.75）火焰的默认灭火强度（DamageWorker_Extinguish：fireSize -= amount * 0.01）。
        /// </summary>
        public const float DefaultExtinguishDamage = 1000f;

        private static readonly List<Thing> thingsAtCellBuffer = new List<Thing>();

        /// <summary>
        /// 对指定格内所有 <see cref="Fire"/> 施加 Extinguish 伤害；不检查视线、墙体或可通行性。
        /// </summary>
        public static void ExtinguishFiresAtCell(IntVec3 cell, Map map, float extinguishDamage)
        {
            if (map == null || !cell.InBounds(map))
                return;

            float amount = extinguishDamage > 0f ? extinguishDamage : DefaultExtinguishDamage;
            thingsAtCellBuffer.Clear();
            thingsAtCellBuffer.AddRange(cell.GetThingList(map));

            DamageInfo dinfo = new DamageInfo(DamageDefOf.Extinguish, amount);
            for (int i = 0; i < thingsAtCellBuffer.Count; i++)
            {
                Thing? thing = thingsAtCellBuffer[i];
                if (thing == null || thing.Destroyed)
                    continue;
                if (thing is Fire fire)
                {
                    try
                    {
                        fire.TakeDamage(dinfo);
                    }
                    catch
                    {
                        // 单格灭火失败不影响波纹继续
                    }
                }
            }
        }

        /// <summary>
        /// 在指定格尝试生成一次消防泡沫污渍；失败不影响调用方。
        /// </summary>
        public static void TryPlaceFirefoamFilth(IntVec3 cell, Map map, bool placeFilth)
        {
            if (!placeFilth || map == null || !cell.InBounds(map))
                return;

            try
            {
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_FireFoam, 1, FilthSourceFlags.None, shouldPropagate: false);
            }
            catch
            {
                // 污渍生成失败不影响灭火
            }
        }
    }
}
