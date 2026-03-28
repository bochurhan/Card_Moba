#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 检查 EffectUnit.Conditions 是否全部满足。
    ///
    /// 支持的条件语法：
    ///   "self.hp < 30"
    ///   "self.hp <= 50%"
    ///   "opponent.shield == 0"
    ///   "self.handCount >= 3"
    ///   "hasBuff:burn"
    ///   "selfHasBuff:shield_aura"
    ///   "opponentHasBuff:vulnerable"
    ///   "self.deckCount == 0"
    ///   "trigCtx.value >= 10"
    ///   "round >= 3"
    ///
    /// 条件之间是 AND 关系；任意条件不满足都会返回 false。
    /// </summary>
    public class ConditionChecker
    {
        // 匹配 "entity.field op value" 语法，支持百分比。
        private static readonly Regex _comparePattern =
            new Regex(@"^(self|opponent)\.([\w]+)\s*(==|!=|<=|>=|<|>)\s*(-?\d+)(%?)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _triggerContextPattern =
            new Regex(@"^trigCtx\.(value|round|extra\.[\w]+)\s*(==|!=|<=|>=|<|>)\s*(-?\d+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _roundPattern =
            new Regex(@"^round\s*(==|!=|<=|>=|<|>)\s*(-?\d+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 检查条件列表是否全部满足。空列表视为无条件，始终通过。
        /// </summary>
        public bool Check(List<string> conditions, BattleContext ctx, Entity source, TriggerContext? triggerContext = null)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            foreach (var condition in conditions)
            {
                if (!CheckSingle(condition, ctx, source, triggerContext))
                {
                    ctx.RoundLog.Add($"[ConditionChecker] 条件未满足：'{condition}'，效果跳过。");
                    return false;
                }
            }

            return true;
        }

        private bool CheckSingle(string condition, BattleContext ctx, Entity source, TriggerContext? triggerContext)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            condition = condition.Trim();

            if (condition.StartsWith("selfHasBuff:", StringComparison.OrdinalIgnoreCase))
            {
                string buffConfigId = condition.Substring("selfHasBuff:".Length).Trim();
                return ctx.BuffManager.HasBuff(ctx, source.EntityId, buffConfigId);
            }

            if (condition.StartsWith("opponentHasBuff:", StringComparison.OrdinalIgnoreCase) ||
                condition.StartsWith("hasBuff:", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = condition.StartsWith("hasBuff:", StringComparison.OrdinalIgnoreCase)
                    ? "hasBuff:"
                    : "opponentHasBuff:";
                string buffConfigId = condition.Substring(prefix.Length).Trim();

                foreach (var kv in ctx.AllPlayers)
                {
                    if (kv.Key != source.OwnerPlayerId)
                        return ctx.BuffManager.HasBuff(ctx, kv.Value.HeroEntity.EntityId, buffConfigId);
                }

                return false;
            }

            var triggerMatch = _triggerContextPattern.Match(condition);
            if (triggerMatch.Success)
            {
                string trigField = triggerMatch.Groups[1].Value;
                string trigOp = triggerMatch.Groups[2].Value;
                int trigRhsVal = int.Parse(triggerMatch.Groups[3].Value);

                if (!TryGetTriggerContextValue(triggerContext, trigField, out int trigLhsVal))
                {
                    ctx.RoundLog.Add($"[ConditionChecker] 无法读取 trigCtx.{trigField}，条件视为不满足。");
                    return false;
                }

                return Compare(trigLhsVal, trigOp, trigRhsVal);
            }

            var roundMatch = _roundPattern.Match(condition);
            if (roundMatch.Success)
            {
                string roundOp = roundMatch.Groups[1].Value;
                int roundRhsVal = int.Parse(roundMatch.Groups[2].Value);
                return Compare(ctx.CurrentRound, roundOp, roundRhsVal);
            }

            var match = _comparePattern.Match(condition);
            if (!match.Success)
            {
                ctx.RoundLog.Add($"[ConditionChecker] 无法解析条件 '{condition}'，视为不满足。");
                return false;
            }

            string who = match.Groups[1].Value.ToLowerInvariant();
            string field = match.Groups[2].Value.ToLowerInvariant();
            string op = match.Groups[3].Value;
            int rhsVal = int.Parse(match.Groups[4].Value);
            bool isPercent = match.Groups[5].Value == "%";

            Entity? target = who == "self" ? source : GetOpponent(ctx, source);
            if (target == null)
            {
                ctx.RoundLog.Add($"[ConditionChecker] 找不到 '{who}' 实体，条件视为不满足。");
                return false;
            }

            int lhsVal = GetFieldValue(ctx, target, field);

            if (isPercent)
                rhsVal = target.MaxHp * rhsVal / 100;

            return Compare(lhsVal, op, rhsVal);
        }

        /// <summary>读取实体字段对应的整数值。</summary>
        private int GetFieldValue(BattleContext ctx, Entity entity, string field)
        {
            switch (field)
            {
                case "hp":        return entity.Hp;
                case "maxhp":     return entity.MaxHp;
                case "shield":    return entity.Shield;
                case "armor":     return entity.Armor;
                case "isstunned": return entity.IsStunned ? 1 : 0;
                case "handcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.GetCardsInZone(Foundation.CardZone.Hand).Count ?? 0;
                }
                case "deckcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.GetCardsInZone(Foundation.CardZone.Deck).Count ?? 0;
                }
                case "playedcardcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.PlayedCardCountThisRound ?? 0;
                }
                case "playeddamagecardcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.PlayedDamageCardCountThisRound ?? 0;
                }
                case "playeddefensecardcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.PlayedDefenseCardCountThisRound ?? 0;
                }
                case "playedcountercardcount":
                {
                    var player = ctx.GetPlayer(entity.OwnerPlayerId);
                    return player?.PlayedCounterCardCountThisRound ?? 0;
                }
                default:
                    ctx.RoundLog.Add($"[ConditionChecker] 未知字段 '{field}'，返回 0。");
                    return 0;
            }
        }

        /// <summary>尝试读取 TriggerContext 中的数值字段。</summary>
        private bool TryGetTriggerContextValue(TriggerContext? triggerContext, string field, out int value)
        {
            value = 0;
            if (triggerContext == null)
                return false;

            if (field.Equals("value", StringComparison.OrdinalIgnoreCase))
            {
                value = triggerContext.Value;
                return true;
            }

            if (field.Equals("round", StringComparison.OrdinalIgnoreCase))
            {
                value = triggerContext.Round;
                return true;
            }

            const string extraPrefix = "extra.";
            if (field.StartsWith(extraPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string extraKey = field.Substring(extraPrefix.Length);
                if (triggerContext.Extra.TryGetValue(extraKey, out var rawValue))
                {
                    if (rawValue is int intValue)
                    {
                        value = intValue;
                        return true;
                    }

                    if (int.TryParse(rawValue?.ToString(), out int parsed))
                    {
                        value = parsed;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Compare(int lhs, string op, int rhs)
        {
            switch (op)
            {
                case "==": return lhs == rhs;
                case "!=": return lhs != rhs;
                case "<":  return lhs < rhs;
                case "<=": return lhs <= rhs;
                case ">":  return lhs > rhs;
                case ">=": return lhs >= rhs;
                default:   return false;
            }
        }

        /// <summary>获取对手实体；多人场景下取第一个非自己玩家。</summary>
        private Entity? GetOpponent(BattleContext ctx, Entity source)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                if (kv.Key != source.OwnerPlayerId)
                    return kv.Value.HeroEntity;
            }

            return null;
        }
    }
}
