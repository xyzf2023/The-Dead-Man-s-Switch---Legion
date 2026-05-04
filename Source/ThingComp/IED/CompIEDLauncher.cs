using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;


namespace DMS_Legion
{
    /// <summary>
    /// IED 武器组件：继承 CompEquippable，装备时通过 CompGetEquippedGizmosExtra 显示引爆 Gizmo
    /// （原版选中 pawn 时只从 PrimaryEq.CompGetEquippedGizmosExtra() 取 Gizmo，故须在此提供）。
    /// </summary>
    public class CompIEDLauncher : CompEquippable
    {
        /// <summary>无可用 IED 时禁用引爆按钮的显示理由（可做翻译键）。</summary>
        public static readonly string NoIEDToDetonateReason = "DMSL_IED_NoIEDToDetonate";
        /// <summary>无 IED 可清除时禁用清除按钮的显示理由（可做翻译键）。</summary>
        public static readonly string NoIEDToClearReason = "DMSL_IED_NoIEDToClear";

        private static readonly List<Projectile_IED> tmpDeployed = new List<Projectile_IED>();

        public CompProperties_IEDLauncher Props => (CompProperties_IEDLauncher)props;

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetEquippedGizmosExtra())
                yield return g;

            ThingWithComps weapon = parent as ThingWithComps;
            if (weapon == null || Props?.projectileDef == null)
                yield break;

            Pawn? holder = (weapon.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            Map? map = holder?.Map;
            if (map == null)
                yield break;

            int weaponId = weapon.thingIDNumber;
            ThingDef? pdef = Props.projectileDef;
            if (pdef == null)
                yield break;
            int count = Projectile_IED.GetDeployedCount(map, pdef, weaponId);

            var cmd = new Command_Action
            {
                defaultLabel = "DMSL_IED_Detonate".Translate(),
                defaultDesc = "DMSL_IED_DetonateDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/IED/IEDBoom"),
                action = () =>
                {
                    if (holder != null && (DMSL_ModSettings.settings == null || DMSL_ModSettings.settings.playIEDDetonateSound) && DefDatabase<SoundDef>.GetNamedSilentFail("DMSL_IED") is SoundDef soundDef)
                        soundDef.PlayOneShot(new TargetInfo(holder));
                    Projectile_IED.GetDeployedForWeapon(map, pdef, weaponId, tmpDeployed);
                    for (int i = 0; i < tmpDeployed.Count; i++)
                        tmpDeployed[i].TriggerImmediate();
                }
            };
            if (count <= 0)
                cmd.Disable(NoIEDToDetonateReason.Translate());
            yield return cmd;

            // 清除 IED Gizmo：清除场上此武器关联的所有 IED 飞行物（与引爆同一绑定逻辑）
            int clearCount = 0;
            Projectile_IED.GetAllForWeapon(map, pdef, weaponId, tmpDeployed);
            clearCount = tmpDeployed.Count;
            var clearCmd = new Command_Action
            {
                defaultLabel = "DMSL_IED_Clear".Translate(),
                defaultDesc = "DMSL_IED_ClearDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/IED/IEDClear"),
                action = () =>
                {
                    Projectile_IED.GetAllForWeapon(map, pdef, weaponId, tmpDeployed);
                    for (int i = 0; i < tmpDeployed.Count; i++)
                        tmpDeployed[i].Destroy();
                }
            };
            if (clearCount <= 0)
                clearCmd.Disable(NoIEDToClearReason.Translate());
            yield return clearCmd;
        }
    }
}
