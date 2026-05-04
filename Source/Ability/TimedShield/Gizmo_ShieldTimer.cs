using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    [StaticConstructorOnStartup]
    public class Gizmo_ShieldTimer : Gizmo
    {
        private float currentSeconds;
        private float maxSeconds;
        private bool isActive;
        private string label;
        
        private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
        private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);
        
        public Gizmo_ShieldTimer(float currentSeconds, float maxSeconds, bool isActive, string label = "")
        {
            this.currentSeconds = currentSeconds;
            this.maxSeconds = maxSeconds;
            this.isActive = isActive;
            this.label = string.IsNullOrEmpty(label) ? "DMSL_ShieldTimer_Label".Translate() : label;
            this.Order = -100f;
        }
        
        public override float GetWidth(float maxWidth) => 140f;
        
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect rect2 = rect.ContractedBy(6f);
            
            Widgets.DrawWindowBackground(rect);
            
            Rect labelRect = rect2;
            labelRect.height = rect.height / 2f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(labelRect, label);
            
            float fillPercent = maxSeconds > 0f ? Mathf.Clamp01(currentSeconds / maxSeconds) : 0f;
            Rect barRect = rect2;
            barRect.yMin = rect2.y + rect2.height / 2f;
            
            Widgets.FillableBar(barRect, fillPercent, FullShieldBarTex, EmptyShieldBarTex, doBorder: false);
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, $"{currentSeconds:F1}s / {maxSeconds:F1}s");
            
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, "DMSL_ShieldTimer_Tooltip".Translate());
            }
            
            return new GizmoResult(GizmoState.Clear);
        }
    }
}

