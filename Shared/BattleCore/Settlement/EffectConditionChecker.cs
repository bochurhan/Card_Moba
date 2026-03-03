#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 效果条件求值器 —— 将 EffectCondition 配置转换为运行时布尔结果。
    ///
    /// 职责：
    ///   - 统一处理所有 EffectConditionType 的检查逻辑
    ///   - 被 SettlementEngine（Conditional 效果检查）和 RoundManager（打出条件检查）共同使用
    ///   - 无状态静态工具类，所有状态从 BattleContext 中读取
    ///
    /// 调用方式：
    ///   bool pass = EffectConditionChecker.EvaluateAll(conditions, sourcePlayerId, targetPlayerId, ctx);
    /// </summary>
    public static class EffectConditionChecker
    {
        /// <summary>
        /// 评估条件列表中的所有条件（AND 语义）。
        /// 所有条件均满足时返回 true；任意一个不满足则返回 false。
        /// 空列表视为"无条件"，直接返回 true。
        /// </summary>
        /// <param name="conditions">要检查的条件列表</param>
        /// <param name="sourcePlayerId">出牌玩家 ID（"我方"视角）</param>
        /// <param name="targetPlayerId">目标玩家 ID（"敌方"视角，可为 null）</param>
        /// <param name="ctx">战斗上下文</param>
        /// <returns>所有条件均满足则 true</returns>
        public static bool EvaluateAll(
            List<EffectCondition> conditions,
            string sourcePlayerId,
            string? targetPlayerId,
            BattleContext ctx)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            foreach (var condition in conditions)
            {
                if (!Evaluate(condition, sourcePlayerId, targetPlayerId, ctx))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 评估单个条件。
        /// </summary>
        public static bool Evaluate(
            EffectCondition condition,
            string sourcePlayerId,
            string? targetPlayerId,
            BattleContext ctx)
        {
            bool result = EvaluateCore(condition, sourcePlayerId, targetPlayerId, ctx);
            return condition.Negate ? !result : result;
        }

        // ══════════════════════════════════════════════════════════
        // 核心条件求值（按 EffectConditionType 分组）
        // ══════════════════════════════════════════════════════════

        private static bool EvaluateCore(
            EffectCondition condition,
            string sourcePlayerId,
            string? targetPlayerId,
            BattleContext ctx)
        {
            var source = ctx.GetPlayer(sourcePlayerId);
            var target = targetPlayerId != null ? ctx.GetPlayer(targetPlayerId) : null;

            switch (condition.ConditionType)
            {
                // ─────────────────────────────────────────────────
                // 100-199: 敌方卡牌状态
                // ─────────────────────────────────────────────────

                case EffectConditionType.EnemyPlayedDamageCard:
                    // 检查 ValidPlanCards（已过滤反制的有效牌）中，是否存在目标玩家的伤害牌
                    return EnemyHasTagInValidCards(ctx, sourcePlayerId, CardTag.Damage);

                case EffectConditionType.EnemyPlayedDefenseCard:
                    return EnemyHasTagInValidCards(ctx, sourcePlayerId, CardTag.Defense);

                case EffectConditionType.EnemyPlayedCounterCard:
                    // 反制牌在 PendingPlanCards 中（本回合新提交的），而不是 ValidPlanCards
                    return EnemyHasTagInPendingCards(ctx, sourcePlayerId, CardTag.Counter);

                case EffectConditionType.EnemyPlayedCardCountAtLeast:
                {
                    int count = CountEnemyValidCards(ctx, sourcePlayerId);
                    return count >= condition.ConditionValue;
                }

                // ─────────────────────────────────────────────────
                // 200-299: 我方手牌/牌库状态
                // ─────────────────────────────────────────────────

                case EffectConditionType.MyDeckIsEmpty:
                    return IsDeckEmpty(ctx, sourcePlayerId);

                case EffectConditionType.MyHandCardCountAtMost:
                    return GetHandCardCount(ctx, sourcePlayerId) <= condition.ConditionValue;

                case EffectConditionType.MyHandCardCountAtLeast:
                    return GetHandCardCount(ctx, sourcePlayerId) >= condition.ConditionValue;

                case EffectConditionType.MyPlayedCardCountAtLeast:
                {
                    int played = CountMyValidCards(ctx, sourcePlayerId);
                    return played >= condition.ConditionValue;
                }

                // ─────────────────────────────────────────────────
                // 300-399: 我方角色状态
                // ─────────────────────────────────────────────────

                case EffectConditionType.MyHpPercentAtMost:
                {
                    if (source == null || source.MaxHp <= 0) return false;
                    int percent = source.Hp * 100 / source.MaxHp;
                    return percent <= condition.ConditionValue;
                }

                case EffectConditionType.MyHpPercentAtLeast:
                {
                    if (source == null || source.MaxHp <= 0) return false;
                    int percent = source.Hp * 100 / source.MaxHp;
                    return percent >= condition.ConditionValue;
                }

                case EffectConditionType.MyHasBuffType:
                {
                    if (source == null) return false;
                    var bm = ctx.GetBuffManager(sourcePlayerId);
                    return bm != null && bm.HasBuffType(condition.ConditionBuffType);
                }

                case EffectConditionType.MyStrengthAtLeast:
                    return source != null && source.Strength >= condition.ConditionValue;

                // ─────────────────────────────────────────────────
                // 400-499: 敌方角色状态
                // ─────────────────────────────────────────────────

                case EffectConditionType.EnemyHpPercentAtMost:
                {
                    var enemy = GetEnemy(ctx, sourcePlayerId, targetPlayerId);
                    if (enemy == null || enemy.MaxHp <= 0) return false;
                    int percent = enemy.Hp * 100 / enemy.MaxHp;
                    return percent <= condition.ConditionValue;
                }

                case EffectConditionType.EnemyHasBuffType:
                {
                    var enemy = GetEnemy(ctx, sourcePlayerId, targetPlayerId);
                    if (enemy == null) return false;
                    var bm = ctx.GetBuffManager(enemy.PlayerId);
                    return bm != null && bm.HasBuffType(condition.ConditionBuffType);
                }

                case EffectConditionType.EnemyIsStunned:
                {
                    var enemy = GetEnemy(ctx, sourcePlayerId, targetPlayerId);
                    return enemy != null && enemy.IsStunned;
                }

                // ─────────────────────────────────────────────────
                // 500-599: 全局/环境状态
                // ─────────────────────────────────────────────────

                case EffectConditionType.RoundNumberAtLeast:
                    return ctx.CurrentRound >= condition.ConditionValue;

                default:
                    // 未知条件类型：记录警告并返回 false（保守策略）
                    ctx.RoundLog.Add($"[ConditionChecker] 未知条件类型: {condition.ConditionType}，返回 false");
                    return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 私有辅助方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查 ValidPlanCards 中是否存在属于敌方且带有指定标签的卡牌。
        /// ValidPlanCards = 本回合已过反制校验的有效定策牌。
        /// </summary>
        private static bool EnemyHasTagInValidCards(BattleContext ctx, string myPlayerId, CardTag tag)
        {
            var myPlayer = ctx.GetPlayer(myPlayerId);
            if (myPlayer == null) return false;

            foreach (var card in ctx.ValidPlanCards)
            {
                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner == null) continue;

                // 敌方 = 不同队伍（1v1场景下即对手）
                if (cardOwner.TeamId != myPlayer.TeamId && card.Config.HasTag(tag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查 PendingPlanCards 中是否存在属于敌方且带有指定标签的卡牌。
        /// 用于检查反制牌（反制牌在 PendingPlanCards 而非 ValidPlanCards）。
        /// </summary>
        private static bool EnemyHasTagInPendingCards(BattleContext ctx, string myPlayerId, CardTag tag)
        {
            var myPlayer = ctx.GetPlayer(myPlayerId);
            if (myPlayer == null) return false;

            foreach (var card in ctx.PendingPlanCards)
            {
                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner == null) continue;

                if (cardOwner.TeamId != myPlayer.TeamId && card.Config.HasTag(tag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 统计 ValidPlanCards 中属于敌方的卡牌数量。
        /// </summary>
        private static int CountEnemyValidCards(BattleContext ctx, string myPlayerId)
        {
            var myPlayer = ctx.GetPlayer(myPlayerId);
            if (myPlayer == null) return 0;

            int count = 0;
            foreach (var card in ctx.ValidPlanCards)
            {
                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner != null && cardOwner.TeamId != myPlayer.TeamId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计 ValidPlanCards 中属于我方的卡牌数量（本回合已提交的有效牌）。
        /// </summary>
        private static int CountMyValidCards(BattleContext ctx, string myPlayerId)
        {
            int count = 0;
            foreach (var card in ctx.ValidPlanCards)
            {
                if (card.SourcePlayerId == myPlayerId)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 检查指定玩家的牌库是否为空。
        /// 通过 PlayerBattleState.DeckCount 属性读取（需要 PlayerBattleState 暴露该信息）。
        /// </summary>
        private static bool IsDeckEmpty(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return false;
            // 注意：DeckCount 需要在 PlayerBattleState 中维护
            return player.DeckCount <= 0;
        }

        /// <summary>
        /// 获取指定玩家的当前手牌数量。
        /// </summary>
        private static int GetHandCardCount(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return 0;
            return player.HandCount;
        }

        /// <summary>
        /// 获取我方视角的敌方玩家。
        /// 优先使用 targetPlayerId；若为空则从 ctx.Players 中找不同队伍的存活玩家。
        /// </summary>
        private static PlayerBattleState? GetEnemy(BattleContext ctx, string myPlayerId, string? targetPlayerId)
        {
            if (!string.IsNullOrEmpty(targetPlayerId))
                return ctx.GetPlayer(targetPlayerId);

            var myPlayer = ctx.GetPlayer(myPlayerId);
            if (myPlayer == null) return null;

            foreach (var player in ctx.Players)
            {
                if (player.TeamId != myPlayer.TeamId && player.IsAlive)
                    return player;
            }
            return null;
        }
    }
}
