using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 游戏级组件：在加载/新建存档时，按设置自动为缺少电子天使派系的存档补生成隐藏派系。
    /// 每个存档仅处理一次，处理结果通过组件字段记录。
    /// </summary>
    public class GameComponent_DigitalAngelFactionInitializer : GameComponent
    {
        private const string DigitalAngelFactionDefName = "DMSL_Faction_DigitalAngel";

        /// <summary>
        /// 本存档是否已经完成过“电子天使派系检测与补添加”流程。
        /// 一旦为 true，后续加载该存档将不再重复扫描派系列表。
        /// </summary>
        private bool digitalAngelFactionProcessed;

        public GameComponent_DigitalAngelFactionInitializer(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            TryEnsureDigitalAngelFaction();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            TryEnsureDigitalAngelFaction();
        }

        /// <summary>
        /// 检查当前世界是否已经存在电子天使派系；若不存在且设置允许，则创建一个隐藏派系并加入当前世界。
        /// 整个流程在本存档生命周期内只执行一次，执行结果通过字段持久化。
        /// </summary>
        private void TryEnsureDigitalAngelFaction()
        {
            // 若本存档已经处理过，则不再进行任何检查
            if (digitalAngelFactionProcessed)
            {
                return;
            }

            // 读取 MOD 设置：关闭选项时不执行检测流程
            var settings = DMSL_ModSettings.settings;
            if (settings == null || !settings.autoAddDigitalAngelFaction)
            {
                return;
            }

            // 若当前世界已经存在电子天使派系，则仅标记为已处理
            Faction? existing = FindExistingDigitalAngelFaction();
            if (existing != null)
            {
                digitalAngelFactionProcessed = true;
                return;
            }

            // 尝试通过 DefDatabase 获取对应的 FactionDef
            FactionDef? def = DefDatabase<FactionDef>.GetNamedSilentFail(DigitalAngelFactionDefName);
            if (def == null)
            {
                Log.Warning($"[DMS_Legion] Failed to auto-add Digital Angel faction: cannot find FactionDef '{DigitalAngelFactionDefName}'.");
                // 不标记为已处理，允许在后续加载中（例如补全缺失 Def 后）再次尝试
                return;
            }

            // 使用原版 FactionGenerator 生成一个隐藏派系实例
            var parms = new FactionGeneratorParms(def, default(IdeoGenerationParms), hidden: true);
            Faction faction = FactionGenerator.NewGeneratedFaction(parms);

            // 再次确保派系处于隐藏状态
            faction.hidden = true;

            // 对齐电子天使与世界其他派系的关系（不发送信件，避免刷屏）
            try
            {
                DigitalAngelRelationAligner.AlignRelations(faction, sendLetters: false);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[DMS_Legion] Error aligning Digital Angel faction relations: {e}");
            }

            // 正式将派系加入当前世界
            Find.FactionManager.Add(faction);

            // 标记本存档已完成电子天使派系处理，后续加载不再重复扫描
            digitalAngelFactionProcessed = true;
        }

        /// <summary> 在当前世界中查找电子天使派系（基于 FactionDef.defName）。 </summary>
        private static Faction? FindExistingDigitalAngelFaction()
        {
            var all = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f != null && f.def != null && f.def.defName == DigitalAngelFactionDefName)
                {
                    return f;
                }
            }
            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref digitalAngelFactionProcessed, "digitalAngelFactionProcessed", false);
        }
    }
}

