/*
 * ===========================================
 *          飞机出现时效果系统 (Spawn Effects)
 * ===========================================
 *
 * 设计目标：
 * 1. 实现飞机出现时的状态影响类副作用
 * 2. 严格区分状态影响和表现属性
 * 3. Renderer只负责触发，不解析具体逻辑
 * 4. 支持XML配置和EffectWorker模式
 *
 * 适用范围（状态影响类）：
 * ✅ EMP脉冲、电子干扰、通讯抑制
 * ✅ 环境改变、力场生成、能量扰动
 * ✅ 状态修改、能力削弱、区域控制
 *
 * 明确排除（表现属性）：
 * ❌ 音效播放、UI提示、贴图展示
 * ❌ 动画效果、粒子发射、视觉反馈
 * ❌ 任何纯显示相关的逻辑
 *
 * 触发时机：
 * - Flight实例创建后
 * - 首次Tick执行前
 * - 在Renderer.StartFlight()中触发
 *
 * 架构约束：
 * - 未定义spawnEffects的支援类型完全不受影响
 * - 具体效果由SpawnEffectWorker实现
 * - 支持复杂的自定义参数和逻辑
 */

using System;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 飞机出现时状态影响效果定义
    /// 仅用于状态影响类副作用，不包含表现属性
    /// </summary>
    public class SpawnEffectDef : Def
    {
        /// <summary>
        /// 效果执行器类型
        /// 必须继承自SpawnEffectWorker
        /// </summary>
        public Type? workerClass;

        /// <summary>
        /// 效果参数
        /// </summary>
        public SpawnEffectProperties properties = new SpawnEffectProperties();

        /// <summary>
        /// 创建效果执行器实例
        /// </summary>
        public SpawnEffectWorker? CreateWorker()
        {
            if (workerClass == null)
            {
                Log.Error($"[DMS_Legion] {defName} 的workerClass未设置");
                return null;
            }

            try
            {
                var worker = (SpawnEffectWorker)Activator.CreateInstance(workerClass);
                worker.def = this;
                worker.properties = properties;
                return worker;
            }
            catch (Exception ex)
            {
                Log.Error($"[DMS_Legion] 创建worker失败: {defName} - {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 飞机出现时状态影响效果属性
    /// </summary>
    public class SpawnEffectProperties
    {
        /// <summary>
        /// 效果半径（如果适用）
        /// </summary>
        public float radius = 0f;

        /// <summary>
        /// 效果持续时间（Ticks，如果适用）
        /// </summary>
        public int durationTicks = 0;

        /// <summary>
        /// 效果强度（如果适用）
        /// </summary>
        public float intensity = 1f;

        /// <summary>
        /// 自定义参数字符串（JSON格式，可选）
        /// </summary>
        public string customParams = "";

        /// <summary>
        /// 允许播放的音效 defName（用于“静音除指定音效外”类效果，如 MuteExceptTinnitus）
        /// </summary>
        public string allowedSoundDefName = "";
    }

    /// <summary>
    /// 飞机出现时状态影响效果执行器基类
    /// 仅负责状态影响类副作用，不处理表现属性
    /// </summary>
    public abstract class SpawnEffectWorker
    {
        public SpawnEffectDef? def;
        public SpawnEffectProperties? properties;

        /// <summary>
        /// 执行状态影响效果
        /// </summary>
        /// <param name="spawnPos">飞机出现位置</param>
        /// <param name="supportType">支援类型定义</param>
        /// <param name="map">当前地图</param>
        public abstract void ExecuteEffect(IntVec3 spawnPos, AerialSupportTypeDef supportType, Map map);
    }
}