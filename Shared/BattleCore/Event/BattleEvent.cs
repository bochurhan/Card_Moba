using System;
using System.Collections.Generic;

namespace CardMoba.BattleCore.Event
{
    /// <summary>
    /// 战斗事件类型枚举。
    /// </summary>
    public enum BattleEventType
    {
        // ══════════════════════════════════════════════════════════
        // 回合相关
        // ══════════════════════════════════════════════════════════

        /// <summary>对局开始</summary>
        BattleStart = 1,

        /// <summary>对局结束</summary>
        BattleEnd = 2,

        /// <summary>回合开始</summary>
        RoundStart = 10,

        /// <summary>回合结束</summary>
        RoundEnd = 11,

        /// <summary>阶段变更</summary>
        PhaseChange = 12,

        // ══════════════════════════════════════════════════════════
        // 卡牌相关
        // ══════════════════════════════════════════════════════════

        /// <summary>打出瞬策牌</summary>
        PlayInstantCard = 101,

        /// <summary>提交定策牌</summary>
        CommitPlanCard = 102,

        /// <summary>取消定策牌</summary>
        CancelPlanCard = 103,

        /// <summary>定策牌结算</summary>
        ResolvePlanCard = 104,

        /// <summary>卡牌效果触发</summary>
        CardEffectTriggered = 105,

        /// <summary>卡牌被反制</summary>
        CardCountered = 106,

        /// <summary>卡牌成功打出（通用）</summary>
        CardPlayed = 107,

        /// <summary>抽牌</summary>
        DrawCard = 110,

        /// <summary>弃牌</summary>
        DiscardCard = 111,

        /// <summary>牌库洗牌</summary>
        ShuffleDeck = 112,

        // ══════════════════════════════════════════════════════════
        // 战斗相关
        // ══════════════════════════════════════════════════════════

        /// <summary>造成伤害</summary>
        DealDamage = 201,

        /// <summary>治疗</summary>
        Heal = 202,

        /// <summary>获得护盾</summary>
        GainShield = 203,

        /// <summary>护盾吸收伤害</summary>
        ShieldAbsorb = 204,

        /// <summary>护盾破裂</summary>
        ShieldBroken = 205,

        /// <summary>获得护甲</summary>
        GainArmor = 206,

        /// <summary>反伤触发</summary>
        ThornsDamage = 207,

        /// <summary>吸血触发</summary>
        LifestealHeal = 208,

        // ══════════════════════════════════════════════════════════
        // 状态相关
        // ══════════════════════════════════════════════════════════

        /// <summary>获得 Buff</summary>
        GainBuff = 301,

        /// <summary>失去 Buff</summary>
        LoseBuff = 302,

        /// <summary>Buff 层数变化</summary>
        BuffStackChange = 303,

        /// <summary>被眩晕</summary>
        Stunned = 310,

        /// <summary>被沉默</summary>
        Silenced = 311,

        /// <summary>濒死</summary>
        NearDeath = 320,

        /// <summary>死亡</summary>
        Death = 321,

        /// <summary>复活</summary>
        Resurrect = 322,

        // ══════════════════════════════════════════════════════════
        // 分路/团队相关
        // ══════════════════════════════════════════════════════════

        /// <summary>分路胜利</summary>
        LaneVictory = 401,

        /// <summary>分路结束</summary>
        LaneEnd = 402,

        /// <summary>换路</summary>
        LaneSwap = 403,

        /// <summary>支援</summary>
        Support = 404,

        /// <summary>中枢塔战斗开始</summary>
        CentralTowerStart = 410,

        /// <summary>中枢塔战斗结束</summary>
        CentralTowerEnd = 411,

        /// <summary>决战开始</summary>
        FinalBattleStart = 420,

        // ══════════════════════════════════════════════════════════
        // 操作相关
        // ══════════════════════════════════════════════════════════

        /// <summary>玩家锁定操作</summary>
        PlayerLock = 501,

        /// <summary>玩家投降</summary>
        Surrender = 502,

        /// <summary>玩家断线</summary>
        Disconnect = 503,

        /// <summary>玩家重连</summary>
        Reconnect = 504,
    }

    /// <summary>
    /// 战斗事件 —— 记录一次战斗中发生的事件。
    /// </summary>
    public class BattleEvent
    {
        /// <summary>事件唯一 ID</summary>
        public int EventId { get; set; }

        /// <summary>事件类型</summary>
        public BattleEventType EventType { get; set; }

