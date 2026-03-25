#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.EventBus;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 战斗上下文（BattleContext）—— 一局战斗的唯一状态容器。
    ///
    /// 所有结算逻辑通过 ctx 读写状态，禁止在 Context 之外持有战斗状态。
    /// 管理器引用（TriggerManager / BuffManager 等）均挂载于此，通过接口访问。
    /// </summary>
    public class BattleContext
    {
        // ══════════════════════════════════════════════════════════
        // 基础信息
        // ══════════════════════════════════════════════════════════

        /// <summary>战斗唯一 ID</summary>
        public string BattleId { get; }

        /// <summary>当前回合数（从 1 开始）</summary>
        public int CurrentRound { get; set; }

        /// <summary>当前回合阶段</summary>
        public BattlePhase CurrentPhase { get; set; }

        // ══════════════════════════════════════════════════════════
        // 玩家数据（O(1) 查找）
        // ══════════════════════════════════════════════════════════

        private readonly Dictionary<string, PlayerData> _players = new Dictionary<string, PlayerData>();

        /// <summary>注册玩家数据（战斗初始化时调用）</summary>
        public void RegisterPlayer(PlayerData player)
        {
            _players[player.PlayerId] = player;
        }

        /// <summary>
        /// 获取玩家数据（O(1) 字典查找）。
        /// 首选此方法，禁止直接遍历 AllPlayers 查找。
        /// </summary>
        public PlayerData? GetPlayer(string playerId)
        {
            _players.TryGetValue(playerId, out var player);
            return player;
        }

        public PlayerData? GetPlayerByEntityId(string entityId)
        {
            foreach (var player in _players.Values)
            {
                if (player.HeroEntity?.EntityId == entityId)
                    return player;
            }
            return null;
        }

        /// <summary>所有玩家数据（只读视图，遍历用）</summary>
        public IReadOnlyDictionary<string, PlayerData> AllPlayers => _players;

        /// <summary>
        /// 通过实体 EntityId 查找英雄实体（遍历所有玩家 HeroEntity）。
        /// 用于 Buff 触发器等需要直接访问 Entity 的场景。
        /// </summary>
        public Entity? GetEntity(string entityId)
        {
            foreach (var kv in _players)
            {
                if (kv.Value.HeroEntity?.EntityId == entityId)
                    return kv.Value.HeroEntity;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════
        // 分路数据
        // ══════════════════════════════════════════════════════════

        /// <summary>所有分路数据（key = laneId）</summary>
        public Dictionary<string, LaneData> Lanes { get; } = new Dictionary<string, LaneData>();

        // ══════════════════════════════════════════════════════════
        // 核心调度
        // ══════════════════════════════════════════════════════════

        /// <summary>延迟效果队列（触发器产生的子效果推入此队列，不直接调用结算引擎）</summary>
        public PendingEffectQueue PendingQueue { get; } = new PendingEffectQueue();

        /// <summary>确定性随机（C/S 共用同一种子，保证结果一致性）</summary>
        public Random.SeededRandom Random { get; }

        // ══════════════════════════════════════════════════════════
        // 管理器（由外部注入，通过接口访问）
        // ══════════════════════════════════════════════════════════

        /// <summary>外部事件总线（广播给 UI/StatManager/LogManager，订阅者只读）</summary>
        public IEventBus EventBus { get; }

        /// <summary>触发器管理器（内部逻辑总线）</summary>
        public Managers.ITriggerManager TriggerManager { get; }

        /// <summary>卡牌区域管理器</summary>
        public Managers.ICardManager CardManager { get; }

        /// <summary>Buff 管理器（薄中介，CRUD + 触发器注册）</summary>
        public Managers.IBuffManager BuffManager { get; }

        /// <summary>数值修正器管理器（力量/虚弱等动态修正）</summary>
        public Managers.IValueModifierManager ValueModifierManager { get; }

        /// <summary>
        /// 卡牌运行时定义提供器。BattleCore 只依赖效果列表和少量生命周期标记。
        /// </summary>
        public System.Func<string, BattleCardDefinition?>? CardDefinitionProvider { get; }

        // ══════════════════════════════════════════════════════════
        // 日志
        // ══════════════════════════════════════════════════════════

        /// <summary>当前回合日志（每回合开始时清空）</summary>
        public List<string> RoundLog { get; } = new List<string>();

        /// <summary>历史回合日志快照（永不清空，按回合存档）</summary>
        public List<List<string>> HistoryLog { get; } = new List<List<string>>();

        public BattleCardDefinition? GetCardDefinition(string configId)
        {
            return CardDefinitionProvider?.Invoke(configId);
        }

        public List<EffectUnit>? BuildCardEffects(string configId)
        {
            var definition = GetCardDefinition(configId);
            if (definition == null)
                return null;

            return EffectUnitCloner.CloneMany(definition.Effects);
        }

        /// <summary>归档当前回合日志到历史记录，并清空当前回合日志</summary>
        public void ArchiveRoundLog()
        {
            HistoryLog.Add(new List<string>(RoundLog));
            RoundLog.Clear();
        }

        /// <summary>战斗阶段枚举</summary>
        public enum BattlePhase
        {
            /// <summary>未开始</summary>
            NotStarted = 0,
            /// <summary>回合开始阶段</summary>
            RoundStart = 1,
            /// <summary>玩家操作阶段（可打牌）</summary>
            PlayerAction = 2,
            /// <summary>定策结算阶段</summary>
            Settlement = 3,
            /// <summary>回合结束阶段（Buff 衰减）</summary>
            RoundEnd = 4,
            /// <summary>战斗结束</summary>
            BattleEnd = 5,
        }

        // ══════════════════════════════════════════════════════════
        // 构造函数
        // ══════════════════════════════════════════════════════════

        public BattleContext(
            string battleId,
            int randomSeed,
            IEventBus eventBus,
            Managers.ITriggerManager triggerManager,
            Managers.ICardManager cardManager,
            Managers.IBuffManager buffManager,
            Managers.IValueModifierManager valueModifierManager,
            System.Func<string, BattleCardDefinition?>? cardDefinitionProvider = null)
        {
            BattleId              = battleId;
            Random                = new Random.SeededRandom(randomSeed);
            EventBus              = eventBus;
            TriggerManager        = triggerManager;
            CardManager           = cardManager;
            BuffManager           = buffManager;
            ValueModifierManager  = valueModifierManager;
            CardDefinitionProvider = cardDefinitionProvider;
        }
    }
}
