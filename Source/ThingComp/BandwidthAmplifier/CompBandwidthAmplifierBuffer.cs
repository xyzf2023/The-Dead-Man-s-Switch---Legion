// ============================================================================
// 频段增幅装置充能缓冲：断电/耀斑后不立即失去带宽，按充能缓冲延迟最多 60000 tick 后移除。
// 充能与功率请求均每 60 tick 更新一次，减轻与电网每 tick 判断的耦合，缓解电力临界时的通断振荡。
// 充能：有电且非耀斑时 +120/秒（研究后 +240），否则 -60/秒；充满后耗电 400W（研究后 200W），充电时 600W（研究后 300W）。
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    public class CompBandwidthAmplifierBuffer : ThingComp
    {
        private const int UpdateInterval = 60;
        private const int ChargePerSecond = 120;
        private const int DrainPerSecond = 60;
        private const int MaxChargeBase = 60000;
        private const int PowerCharging = 600;
        private const int PowerFull = 400;

        private int _chargeTicks;

        /// <summary>当前缓冲充能（tick 数），大于 0 时建筑仍提供带宽。</summary>
        public int ChargeTicks => _chargeTicks;

        /// <summary>是否仍提供带宽（充能未耗尽）。</summary>
        public bool AllowBandwidth => _chargeTicks > 0;

        /// <summary>当前存档下的充能上限（tick），供建筑绘制充能条等使用。</summary>
        public int MaxChargeTicks => MaxCharge;

        private static bool HasClusterTransceiverResearch() =>
            DMSL_GameComponent_ClusterTransceiverResearchCache.GetOrCreate()?.ClusterTransceiverCompleted == true;

        private int MaxCharge => HasClusterTransceiverResearch() ? MaxChargeBase * 2 : MaxChargeBase;
        private int ChargeRatePerSecond => HasClusterTransceiverResearch() ? ChargePerSecond * 2 : ChargePerSecond;
        private int PowerChargingNow => HasClusterTransceiverResearch() ? PowerCharging / 2 : PowerCharging;
        private int PowerFullNow => HasClusterTransceiverResearch() ? PowerFull / 2 : PowerFull;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // 新建建筑初始为空充能，需通电充能后才提供带宽
        }

        public override void CompTick()
        {
            var powerTrader = parent.TryGetComp<CompPowerTrader>();
            if (powerTrader == null)
                return;

            if (!parent.IsHashIntervalTick(UpdateInterval))
                return;

            UpdateCharge();
            float desired = GetDesiredPowerConsumption();
            if (powerTrader.PowerOutput != desired)
                powerTrader.PowerOutput = desired;
        }

        private static bool IsSolarFlareActive(Map map)
        {
            var def = DefDatabase<GameConditionDef>.GetNamed("SolarFlare", false);
            return def != null && map != null && map.gameConditionManager.ConditionIsActive(def);
        }

        private void UpdateCharge()
        {
            bool powerOn = parent.TryGetComp<CompPowerTrader>()?.PowerOn ?? false;
            bool solarFlare = IsSolarFlareActive(parent.Map);
            int maxCharge = MaxCharge;
            int addPerUpdate = ChargeRatePerSecond;

            if (powerOn && !solarFlare)
                _chargeTicks = _chargeTicks + addPerUpdate > maxCharge ? maxCharge : _chargeTicks + addPerUpdate;
            else
            {
                _chargeTicks -= DrainPerSecond;
                if (_chargeTicks < 0)
                    _chargeTicks = 0;
            }
        }

        /// <summary>期望耗电量：充满时 PowerFullNow，充电中 PowerChargingNow，断电/耀斑时 0。</summary>
        private float GetDesiredPowerConsumption()
        {
            if (_chargeTicks >= MaxCharge)
                return -PowerFullNow;
            bool powerOn = parent.TryGetComp<CompPowerTrader>()?.PowerOn ?? false;
            bool solarFlare = IsSolarFlareActive(parent.Map);
            if (powerOn && !solarFlare)
                return -PowerChargingNow;
            return 0f;
        }

        /// <summary>选中建筑时在检查信息面板显示充能缓冲可用时长（类似蓄电池显示蓄电量）。</summary>
        public override string CompInspectStringExtra()
        {
            int secondsRemaining = _chargeTicks / DrainPerSecond;
            return "DMSL_BandwidthAmplifier_BufferDuration".Translate(secondsRemaining.ToString());
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _chargeTicks, "chargeTicks", 0);
        }
    }
}
