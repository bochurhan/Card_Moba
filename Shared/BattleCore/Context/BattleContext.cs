using System.Collections.Generic;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 战斗上下文 —— 一整局对战的所有可变状态。
    /// 这是结算引擎的"世界"，所有结算逻辑都读写这个对象。
    /// 
    /// 设计原则：
    /// - 前后端共用同一个 BattleContext，保证结算结果一致
    /// - 符合《定策牌结算机制 V4.0》，支持跨回合反制牌、触发式效果等
    /// </summary>
    public class BattleContext
    {
        // ── 对局基础信息 ──

        /// <summary>当前回合数（从1开始）</summary>
        public int CurrentRound { get; set; } = 1;

        /// <summary>最大回合数（达到后强制结束）</summary>
        public int MaxRounds { get; set; } = 25;

        /// <summary>对局是否已结束</summary>
        public bool IsGameOver { get; set; }

        /// <summary>获胜玩家ID（-1 表示平局或未结束）</summary>
        public int WinnerPlayerId { get; set; } = -1;

        /// <summary>获胜队伍ID（-1 表示平局或未结束）</summary>
        public int WinnerTeamId { get; set; } = -1;

        // ── 玩家状态 ──

        /// <summary>所有参战玩家的状态（原型阶段：2个玩家，1v1）</summary>
        public List<PlayerBattleState> Players { get; set; } = new List<PlayerBattleState>();

        // ── 本回合操作记录 ──

        /// <summary>本回合所有已提交的定策牌操作（回合末统一结算）</summary>
        public List<CardAction> PendingPlanActions { get; set; } = new List<CardAction>();

        /// <summary>本回合已结算的所有操作日志（用于回放和UI展示）</summary>
        public List<string> RoundLog { get; set; } = new List<string>();

        // ── 跨回合数据（反制牌系统） ──

        /// <summary>
        /// 上回合提交的反制牌（本回合堆叠0层触发校验）
        /// 根据文档：反制牌本回合提交锁定，下回合堆叠0层触发
        /// </summary>
        public List<CardAction> PendingCounterCards { get; set; } = new List<CardAction>();

        /// <summary>
        /// 本回合被反制作废的卡牌操作（用于动画展示和日志）
        /// </summary>
        public List<CardAction> CounteredActions { get; set; } = new List<CardAction>();

        /// <summary>
        /// 本回合有效的定策牌（未被反制、未被作废）
        /// 在堆叠0层结算完成后填充，供后续堆叠层使用
        /// </summary>
        public List<CardAction> ValidPlanActions { get; set; } = new List<CardAction>();

        // ── 触发式效果追踪 ──

        /// <summary>
        /// 本回合待触发的效果列表（堆叠2层步骤2处理）
        /// </summary>
        public List<PendingTriggerEffect> PendingTriggerEffects { get; set; } = new List<PendingTriggerEffect>();

        /// <summary>
        /// 本回合是否已触发连锁（连锁封顶规则：单回合仅1次连锁）
        /// </summary>
        public bool HasChainTriggeredThisRound { get; set; }

        // ── 便捷方法 ──

        /// <summary>
        /// 根据玩家ID获取玩家状态。
        /// </summary>
        /// <param name="playerId">目标玩家ID</param>
        /// <returns>玩家状态，找不到则返回 null</returns>
        public PlayerBattleState? GetPlayer(int playerId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == playerId)
                    return Players[i];
            }
            return null;
        }

        /// <summary>
        /// 获取指定玩家的对手（1v1 原型用）。
        /// </summary>
        /// <param name="playerId">当前玩家ID</param>
        /// <returns>对手的状态，找不到则返回 null</returns>
        public PlayerBattleState? GetOpponent(int playerId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId != playerId)
                    return Players[i];
            }
            return null;
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
            foreach (var action in PendingPlanActions)
            {
                // 使用 Flags 检查：只要包含"反制"类型就转移
                if ((action.Card.SubType & Protocol.Enums.CardSubType.反制) != 0)
                {
                    PendingCounterCards.Add(action);
                }
            }

            // 清空本回合数据
            PendingPlanActions.Clear();
            ValidPlanActions.Clear();
            CounteredActions.Clear();
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
            foreach (var player in Players)
            {
                player.OnRoundStart();
            }
        }
    }

    /// <summary>
    /// 待触发效果 —— 表示一个等待在堆叠2层步骤2处理的触发式效果。
    /// </summary>
    public class PendingTriggerEffect
    {
        /// <summary>效果来源玩家ID</summary>
        public int SourcePlayerId { get; set; }

        /// <summary>效果目标玩家ID</summary>
        public int TargetPlayerId { get; set; }

        /// <summary>触发效果类型</summary>
        public Protocol.Enums.EffectType EffectType { get; set; }

        /// <summary>效果数值</summary>
        public int Value { get; set; }

        /// <summary>触发原因描述（用于日志）</summary>
        public string TriggerReason { get; set; } = string.Empty;
    }
}