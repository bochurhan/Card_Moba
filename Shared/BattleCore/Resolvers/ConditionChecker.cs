#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 效果条件检查器（ConditionChecker）—— 判断 EffectUnit.Conditions 是否全部满足。
    ///
    /// 条件语法（所有条件字符串大小写不敏感）：
    ///   "self.hp &lt; 30"               —— 自身 HP 低于 30
    ///   "self.hp &lt;= 50%"            —— 自身 HP 不超过最大 HP 的 50%
    ///   "opponent.shield == 0"         —— 对手无护盾
    ///   "self.handCount >= 3"          —— 手牌数量 ≥ 3
    ///   "hasBuff:burn"                 —— 对手持有 burn Buff
    ///   "selfHasBuff:shield_aura"      —— 自身持有指定 Buff
    ///   "opponentHasBuff:vulnerable"   —— 对手持有指定 Buff
    ///   "self.deckCount == 0"          —— 卡组已空
    ///
    /// 支持的比较运算符：==  !=  &lt;  &lt;=  >  >=
    ///
    /// 全部条件均通过时，效果才允许执行（AND 语义）。
    /// </summary>
    public class ConditionChecker
    {
        // 匹配 "entity.field op value" 格式（支持百分比值）
        private static readonly Regex _comparePattern =
            new Regex(@"^(self|opponent)\.([\w]+)\s*(==|!=|<=|>=|<|>)\s*(-?\d+)(%?)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _triggerContextPattern =
            new Regex(@"^trigCtx\.(value|round|extra\.[\w]+)\s*(==|!=|<=|>=|<|>)\s*(-?\d+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ══════════════════════════════════════════════════════════
        // 主入口
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查所有条件是否全部满足（AND 语义）。
        /// 空条件列表视为"无条件，始终通过"。
        /// </summary>
        /// <param name="conditions">条件字符串列表（来自 EffectUnit.Conditions）</param>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="source">施法者实体</param>
        /// <returns>全部条件满足时返回 true</returns>
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

        // ══════════════════════════════════════════════════════════
        // 单条件解析
        // ══════════════════════════════════════════════════════════

        private bool CheckSingle(string condition, BattleContext ctx, Entity source, TriggerContext? triggerContext)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;

            condition = condition.Trim();

            // ── 特殊语法：Buff 存在检查 ────────────────────────────

            // "selfHasBuff:configId"
            if (condition.StartsWith("selfHasBuff:", StringComparison.OrdinalIgnoreCase))
            {
                string buffConfigId = condition.Substring("selfHasBuff:".Length).Trim();
                return ctx.BuffManager.HasBuff(ctx, source.EntityId, buffConfigId);
            }

            // "opponentHasBuff:configId" 或 "hasBuff:configId"（默认对手）
            if (condition.StartsWith("opponentHasBuff:", StringComparison.OrdinalIgnoreCase) ||
                condition.StartsWith("hasBuff:", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = condition.StartsWith("hasBuff:", StringComparison.OrdinalIgnoreCase)
                    ? "hasBuff:" : "opponentHasBuff:";
                string buffConfigId = condition.Substring(prefix.Length).Trim();

                // 查找对手实体
                foreach (var kv in ctx.AllPlayers)
                {
                    if (kv.Key != source.OwnerPlayerId)
                        return ctx.BuffManager.HasBuff(ctx, kv.Value.HeroEntity.EntityId, buffConfigId);
                }
                return false;
            }

            // ── 通用比较语法：entity.field op value ───────────────

            var triggerMatch = _triggerContextPattern.Match(condition);
            if (triggerMatch.Success)
            {
                string trigField = triggerMatch.Groups[1].Value;
                string trigOp    = triggerMatch.Groups[2].Value;
                int trigRhsVal   = int.Parse(triggerMatch.Groups[3].Value);

                if (!TryGetTriggerContextValue(triggerContext, trigField, out int trigLhsVal))
                {
                    ctx.RoundLog.Add($"[ConditionChecker] 鈿狅笍 鏃犳硶璇诲彇 trigCtx.{trigField}锛屾潯浠惰涓轰笉婊¤冻銆?");
                    return false;
                }

                return Compare(trigLhsVal, trigOp, trigRhsVal);
            }

            var match = _comparePattern.Match(condition);
            if (!match.Success)
            {
                ctx.RoundLog.Add($"[ConditionChecker] ⚠️ 无法解析条件 '{condition}'，视为不满足。");
                return false;
            }

            string who        = match.Groups[1].Value.ToLowerInvariant();
            string field      = match.Groups[2].Value.ToLowerInvariant();
            string op         = match.Groups[3].Value;
            int    rhsVal     = int.Parse(match.Groups[4].Value);
            bool   isPercent  = match.Groups[5].Value == "%";

            // 获取目标实体
            Entity? target = who == "self" ? source : GetOpponent(ctx, source);
            if (target == null)
            {
                ctx.RoundLog.Add($"[ConditionChecker] ⚠️ 找不到 '{who}' 实体，条件视为不满足。");
                return false;
            }

            // 读取字段实际值
            int lhsVal = GetFieldValue(ctx, target, field);

            // 百分比：将 rhsVal 转为实际值（相对 maxHp）
            if (isPercent)
                rhsVal = target.MaxHp * rhsVal / 100;

            return Compare(lhsVal, op, rhsVal);
        }

        // ══════════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════════

        /// <summary>获取实体字段的整数值</summary>
        private int GetFieldValue(BattleContext ctx, Entity entity, string field)
        {
            switch (field)
            {
                case "hp":        return entity.Hp;
                case "maxhp":     return entity.MaxHp;
                case "shield":    return entity.Shield;
                case "armor":     return entity.Armor;
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
                default:
                    ctx.RoundLog.Add($"[ConditionChecker] ⚠️ 未知字段 '{field}'，返回 0。");
                    return 0;
            }
        }

        /// <summary>执行比较运算</summary>
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
                case "<":  return lhs <  rhs;
                case "<=": return lhs <= rhs;
                case ">":  return lhs >  rhs;
                case ">=": return lhs >= rhs;
                default:   return false;
            }
        }

        /// <summary>获取对手实体（1v1 场景；多人时取第一个非自身玩家）</summary>
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
