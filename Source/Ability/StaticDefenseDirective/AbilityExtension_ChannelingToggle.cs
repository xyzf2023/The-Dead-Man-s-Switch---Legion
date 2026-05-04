using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 能力扩展：配置吟唱时长、可否被打断、完成后附加的Hediff。
    /// </summary>
    public class AbilityExtension_ChannelingToggle : DefModExtension
    {
        public int chantTicks = 600; // 默认10秒
        public bool canInterrupt = false;
        public HediffDef? hediffOnComplete;

        public static AbilityExtension_ChannelingToggle? Get(Def? def)
        {
            return def?.GetModExtension<AbilityExtension_ChannelingToggle>();
        }
    }
}

