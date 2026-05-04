using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DMS_Legion
{
    public class CompTimedShieldController : ThingComp
    {
        private const int AIScanIntervalTicks = 120;
        private const int EnemyScanRadiusCells = 20;

        private bool isShieldActive = false;
        private float currentChargeSeconds = 30f;
        private float maxChargeSeconds = 30f;
        private CachedTexture? shieldOnIcon;
        private CachedTexture? shieldOffIcon;

        public CompProperties_TimedShieldController? Props => props as CompProperties_TimedShieldController;
        public bool IsShieldActive => isShieldActive;
        public float CurrentChargeSeconds => currentChargeSeconds;
        public float MaxChargeSeconds => maxChargeSeconds;
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeIfReady();
        }
        
        private void InitializeIfReady()
        {
            if (Props == null || parent == null) return;
            
            try
            {
                shieldOnIcon = new CachedTexture(Props.shieldOnIconPath);
                shieldOffIcon = new CachedTexture(Props.shieldOffIconPath);
            }
            catch
            {
                // 图标加载失败时保持null，GetGizmos会检查并跳过
            }
            
            if (maxChargeSeconds != Props.maxDurationSeconds)
            {
                float percent = maxChargeSeconds > 0f ? currentChargeSeconds / maxChargeSeconds : 0f;
                maxChargeSeconds = Props.maxDurationSeconds;
                currentChargeSeconds = percent * maxChargeSeconds;
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            if (Props == null || parent == null)
            {
                InitializeIfReady();
                return;
            }
            
            if (isShieldActive)
            {
                currentChargeSeconds -= (1f / 60f);
                if (currentChargeSeconds <= 0f)
                {
                    currentChargeSeconds = 0f;
                    isShieldActive = false;
                }
            }
            else if (currentChargeSeconds < maxChargeSeconds)
            {
                currentChargeSeconds += (Props.rechargeRatePerSecond / 60f);
                if (currentChargeSeconds > maxChargeSeconds)
                    currentChargeSeconds = maxChargeSeconds;
            }

            // 非玩家派系：按 120 tick 间隔扫描 20 格内是否有敌人；仅在护盾未展开时尝试展开，展开后维持直至充能耗尽，不再扫描也不自动关闭
            if (parent is Pawn pawn && pawn.Faction != Faction.OfPlayer && pawn.Spawned && pawn.IsHashIntervalTick(AIScanIntervalTicks))
            {
                if (!isShieldActive)
                {
                    bool hasEnemy = HasEnemyPawnInRange(EnemyScanRadiusCells);
                    if (hasEnemy && CanActivateShield())
                        ActivateShield();
                }
            }
        }

        /// <summary>该单位所属派系敌对的、20 格内的 Pawn 是否存在。</summary>
        private bool HasEnemyPawnInRange(int radiusCells)
        {
            if (parent?.Faction == null || !parent.Spawned || parent.Map == null)
                return false;
            IntVec3 center = parent.Position;
            Map map = parent.Map;
            int maxDistSq = radiusCells * radiusCells;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == parent || p.Dead || p.Downed || p.Faction == null)
                    continue;
                if (!parent.Faction.HostileTo(p.Faction))
                    continue;
                if ((p.Position - center).LengthHorizontalSquared > maxDistSq)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>原版 ThingComp 在父单位受伤时会被 ThingWithComps.PostApplyDamage 调用，此处不再根据受伤触发护盾展开。</summary>
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
        }

        private void PlayActivateSound()
        {
            if (Props?.activateSoundDef == null)
            {
                Log.Warning("[DMS_Legion] 护盾激活音效定义为空");
                return;
            }
            
            if (parent == null)
            {
                Log.Warning("[DMS_Legion] 父对象为空");
                return;
            }
            
            if (!parent.Spawned)
            {
                Log.Warning("[DMS_Legion] 父对象未生成");
                return;
            }
            
            try
            {
                Props.activateSoundDef.PlayOneShot(new TargetInfo(parent));
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DMS_Legion] 播放护盾激活音效失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        public void ActivateShield()
        {
            if (currentChargeSeconds >= Props?.minChargeToActivate && !isShieldActive)
            {
                isShieldActive = true;
                PlayActivateSound();
            }
            else
            {
                Log.Warning($"[DMS_Legion] 条件不满足 - 充能: {currentChargeSeconds}, 最小需求: {Props?.minChargeToActivate}, 已激活: {isShieldActive}");
            }
        }
        
        public void DeactivateShield()
        {
            isShieldActive = false;
        }
        
        public bool CanActivateShield()
        {
            return Props != null && currentChargeSeconds >= Props.minChargeToActivate;
        }
        
        public int GetHitPointsFromTime()
        {
            if (Props == null || maxChargeSeconds <= 0f) return 0;
            float percent = Mathf.Clamp01(currentChargeSeconds / maxChargeSeconds);
            return Mathf.RoundToInt(percent * Props.hitPointsMapping);
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isShieldActive, "isShieldActive", false);
            Scribe_Values.Look(ref currentChargeSeconds, "currentChargeSeconds", 30f);
            Scribe_Values.Look(ref maxChargeSeconds, "maxChargeSeconds", 30f);
        }
        
        public IEnumerable<Gizmo> GetGizmos()
        {
            if (Props == null || parent == null) yield break;
            // 非玩家派系不显示护盾 Gizmo（与原版 CompRefuelable.hideGizmosIfNotPlayerFaction 一致）
            if (parent.Faction != Faction.OfPlayer) yield break;
            
            if (shieldOnIcon == null || shieldOffIcon == null)
            {
                InitializeIfReady();
                if (shieldOnIcon == null || shieldOffIcon == null) yield break;
            }
            
            yield return new Gizmo_ShieldTimer(CurrentChargeSeconds, MaxChargeSeconds, IsShieldActive, "DMSL_ShieldTimer_Label".Translate());
            
            var action = new Command_Action();
            action.defaultLabel = IsShieldActive ? "DMSL_ShieldDeactivate_Label".Translate() : "DMSL_ShieldActivate_Label".Translate();
            action.icon = IsShieldActive ? shieldOnIcon?.Texture : shieldOffIcon?.Texture;
            action.action = () =>
            {
                if (Props == null || parent == null)
                {
                    Log.Warning("[DMS_Legion] Props或parent为空，无法执行操作");
                    return;
                }
                if (IsShieldActive)
                {
                    DeactivateShield();
                }
                else if (CanActivateShield())
                {
                    ActivateShield();
                }
                else
                {
                    Log.Warning("[DMS_Legion] 无法激活护盾 - 充能不足");
                }
            };
            
            if (!IsShieldActive && !CanActivateShield())
                action.Disable("DMSL_ShieldRecharging_Message_Simple".Translate());
            
            action.defaultDesc = IsShieldActive 
                ? "DMSL_ShieldActive_Desc".Translate() 
                : (CanActivateShield() ? "DMSL_ShieldInactive_Desc".Translate() : "DMSL_ShieldRecharging_Message_Simple".Translate());
            
            yield return action;
        }
    }
    
    public class CompProperties_TimedShieldController : CompProperties
    {
        public string shieldOnIconPath = "UI/Gizmo/ShieldOn";
        public string shieldOffIconPath = "UI/Gizmo/ShieldOff";
        public float maxDurationSeconds = 30f;
        public float rechargeRatePerSecond = 0.5f;
        public float minChargeToActivate = 5f;
        public int hitPointsMapping = 1000;
        public SoundDef? activateSoundDef; // 护盾展开音效
        
        public CompProperties_TimedShieldController()
        {
            compClass = typeof(CompTimedShieldController);
        }
    }
}

