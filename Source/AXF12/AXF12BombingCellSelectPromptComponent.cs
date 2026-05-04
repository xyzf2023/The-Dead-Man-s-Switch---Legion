using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 轰炸两步流程之第二步：挂在目标地图上，在 OnGUI 中绘制「选择轰炸落点」按钮。
    /// 用户点击按钮后（正常 UI 上下文）再启动单格选点器，避免在世界选格回调中启动导致选点器不出现。
    /// </summary>
    public class AXF12BombingCellSelectPromptComponent : MapComponent
    {
        /// <summary>为 true 时不再绘制按钮；设置新 pending 时由 Comp 重置为 false。</summary>
        public bool shouldRemove;

        public AXF12BombingCellSelectPromptComponent(Map map) : base(map)
        {
        }

        public override void MapComponentOnGUI()
        {
            if (shouldRemove)
            {
                return;
            }

            if (Current.Game.CurrentMap != map)
            {
                return;
            }

            if (Comp_AXF12ReconLaunch.PendingBombingComp == null)
            {
                return;
            }

            float w = 220f;
            float h = 36f;
            Rect rect = new Rect(UI.screenWidth - w - 20f, 20f, w, h);
            if (Widgets.ButtonText(rect, "DMSL_AXF12_SelectBombCell".Translate()))
            {
                Comp_AXF12ReconLaunch comp = Comp_AXF12ReconLaunch.PendingBombingComp;
                PlanetTile tile = Comp_AXF12ReconLaunch.PendingBombingTargetTile;
                int count = Comp_AXF12ReconLaunch.PendingBombingCount;
                string supportDefName = Comp_AXF12ReconLaunch.PendingBombingSupportTypeDefName ?? "DMSL_AerialSupport_AXF12Bombing_Once";
                Comp_AXF12ReconLaunch.ClearPendingBombing();
                comp.StartBombingCellTargeter(tile, count, supportDefName);
                shouldRemove = true;
            }
        }
    }
}
