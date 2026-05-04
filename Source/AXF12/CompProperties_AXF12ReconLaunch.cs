using Verse;

namespace DMS_Legion.AXF12
{
    public class CompProperties_AXF12ReconLaunch : CompProperties
    {
        public string supportTypeDefName = "DMSL_AerialSupport_AXF12Recon";
        public string transportShipDefName = "DMSL_AXF12_OffsetConfig";
        public string worldObjectDefName = "DMSL_AXF12_OffsetConfig_Traveling";
        public string gizmoLabel = "DMSL_AXF12_ReconLaunch_GizmoLabel";
        public string gizmoDesc = "DMSL_AXF12_ReconLaunch_GizmoDesc";
        public string gizmoIconPath = "UI/Gizmo/Recon";
        public string interceptGizmoLabel = "DMSL_AXF12_ReconLaunch_InterceptGizmoLabel";
        public string interceptGizmoDesc = "DMSL_AXF12_ReconLaunch_InterceptGizmoDesc";
        public string interceptGizmoIconPath = "UI/Gizmo/Intercept";
        public string bombGizmoLabel = "DMSL_AXF12_ReconLaunch_BombGizmoLabel";
        public string bombGizmoDesc = "DMSL_AXF12_ReconLaunch_BombGizmoDesc";
        public string bombGizmoIconPath = "UI/Gizmo/Bomb";

        public CompProperties_AXF12ReconLaunch()
        {
            compClass = typeof(Comp_AXF12ReconLaunch);
        }
    }
}