        /// <summary>发生的回合数</summary>
        public int Round { get; set; }

        /// <summary>发生时的阶段</summary>
        public int Phase { get; set; }

        /// <summary>事件发生的时间戳（毫秒）</summary>
        public long Timestamp { get; set; }

        /// <summary>事件来源玩家 ID</summary>
        public string SourcePlayerId { get; set; }

        /// <summary>事件目标玩家 ID</summary>
        public string TargetPlayerId { get; set; }

        /// <summary>相关卡牌 ID</summary>
        public string CardId { get; set; }

        /// <summary>相关卡牌运行时 ID</summary>
        public string CardRuntimeId { get; set; }

        /// <summary>主要数值（如伤害值、治疗量）</summary>
        public int Value { get; set; }

        /// <summary>次要数值（如护盾吸收后剩余伤害）</summary>
        public int SecondaryValue { get; set; }

        /// <summary>是否为暴击</summary>
        public bool IsCritical { get; set; }

        /// <summary>相关 Buff ID</summary>
        public string BuffId { get; set; }

        /// <summary>分路索引</summary>
        public int LaneIndex { get; set; } = -1;

        /// <summary>事件描述（人类可读）</summary>
        public string Description { get; set; }

        /// <summary>额外数据（JSON 格式字符串）</summary>
        public string ExtraData { get; set; }

        public override string ToString()
        {
            return $"[R{Round}] {EventType}: {SourcePlayerId} -> {TargetPlayerId}, 值:{Value}";
        }
    }

    /// <summary>
    /// 战斗事件记录器 —— 记录战斗过程中所有事件，用于回放和日志。
    /// </summary>
    public class BattleEventRecorder
    {
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private int _eventIdCounter = 0;
        private long _battleStartTime;

        /// <summary>当前回合数</summary>
        public int CurrentRound { get; set; } = 0;

        /// <summary>当前阶段</summary>
        public int CurrentPhase { get; set; } = 0;

        /// <summary>所有事件（只读）</summary>
        public IReadOnlyList<BattleEvent> Events => _events;

        /// <summary>事件记录回调（用于实时通知）</summary>
        public event Action<BattleEvent> OnEventRecorded;

        public BattleEventRecorder()
        {
            _battleStartTime = GetCurrentTimestamp();
        }

        /// <summary>
        /// 记录一个事件。
        /// </summary>
        public BattleEvent Record(BattleEventType type, string sourcePlayerId = null, string targetPlayerId = null)
        {
            var evt = new BattleEvent
            {
                EventId = ++_eventIdCounter,
                EventType = type,
                Round = CurrentRound,
                Phase = CurrentPhase,
                Timestamp = GetCurrentTimestamp() - _battleStartTime,
                SourcePlayerId = sourcePlayerId,
                TargetPlayerId = targetPlayerId,
            };

            _events.Add(evt);
            OnEventRecorded?.Invoke(evt);

            return evt;
        }

        /// <summary>
        /// 记录伤害事件。
        /// </summary>
        public BattleEvent RecordDamage(string sourceId, string targetId, int damage, bool isCritical = false, string cardId = null)
        {
            var evt = Record(BattleEventType.DealDamage, sourceId, targetId);
            evt.Value = damage;
            evt.IsCritical = isCritical;
            evt.CardId = cardId;
            return evt;
        }

        /// <summary>
        /// 记录治疗事件。
        /// </summary>
        public BattleEvent RecordHeal(string sourceId, string targetId, int healAmount, string cardId = null)
        {
            var evt = Record(BattleEventType.Heal, sourceId, targetId);
            evt.Value = healAmount;
            evt.CardId = cardId;
            return evt;
        }

        /// <summary>
        /// 记录护盾事件。
        /// </summary>
        public BattleEvent RecordShield(string sourceId, string targetId, int shieldAmount, string cardId = null)
        {
            var evt = Record(BattleEventType.GainShield, sourceId, targetId);
            evt.Value = shieldAmount;
            evt.CardId = cardId;
            return evt;
        }

        /// <summary>
        /// 记录 Buff 获得事件。
        /// </summary>
        public BattleEvent RecordBuffGain(string sourceId, string targetId, string buffId, int stacks = 1, int duration = 0)
        {
            var evt = Record(BattleEventType.GainBuff, sourceId, targetId);
            evt.BuffId = buffId;
            evt.Value = stacks;
            evt.SecondaryValue = duration;
            return evt;
        }

