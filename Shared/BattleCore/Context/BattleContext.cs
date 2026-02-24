using System.Collections.Generic;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 战斗上下文 —— 一整局对战的所有可变状态。
    /// 这是结算引擎的"世界"，所有结算逻辑都读写这个对象。
    /// 设计原则：前后端共用同一个 BattleContext，保证结算结果一致。
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

        // ── 玩家状态 ──

        /// <summary>所有参战玩家的状态（原型阶段：2个玩家，1v1）</summary>
        public List<PlayerBattleState> Players { get; set; } = new List<PlayerBattleState>();

        // ── 本回合操作记录 ──

        /// <summary>本回合所有已提交的定策牌操作（回合末统一结算）</summary>
        public List<CardAction> PendingPlanActions { get; set; } = new List<CardAction>();

        /// <summary>本回合已结算的所有操作日志（用于回放和UI展示）</summary>
        public List<string> RoundLog { get; set; } = new List<string>();

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
        /// 清空本回合的临时数据，准备进入下一回合。
        /// </summary>
        public void ClearRoundData()
        {
            PendingPlanActions.Clear();
            RoundLog.Clear();
            // 重置所有玩家的回合锁定状态
            for (int i = 0; i < Players.Count; i++)
            {
                Players[i].IsLocked = false;
            }
        }
    }
}
