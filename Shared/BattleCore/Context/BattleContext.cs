using System.Collections.Generic;
using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Event;
using CardMoba.BattleCore.Random;
using CardMoba.BattleCore.Trigger;

#pragma warning disable CS8632 // nullable 注解警告

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 战斗上下文 —— 一整局对战的所有可变状态。
    /// 这是结算引擎的"世界"，所有结算逻辑都读写这个对象。
    /// 
    /// 设计原则：
    /// - 前后端共用同一个 BattleContext，保证结算结果一致
    /// - 支持 3v3 分路对战模式（三条分路 + 中枢塔 + 决战期）
    /// - 符合《定策牌结算机制 V4.0》，支持跨回合反制牌、触发式效果等
    /// </summary>
    public class BattleContext
    {
        // ══════════════════════════════════════════════════════════
        // 对局基础信息
        // ══════════════════════════════════════════════════════════

        /// <summary>当前回合数（从1开始）</summary>
        public int CurrentRound { get; set; } = 1;

        /// <summary>最大回合数（达到后强制结束）</summary>
        public int MaxRounds { get; set; } = 25;

        /// <summary>对局是否已结束</summary>
        public bool IsGameOver { get; set; }

        /// <summary>获胜队伍ID（-1 表示平局或未结束）</summary>
        public int WinnerTeamId { get; set; } = -1;

        /// <summary>当前比赛阶段（分路期/中枢塔/决战期）</summary>
        public Protocol.Enums.MatchPhase MatchPhase { get; set; } = Protocol.Enums.MatchPhase.LanePhase1;

        // ══════════════════════════════════════════════════════════
        // 确定性随机数生成器
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 确定性随机数生成器 —— 保证客户端和服务端结算结果一致。
        /// 使用相同种子初始化后，调用顺序相同则结果相同。
        /// </summary>
        public SeededRandom Random { get; set; } = new SeededRandom(0);

        // ══════════════════════════════════════════════════════════
        // 3v3 分路结构
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 三条分路状态（0=上路, 1=中路, 2=下路）
        /// 分路期每条路独立结算，中枢塔/决战期合并结算
        /// </summary>
        public LaneState[] Lanes { get; set; } = new LaneState[3]
        {
            new LaneState { LaneIndex = 0 },
            new LaneState { LaneIndex = 1 },
            new LaneState { LaneIndex = 2 }
        };

        /// <summary>
        /// 所有参战玩家的状态（3v3 = 6个玩家）
        /// </summary>
        public List<PlayerBattleState> Players { get; set; } = new List<PlayerBattleState>();

        // ══════════════════════════════════════════════════════════
        // 本回合操作记录
        // ══════════════════════════════════════════════════════════

        /// <summary>本回合所有已提交的定策牌（回合末统一结算）</summary>
        public List<PlayedCard> PendingPlanCards { get; set; } = new List<PlayedCard>();

        /// <summary>本回合出牌计数器（用于生成 PlayedCard.RuntimeId）</summary>
        public int CardPlayedCountThisRound { get; set; }

        /// <summary>本回合已结算的所有操作日志（用于回放和UI展示）</summary>
        public List<string> RoundLog { get; set; } = new List<string>();

        // ── 跨回合数据（反制牌系统） ──

        /// <summary>
        /// 上回合提交的反制牌（本回合堆叠0层触发校验）
        /// 根据文档：反制牌本回合提交锁定，下回合堆叠0层触发
        /// </summary>
        public List<PlayedCard> PendingCounterCards { get; set; } = new List<PlayedCard>();

        /// <summary>
        /// 本回合被反制作废的卡牌（用于动画展示和日志）
        /// </summary>
        public List<PlayedCard> CounteredCards { get; set; } = new List<PlayedCard>();

        /// <summary>
        /// 本回合有效的定策牌（未被反制、未被作废）
        /// 在堆叠0层结算完成后填充，供后续堆叠层使用
        /// </summary>
        public List<PlayedCard> ValidPlanCards { get; set; } = new List<PlayedCard>();

        // ── 触发式效果追踪 ──

        /// <summary>
        /// 本回合待触发的效果列表（堆叠2层步骤2处理）
        /// </summary>
        public List<PendingTriggerEffect> PendingTriggerEffects { get; set; } = new List<PendingTriggerEffect>();

        /// <summary>
        /// 本回合是否已触发连锁（连锁封顶规则：单回合仅1次连锁）
        /// </summary>
        public bool HasChainTriggeredThisRound { get; set; }

        // ══════════════════════════════════════════════════════════
        // 系统管理器
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 触发器管理器 —— 管理所有跨回合触发效果。
        /// </summary>
        public TriggerManager TriggerManager { get; set; } = new TriggerManager();

        /// <summary>
        /// 战斗事件记录器 —— 记录所有战斗事件，用于回放和日志。
        /// </summary>
        public BattleEventRecorder EventRecorder { get; set; } = new BattleEventRecorder();

        /// <summary>
        /// 玩家 Buff 管理器字典（PlayerId -> BuffManager）
        /// </summary>
        private Dictionary<string, BuffManager> _buffManagers = new Dictionary<string, BuffManager>();

        // ══════════════════════════════════════════════════════════
        // 便捷方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 根据玩家ID获取玩家状态（字符串版本）。
        /// </summary>
        /// <param name="playerId">目标玩家ID（字符串）</param>
        /// <returns>玩家状态，找不到则返回 null</returns>
        public PlayerBattleState? GetPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == playerId)
                    return Players[i];
            }
            return null;
        }

        /// <summary>
        /// 获取玩家所在的分路。
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>分路状态，找不到则返回 null</returns>
        public LaneState? GetPlayerLane(string playerId)
        {
            foreach (var lane in Lanes)
            {
                if (lane.HasPlayer(playerId))
                    return lane;
            }
            return null;
        }

        /// <summary>
        /// 生成下一张打出卡牌的运行时ID。
        /// 格式："{回合数}_{玩家ID}_{序号}"
        /// </summary>
        public string GenerateCardRuntimeId(string playerId)
        {
            CardPlayedCountThisRound++;
            return $"{CurrentRound}_{playerId}_{CardPlayedCountThisRound}";
        }

        /// <summary>
        /// 获取指定队伍的所有玩家。
        /// </summary>
        public List<PlayerBattleState> GetTeamPlayers(int teamId)
        {
            List<PlayerBattleState> result = new List<PlayerBattleState>();
            foreach (var player in Players)
            {
                if (player.TeamId == teamId)
                    result.Add(player);
            }
            return result;
        }

        /// <summary>
        /// 获取敌方队伍的所有玩家。
        /// </summary>
        public List<PlayerBattleState> GetEnemyTeamPlayers(int myTeamId)
        {
            List<PlayerBattleState> result = new List<PlayerBattleState>();
            foreach (var player in Players)
            {
                if (player.TeamId != myTeamId)
                    result.Add(player);
            }
            return result;
        }

        /// <summary>
        /// 清空本回合的临时数据，准备进入下一回合。
        /// </summary>
        public void ClearRoundData()
        {
            // 将本回合的反制牌转移到跨回合存储
            PendingCounterCards.Clear();
            foreach (var card in PendingPlanCards)
            {
                // 使用 Flags 检查：只要包含"反制"标签就转移
                if (card.Config.HasTag(Protocol.Enums.CardTag.Counter))
                {
                    PendingCounterCards.Add(card);
                }
            }

            // 清空本回合数据
            PendingPlanCards.Clear();
            ValidPlanCards.Clear();
            CounteredCards.Clear();
            PendingTriggerEffects.Clear();
            RoundLog.Clear();
            HasChainTriggeredThisRound = false;

            // 重置所有玩家的回合状态
            for (int i = 0; i < Players.Count; i++)
            {
                Players[i].IsLocked = false;
                Players[i].ResetRoundStats();
            }
        }

        /// <summary>
        /// 回合开始时调用，处理跨回合效果和玩家状态更新。
        /// </summary>
        public void OnRoundStart()
        {
            EventRecorder.RecordRoundStart(CurrentRound);

            // 触发回合开始触发器
            TriggerManager.FireTriggers(this, TriggerTiming.OnRoundStart);

            foreach (var player in Players)
            {
                player.OnRoundStart();

                // 触发玩家 Buff 的回合开始效果
                var buffManager = GetBuffManager(player.PlayerId);
                buffManager?.OnRoundStart(this);
            }
        }

        /// <summary>
        /// 回合结束时调用，处理触发器衰减和 Buff 持续时间。
        /// </summary>
        public void OnRoundEnd()
        {
            // 触发回合结束触发器
            TriggerManager.FireTriggers(this, TriggerTiming.OnRoundEnd);

            foreach (var player in Players)
            {
                // 触发玩家 Buff 的回合结束效果
                var buffManager = GetBuffManager(player.PlayerId);
                buffManager?.OnRoundEnd(this);
            }

            // 处理触发器回合衰减
            TriggerManager.OnRoundEnd();

            EventRecorder.RecordRoundEnd(CurrentRound);
        }

        /// <summary>
        /// 获取玩家的 Buff 管理器。
        /// </summary>
        public BuffManager GetBuffManager(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;

            if (_buffManagers.TryGetValue(playerId, out var manager))
                return manager;

            // 自动创建
            var player = GetPlayer(playerId);
            if (player != null)
            {
                manager = new BuffManager(player);
                _buffManagers[playerId] = manager;
                return manager;
            }

            return null;
        }

        /// <summary>
        /// 根据玩家ID获取玩家状态（别名方法，兼容 SettlementEngine）。
        /// </summary>
        public PlayerBattleState GetPlayerState(string playerId)
        {
            return GetPlayer(playerId);
        }

        /// <summary>
        /// 初始化战斗上下文（在对局开始时调用）。
        /// </summary>
        /// <param name="seed">随机种子</param>
        public void Initialize(int seed)
        {
            Random = new SeededRandom(seed);
            CurrentRound = 1;
            IsGameOver = false;
            WinnerTeamId = -1;
            CardPlayedCountThisRound = 0;

            // 初始化所有玩家的 BuffManager
            _buffManagers.Clear();
            foreach (var player in Players)
            {
                _buffManagers[player.PlayerId] = new BuffManager(player);
            }

            // 清空事件记录
            EventRecorder.Clear();

            // 清空触发器
            TriggerManager.ClearAllTriggers();

            // 记录对局开始
            EventRecorder.Record(BattleEventType.BattleStart);
        }

        /// <summary>
        /// 结束对局。
        /// </summary>
        public void EndBattle(int winnerTeamId)
        {
            IsGameOver = true;
            WinnerTeamId = winnerTeamId;
            EventRecorder.Record(BattleEventType.BattleEnd);
        }
    }

    /// <summary>
    /// 待触发效果 —— 表示一个等待在堆叠2层步骤2处理的触发式效果。
    /// </summary>
    public class PendingTriggerEffect
    {
        /// <summary>效果来源玩家ID</summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        /// <summary>效果目标玩家ID</summary>
        public string TargetPlayerId { get; set; } = string.Empty;

        /// <summary>触发效果类型</summary>
        public Protocol.Enums.EffectType EffectType { get; set; }

        /// <summary>效果数值</summary>
        public int Value { get; set; }

        /// <summary>触发原因描述（用于日志）</summary>
        public string TriggerReason { get; set; } = string.Empty;
    }
}