using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 空中支援类型定义
    /// </summary>
    public class AerialSupportTypeDef : Def
    {
        /// <summary>
        /// 图标路径
        /// </summary>
        public string? iconPath = null;

        /// <summary>
        /// 飞机贴图路径
        /// </summary>
        public string aircraftTexturePath = "Things/AerialSupport/Aircraft";

        /// <summary>
        /// 飞机出现音效
        /// </summary>
        public SoundDef? appearSoundDef;

        /// <summary>
        /// 飞机出现音效（已弃用，使用appearSoundDef）
        /// </summary>
        [Obsolete("使用appearSoundDef代替")]
        public string? appearSound = null;

        /// <summary>
        /// 飞行速度（每tick前进的距离比例）
        /// </summary>
        public float flightSpeed = 0.05f;


        /// <summary>
        /// 使用此支援类型后的冷却时间（tick数）
        /// </summary>
        public int cooldownTicks = 0;

        /// <summary>
        /// 飞机贴图的绘制大小
        /// </summary>
        public float drawSize = 2f;


        /// <summary>
        /// 飞机到达目标时执行的效果组件列表
        /// </summary>
        public List<CompProperties> effectComps = new List<CompProperties>();

        /// <summary>
        /// 是否在飞机到达目的地后继续绘制（如果为false，则在贴图与目标地点重合时停止绘制）
        /// </summary>
        public bool continueDrawingAfterArrival = true;

        /// <summary>
        /// 飞行路径类型（选点方式）
        /// - "Normal": 从地图边缘随机选择一个点到目标点的直线飞行（默认）
        /// - "CustomLine": 玩家选择两个点，飞机沿这两个点确定的直线飞行
        /// - "MultiTarget": 玩家选择多个目标点，依次打击每个目标
        /// </summary>
        public string flightPathType = "Normal";

        /// <summary>
        /// 多目标支援的选点数量（仅当flightPathType为"MultiTarget"时有效）
        /// 玩家需要选择的目標点数量，必须 ≥ 1
        /// </summary>
        public int selectionPointCount = 1;

        /// <summary>
        /// 多目标支援的打击间隔（仅当flightPathType为"MultiTarget"时有效）
        /// 每个目标点之间的执行间隔Ticks数，基于Find.TickManager.TicksGame
        /// </summary>
        public int selectionIntervalFrames = 60;

        /// <summary>
        /// 支援起始方向（仅当flightPathType为"Normal"或"MultiTarget"时有效）
        /// 指定飞机从哪个地图边缘开始绘制，可选值：
        /// - "Random" 或 空值：随机选择边缘（默认）
        /// - "North", "South", "East", "West"：指定具体方向
        /// </summary>
        public string? startDirection;

        /// <summary>
        /// 东/西进入时：为 true 表示起点 z 优先在目标点北侧（目标点 z 以上），从目标北侧进入。
        /// 南/北进入时：为 true 表示起点 x 与选点 x 相同，呈垂直路线（飞机正对选点飞入）。
        /// 仅当 startDirection 为 North/South/East/West 且 flightPathType 为 Normal 或 MultiTarget 时有效。
        /// </summary>
        public bool preferNorthEntry = false;

        /// <summary>
        /// 绘制延迟：从实例创建到开始绘制的帧数（ticks）
        /// 默认值为0，表示立即开始绘制
        /// 推荐范围：0 ~ 360000 ticks（0 ~ 100分钟）
        /// </summary>
        public int renderDelayTicks = 0;

        /// <summary>
        /// 声音延迟：从实例创建到播放声音的帧数（ticks）
        /// 默认值为0，表示立即播放声音
        /// 推荐范围：0 ~ 360000 ticks（0 ~ 100分钟）
        /// </summary>
        public int soundDelayTicks = 0;

        /// <summary>
        /// 飞机出现时的状态影响效果列表（Spawn Effects）
        /// 仅用于状态影响类副作用，如EMP、电子干扰、通讯抑制等
        /// 不包含表现属性（音效、贴图、动画）
        /// </summary>
        public List<SpawnEffectDef> spawnEffects = new List<SpawnEffectDef>();

        #region 尾气系统配置

        /// <summary>
        /// 是否启用尾气效果（总开关）
        /// 当为false时，完全不执行任何尾气相关逻辑
        /// </summary>
        public bool enableExhaust = false;

        /// <summary>
        /// 尾气生成率（每tick生成概率，0-1之间）
        /// 0表示不生成，1表示每tick都生成
        /// </summary>
        public float exhaustSpawnRate = 0f;

        /// <summary>
        /// 尾气粒子数（平均每tick生成的尾气Fleck数量）
        /// 允许非整数（如0.5、2.5），使用概率补偿模型实现
        /// <=0时不生成任何尾气
        /// </summary>
        public float exhaustParticlesPerTick = 0f;

        /// <summary>
        /// 尾气基础缩放系数（影响视觉大小）
        /// </summary>
        public float exhaustBaseScale = 1.0f;

        /// <summary>
        /// 尾气最小速度（扩散速度下限）
        /// </summary>
        public float exhaustMinSpeed = 0.4f;

        /// <summary>
        /// 尾气最大速度（扩散速度上限）
        /// </summary>
        public float exhaustMaxSpeed = 0.6f;

        /// <summary>
        /// 尾气角度扰动范围（±度数）
        /// </summary>
        public float exhaustAngleVariance = 15f;

        /// <summary>
        /// 尾气旋转范围（±度/秒）
        /// </summary>
        public float exhaustRotationRange = 30f;

        #endregion

        /// <summary>
        /// 获取图标材质
        /// </summary>
        public UnityEngine.Material IconMat
        {
            get
            {
                if (iconPath != null)
                {
                    return MaterialPool.MatFrom(iconPath);
                }
                return BaseContent.BadMat;
            }
        }

        /// <summary>
        /// 获取飞机材质
        /// </summary>
        public UnityEngine.Material AircraftMat
        {
            get
            {
                if (aircraftTexturePath != null)
                {
                    return MaterialPool.MatFrom(aircraftTexturePath, ShaderDatabase.MetaOverlay);
                }
                return MaterialPool.MatFrom("Things/AerialSupport/Aircraft", ShaderDatabase.MetaOverlay);
            }
        }
    }
}