        /// <summary>
        /// 记录卡牌打出事件。
        /// </summary>
        public BattleEvent RecordCardPlayed(string playerId, string cardId, string cardRuntimeId, bool isInstant)
        {
            var type = isInstant ? BattleEventType.PlayInstantCard : BattleEventType.CommitPlanCard;
            var evt = Record(type, playerId);
            evt.CardId = cardId;
            evt.CardRuntimeId = cardRuntimeId;
            return evt;
        }

        /// <summary>
        /// 记录死亡事件。
        /// </summary>
        public BattleEvent RecordDeath(string playerId, string killerId = null)
        {
            var evt = Record(BattleEventType.Death, killerId, playerId);
            return evt;
        }

        /// <summary>
        /// 记录回合开始。
        /// </summary>
        public void RecordRoundStart(int roundNumber)
        {
            CurrentRound = roundNumber;
            Record(BattleEventType.RoundStart);
        }

        /// <summary>
        /// 记录回合结束。
        /// </summary>
        public void RecordRoundEnd(int roundNumber)
        {
            Record(BattleEventType.RoundEnd);
        }

        /// <summary>
        /// 直接记录一个已构建的事件对象。
        /// </summary>
        public void RecordEvent(BattleEvent evt)
        {
            if (evt == null) return;

            // 分配 ID 和时间戳（如果没有设置）
            if (evt.EventId == 0)
                evt.EventId = ++_eventIdCounter;

            if (evt.Timestamp == 0)
                evt.Timestamp = GetCurrentTimestamp() - _battleStartTime;

            if (evt.Round == 0)
                evt.Round = CurrentRound;

            _events.Add(evt);
            OnEventRecorded?.Invoke(evt);
        }

        /// <summary>
        /// 获取指定回合的所有事件。
        /// </summary>
        public List<BattleEvent> GetEventsByRound(int roundNumber)
        {
            var result = new List<BattleEvent>();
            foreach (var evt in _events)
            {
                if (evt.Round == roundNumber)
                    result.Add(evt);
            }
            return result;
        }

        /// <summary>
        /// 获取指定类型的所有事件。
        /// </summary>
        public List<BattleEvent> GetEventsByType(BattleEventType type)
        {
            var result = new List<BattleEvent>();
            foreach (var evt in _events)
            {
                if (evt.EventType == type)
                    result.Add(evt);
            }
            return result;
        }

        /// <summary>
        /// 获取指定玩家的所有事件（作为来源）。
        /// </summary>
        public List<BattleEvent> GetEventsBySource(string playerId)
        {
            var result = new List<BattleEvent>();
            foreach (var evt in _events)
            {
                if (evt.SourcePlayerId == playerId)
                    result.Add(evt);
            }
            return result;
        }

        /// <summary>
        /// 计算玩家总造成伤害。
        /// </summary>
        public int GetTotalDamageDealt(string playerId)
        {
            int total = 0;
            foreach (var evt in _events)
            {
                if (evt.EventType == BattleEventType.DealDamage && evt.SourcePlayerId == playerId)
                    total += evt.Value;
            }
            return total;
        }

        /// <summary>
        /// 计算玩家总受到伤害。
        /// </summary>
        public int GetTotalDamageTaken(string playerId)
        {
            int total = 0;
            foreach (var evt in _events)
            {
                if (evt.EventType == BattleEventType.DealDamage && evt.TargetPlayerId == playerId)
                    total += evt.Value;
            }
            return total;
        }

        /// <summary>
        /// 计算玩家总治疗量。
        /// </summary>
        public int GetTotalHealing(string playerId)
        {
            int total = 0;
            foreach (var evt in _events)
            {
                if (evt.EventType == BattleEventType.Heal && evt.TargetPlayerId == playerId)
                    total += evt.Value;
            }
            return total;
        }

        /// <summary>
        /// 获取玩家击杀数。
        /// </summary>
        public int GetKillCount(string playerId)
        {
            int count = 0;
            foreach (var evt in _events)
            {
                if (evt.EventType == BattleEventType.Death && evt.SourcePlayerId == playerId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 清除所有事件。
        /// </summary>
        public void Clear()
        {
            _events.Clear();
            _eventIdCounter = 0;
            CurrentRound = 0;
            CurrentPhase = 0;
            _battleStartTime = GetCurrentTimestamp();
        }

        private static long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
