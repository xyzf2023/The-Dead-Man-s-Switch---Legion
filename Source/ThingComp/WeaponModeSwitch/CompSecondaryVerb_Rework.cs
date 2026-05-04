using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 武器模式切换组件
    /// 允许武器在主模式和次模式之间切换，通过替换verbProps实现
    /// </summary>
    public class CompSecondaryVerb_Rework : ThingComp
    {
        private Verb? verbInt = null;
        private CompEquippable? compEquippableInt;
        private bool isSecondaryVerbSelected;

        /// <summary>
        /// 获取组件属性（类型安全的访问）
        /// </summary>
        public CompProperties_SecondaryVerb_Rework Props => 
            (CompProperties_SecondaryVerb_Rework)props;

        /// <summary>
        /// 当前是否选择了次要模式
        /// </summary>
        public bool IsSecondaryVerbSelected => isSecondaryVerbSelected;

        /// <summary>
        /// 获取武器的CompEquippable组件
        /// </summary>
        private CompEquippable EquipmentSource
        {
            get
            {
                if (compEquippableInt != null)
                {
                    return compEquippableInt;
                }

                compEquippableInt = parent.GetComp<CompEquippable>();
                if (compEquippableInt == null)
                {
                    Log.ErrorOnce(parent.LabelCap + " has CompSecondaryVerb_Rework but no CompEquippable", 50020);
                }
                return compEquippableInt!;
            }
        }

        /// <summary>
        /// 获取当前装备武器的Pawn
        /// </summary>
        public Pawn? CasterPawn
        {
            get
            {
                Thing? caster = Verb?.caster;
                return caster as Pawn;
            }
        }

        /// <summary>
        /// 获取武器的PrimaryVerb
        /// </summary>
        private Verb? Verb
        {
            get
            {
                if (verbInt == null)
                {
                    verbInt = EquipmentSource?.PrimaryVerb;
                }
                return verbInt;
            }
        }

        /// <summary>
        /// 生成模式切换按钮的Gizmo
        /// 只在玩家阵营时显示
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (CasterPawn == null || CasterPawn.Faction == Faction.OfPlayer)
            {
                string commandIcon = IsSecondaryVerbSelected 
                    ? Props.secondaryCommandIcon 
                    : Props.mainCommandIcon;

                if (commandIcon == "")
                {
                    commandIcon = "UI/Buttons/Reload";
                }

                yield return new Command_Action
                {
                    action = SwitchVerb,
                    defaultLabel = IsSecondaryVerbSelected 
                        ? Props.secondaryWeaponLabel 
                        : Props.mainWeaponLabel,
                    defaultDesc = Props.description,
                    icon = ContentFinder<Texture2D>.Get(commandIcon, false)
                };
            }
        }

        /// <summary>
        /// 保存/加载当前模式状态
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isSecondaryVerbSelected, "DMSL_useSecondaryVerb", false);
            
            // 加载后恢复模式状态
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PostAmmoDataLoaded();
            }
        }

        /// <summary>
        /// 切换攻击模式（公共方法）
        /// </summary>
        public void SwitchMode()
        {
            SwitchVerb();
        }

        /// <summary>
        /// 切换攻击模式
        /// 通过替换PrimaryVerb.verbProps来实现模式切换
        /// </summary>
        private void SwitchVerb()
        {
            if (EquipmentSource?.PrimaryVerb == null)
            {
                return;
            }

            if (!IsSecondaryVerbSelected)
            {
                // 切换到次要模式：使用Props.verbProps
                EquipmentSource.PrimaryVerb.verbProps = Props.verbProps;
                isSecondaryVerbSelected = true;
            }
            else
            {
                // 切换回主模式：使用武器定义中的默认verbs[0]
                EquipmentSource.PrimaryVerb.verbProps = parent.def.Verbs[0];
                isSecondaryVerbSelected = false;
            }

            // 清除Verb的缓存字段，确保新参数生效
            if (Verb != null)
            {
                typeof(Verb).GetField("cachedTicksBetweenBurstShots", 
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(Verb, null);
                typeof(Verb).GetField("cachedBurstShotCount", 
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(Verb, null);
            }
        }

        /// <summary>
        /// 加载后恢复模式状态
        /// 如果之前选择了次模式，需要重新应用
        /// </summary>
        private void PostAmmoDataLoaded()
        {
            if (isSecondaryVerbSelected && EquipmentSource?.PrimaryVerb != null)
            {
                EquipmentSource.PrimaryVerb.verbProps = Props.verbProps;
            }
        }
    }
}
