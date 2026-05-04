using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 遥控 IED 抛射体：落地不爆炸，等收到引爆指令后 600 tick 倒计时再爆炸。
    /// 通过 launcherWeaponId 与发射它的武器实例绑定，仅该武器的 Gizmo 可引爆。
    /// </summary>
    public class Projectile_IED : Projectile_Explosive
    {
        private const int DetonationDelayTicks = 600;

        private static readonly HashSet<int> tmpWeaponIdsOnMap = new HashSet<int>();
        private static readonly HashSet<int> tmpLauncherIds = new HashSet<int>();
        private static readonly List<Projectile_IED> tmpOrphansToDestroy = new List<Projectile_IED>();

        /// <summary>发射此抛射体的武器实例的 thingIDNumber，用于 Gizmo 只引爆“当前武器”部署的 IED。</summary>
        public int launcherWeaponId;

        /// <summary>遥控引爆倒计时（tick），0 表示尚未开始；开始后每 tick 递减，≤0 时爆炸。</summary>
        private int remoteDetonationCountdown;

        public bool CountdownStarted => remoteDetonationCountdown > 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref launcherWeaponId, "launcherWeaponId", 0);
            Scribe_Values.Look(ref remoteDetonationCountdown, "remoteDetonationCountdown", 0);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing? equipment = null, ThingDef? targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            launcherWeaponId = equipment?.thingIDNumber ?? 0;
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (blockedByShield)
            {
                base.Impact(hitThing, blockedByShield: true);
                return;
            }
            // 落地不爆炸：不调用 base.Impact，不 Destroy，仅设 landed，不设倒计时
            landed = true;
            if (def.projectile.landedEffecter != null)
                def.projectile.landedEffecter.Spawn(Position, Map)?.Cleanup();
            // 仅在每次新 IED 落地时执行一次孤儿检测，清理对应武器已不存在的飞行物
            TryCleanupOrphanedProjectiles(Map);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!landed)
                return;
            if (remoteDetonationCountdown > 0)
            {
                remoteDetonationCountdown -= delta;
                if (remoteDetonationCountdown <= 0)
                    Explode();
            }
        }

        /// <summary>由武器 Gizmo 调用：开始 600 tick 倒计时，结束后爆炸。</summary>
        public void StartCountdown()
        {
            if (landed && remoteDetonationCountdown <= 0)
            {
                remoteDetonationCountdown = DetonationDelayTicks;
                GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(this, DamageDef, launcher?.Faction, launcher);
            }
        }

        /// <summary>由武器 Gizmo 调用：立即引爆，无倒计时。</summary>
        public void TriggerImmediate()
        {
            if (landed)
                Explode();
        }

        /// <summary>当前地图上由指定武器发射、已落地且尚未开始倒计时的 IED 数量（用于上限与 Gizmo 禁用）。</summary>
        public static int GetDeployedCount(Map map, ThingDef projectileDef, int launcherWeaponId)
        {
            if (map == null || projectileDef == null)
                return 0;
            int count = 0;
            var list = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Projectile_IED p && p.def == projectileDef && p.launcherWeaponId == launcherWeaponId && p.landed && !p.CountdownStarted)
                    count++;
            }
            return count;
        }

        /// <summary>当前地图上由指定武器发射、已落地且尚未开始倒计时的 IED 列表（用于引爆）。</summary>
        public static void GetDeployedForWeapon(Map map, ThingDef projectileDef, int launcherWeaponId, List<Projectile_IED> outList)
        {
            outList.Clear();
            if (map == null || projectileDef == null)
                return;
            var list = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Projectile_IED p && p.def == projectileDef && p.launcherWeaponId == launcherWeaponId && p.landed && !p.CountdownStarted)
                    outList.Add(p);
            }
        }

        /// <summary>当前地图上由指定武器发射的所有 IED 抛射体（含飞行中与已落地），用于清除 Gizmo。</summary>
        public static void GetAllForWeapon(Map map, ThingDef projectileDef, int launcherWeaponId, List<Projectile_IED> outList)
        {
            outList.Clear();
            if (map == null || projectileDef == null)
                return;
            var list = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Projectile_IED p && p.def == projectileDef && p.launcherWeaponId == launcherWeaponId)
                    outList.Add(p);
            }
        }

        /// <summary>
        /// 孤儿清理：场上存在 IED 飞行物但对应武器已不在本图（被毁、卖掉等）时，销毁这些飞行物。
        /// 仅在每次新 IED 落地时由 Impact 调用一次，避免每 tick 检测带来的性能损耗。
        /// </summary>
        private static void TryCleanupOrphanedProjectiles(Map map)
        {
            if (map == null)
                return;
            var projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            tmpLauncherIds.Clear();
            tmpOrphansToDestroy.Clear();
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i] is Projectile_IED p && p.landed)
                    tmpLauncherIds.Add(p.launcherWeaponId);
            }
            if (tmpLauncherIds.Count == 0)
                return;
            tmpWeaponIdsOnMap.Clear();
            var allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.Everything);
            for (int i = 0; i < allThings.Count; i++)
                tmpWeaponIdsOnMap.Add(allThings[i].thingIDNumber);
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.equipment != null)
                {
                    var eqList = pawn.equipment.AllEquipmentListForReading;
                    for (int j = 0; j < eqList.Count; j++)
                        tmpWeaponIdsOnMap.Add(eqList[j].thingIDNumber);
                }
                if (pawn.inventory != null)
                {
                    var inv = pawn.inventory.innerContainer;
                    for (int j = 0; j < inv.Count; j++)
                        tmpWeaponIdsOnMap.Add(inv[j].thingIDNumber);
                }
            }
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i] is Projectile_IED p && p.landed && !tmpWeaponIdsOnMap.Contains(p.launcherWeaponId))
                    tmpOrphansToDestroy.Add(p);
            }
            for (int i = 0; i < tmpOrphansToDestroy.Count; i++)
                tmpOrphansToDestroy[i].Destroy();
        }
    }
}
