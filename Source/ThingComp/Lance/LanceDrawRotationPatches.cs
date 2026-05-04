using HarmonyLib;
using Verse;
using UnityEngine;
using RimWorld;

namespace DMS_Legion
{
    /// <summary>
    /// 骑士长枪持握时使用自定义渲染，完全由 CompLanceDrawRotation 的 XML 角度控制旋转，
    /// 不经过原版 DrawEquipmentAiming 的 equippedAngleOffset 与分支逻辑。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawCarriedWeapon))]
    public static class Patch_DrawCarriedWeapon
    {
        // 与原版 PawnRenderUtility 一致的持握位置偏移（原版为 private，此处复制以便自定义绘制）
        private static readonly Vector3 EqLocNorth = new Vector3(0f, 0f, -0.11f);
        private static readonly Vector3 EqLocEast = new Vector3(0.22f, 0f, -0.22f);
        private static readonly Vector3 EqLocSouth = new Vector3(0f, 0f, -0.22f);
        private static readonly Vector3 EqLocWest = new Vector3(-0.22f, 0f, -0.22f);

        /// <summary>
        /// 持握时 mesh 在世界空间中的基准旋转（度）。设为 0 表示以贴图原文件为基准，仅用 XML 偏移控制。
        /// </summary>
        private const float BaseCarriedAngle = 0f;

        /// <summary>
        /// West 朝向时传入 DrawWeaponMesh 的角度，使使用 plane10Flip 且最终旋转 = 127 - 180 - equippedAngleOffset。
        /// 返回 127 - equippedAngleOffset（归一化到 0~360），落在 (160,200) 时由 DrawWeaponMesh 用 flip。
        /// </summary>
        private static float WestAngle(Thing eq)
        {
            float deg = 127f - eq.def.equippedAngleOffset;
            return (deg % 360f + 360f) % 360f;
        }

        [HarmonyPrefix]
        public static bool Prefix(ThingWithComps? weapon, Vector3 drawPos, Rot4 facing, float equipmentDrawDistanceFactor)
        {
            if (weapon == null)
                return true;
            CompLanceDrawRotation? comp = weapon.GetComp<CompLanceDrawRotation>();
            if (comp == null)
                return true; // 非长枪：走原版

            Vector3 loc = drawPos + EquipmentDrawOffset(facing, equipmentDrawDistanceFactor);
            if (facing == Rot4.East)
            {
                loc.x += comp.Props.eastDrawOffsetX;
                loc.z += comp.Props.eastDrawOffsetZ;
            }
            else if (facing == Rot4.West)
            {
                loc.x += comp.Props.westDrawOffsetX;
                loc.z += comp.Props.westDrawOffsetZ;
            }
            else if (facing == Rot4.South)
            {
                loc.x += comp.Props.southDrawOffsetX;
                loc.z += comp.Props.southDrawOffsetZ;
            }
            else if (facing == Rot4.North)
            {
                loc.x += comp.Props.northDrawOffsetX;
                loc.z += comp.Props.northDrawOffsetZ;
            }
            float angle = CarriedAngleForFacing(weapon!, facing, comp.Props);
            DrawWeaponMesh(weapon, loc, angle);
            return false; // 已自定义绘制，跳过原版
        }

        private static Vector3 EquipmentDrawOffset(Rot4 facing, float factor)
        {
            switch (facing.AsInt)
            {
                case 0: return EqLocNorth * factor;
                case 1: return EqLocEast * factor;
                case 2: return EqLocSouth * factor;
                case 3: return EqLocWest * factor;
                default: return EqLocSouth * factor;
            }
        }

        private static float CarriedAngleForFacing(Thing weapon, Rot4 facing, CompProperties_LanceDrawRotation props)
        {
            float deg;
            switch (facing.AsInt)
            {
                case 0: deg = BaseCarriedAngle + props.northRotationOffset; break;
                case 1: deg = BaseCarriedAngle; break;
                case 2: deg = BaseCarriedAngle + props.southRotationOffset; break;
                case 3: deg = WestAngle(weapon); break;
                default: deg = BaseCarriedAngle; break;
            }
            return (deg % 360f + 360f) % 360f;
        }

        private static void DrawWeaponMesh(Thing eq, Vector3 drawLoc, float angleDeg)
        {
            Mesh mesh;
            float num = angleDeg;
            if (num > 20f && num < 160f)
                mesh = MeshPool.plane10;
            else if (num > 200f && num < 340f)
            {
                mesh = MeshPool.plane10Flip;
                num = (num - 180f + 360f) % 360f;
            }
            else if (num >= 160f && num <= 200f)
            {
                // West 等需镜像：用 plane10Flip，旋转 = angleDeg - 180（与原版 217 分支一致）
                mesh = MeshPool.plane10Flip;
                num = (angleDeg - 180f + 360f) % 360f;
            }
            else
                mesh = MeshPool.plane10;

            Material mat = eq.Graphic is Graphic_StackCount gsc
                ? gsc.SubGraphicForStackCount(1, eq.def).MatSingleFor(eq)
                : eq.Graphic.MatSingleFor(eq);
            Vector3 scale = new Vector3(eq.Graphic.drawSize.x, 0f, eq.Graphic.drawSize.y);
            Matrix4x4 m = Matrix4x4.TRS(drawLoc, Quaternion.AngleAxis(num, Vector3.up), scale);
            Graphics.DrawMesh(mesh, m, mat, 0);
        }
    }
}
