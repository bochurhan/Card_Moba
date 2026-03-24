#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 动态参数解析器（DynamicParamResolver）—— BattleCore V2 表达式求值核心。
    ///
    /// 职责：将 EffectUnit.ValueExpression 字符串求值为整数。
    ///
    /// 支持的表达式语法：
    ///   1. 字面整数：                "10"、"-3"
    ///   2. 前置效果引用：             "{{preEffect[effectId].totalRealHpDamage}}"
    ///   3. 上下文属性：               "{{self.hp}}"、"{{self.shield}}"、"{{self.handCount}}"
    ///                                "{{opponent.hp}}"、"{{opponent.shield}}"
    ///   4. 简单算术（+、-、*、/）：  "{{preEffect[dmg_01].totalRealHpDamage * 2}}"
    ///                                "{{(6 - self.handCount) * 3}}"
    ///
    /// 设计约束：
    ///   - 不依赖浮点数（结果始终为整数，中间计算用 long 防溢出后截断）
    ///   - 不引入外部脚本引擎（零依赖，完全自实现 mini 词法/解析器）
    ///   - 遇到无法解析的表达式返回 0 并写入 ctx.RoundLog 警告
    /// </summary>
    public class DynamicParamResolver
    {
        // 匹配 {{ ... }} 整体模板，内容为要求值的表达式
        private static readonly Regex _templatePattern =
            new Regex(@"\{\{(.+?)\}\}", RegexOptions.Compiled);

        // 匹配 preEffect[effectId].field 引用
        private static readonly Regex _preEffectPattern =
            new Regex(@"preEffect\[([^\]]+)\]\.(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 匹配 self.field 或 opponent.field 引用
        private static readonly Regex _entityFieldPattern =
            new Regex(@"(self|opponent)\.(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _triggerContextFieldPattern =
            new Regex(@"trigCtx\.(value|round|extra\.\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ══════════════════════════════════════════════════════════
        // 主入口
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将 ValueExpression 解析为整数值。
        /// </summary>
        /// <param name="expression">EffectUnit.ValueExpression 字符串</param>
        /// <param name="ctx">战斗上下文（用于读取运行时数据）</param>
        /// <param name="source">施法者实体</param>
        /// <param name="priorResults">本张牌前置效果的结果列表</param>
        /// <returns>解析出的整数值（失败时返回 0）</returns>
        public int Resolve(
            string expression,
            BattleContext ctx,
            Entity source,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext = null)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            // 快速路径：纯字面量整数（最常见情况，避免正则开销）
            if (int.TryParse(expression.Trim(), out int literal))
                return literal;

            // 模板路径：包含 {{ }} 的表达式
            var match = _templatePattern.Match(expression);
            if (!match.Success)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ 无法识别的 ValueExpression='{expression}'，返回 0。");
                return 0;
            }

            string innerExpr = match.Groups[1].Value.Trim();

            try
            {
                // 先展开所有引用占位符，再对纯数字算术表达式求值
                string expanded = ExpandReferences(innerExpr, ctx, source, priorResults, triggerContext);
                return EvaluateArithmetic(expanded, ctx);
            }
            catch (Exception ex)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ 表达式求值异常 '{expression}'：{ex.Message}，返回 0。");
                return 0;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 引用展开
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 将表达式中的引用（preEffect / self / opponent）替换为具体数字。
        /// </summary>
        private string ExpandReferences(
            string expr,
            BattleContext ctx,
            Entity source,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            // 1. 展开 preEffect[effectId].field
            expr = _preEffectPattern.Replace(expr, m =>
            {
                string effectId = m.Groups[1].Value;
                string field    = m.Groups[2].Value.ToLowerInvariant();

                // 查找前置效果结果（按 EffectId 匹配）
                EffectResult? found = null;
                foreach (var r in priorResults)
                {
                    if (r.EffectId.Equals(effectId, StringComparison.OrdinalIgnoreCase))
                    {
                        found = r;
                        break;
                    }
                }

                if (found == null)
                {
                    ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ 找不到前置效果 '{effectId}'，该引用替换为 0。");
                    return "0";
                }

                return field switch
                {
                    "totalrealhpdamage" => found.TotalRealHpDamage.ToString(),
                    "totalrealheal"     => found.TotalRealHeal.ToString(),
                    "totalrealshield"   => found.TotalRealShield.ToString(),
                    "success"           => found.Success ? "1" : "0",
                    _ => GetExtraValue(found, field, ctx),
                };
            });

            // 2. 展开 self.field 和 opponent.field
            expr = _triggerContextFieldPattern.Replace(expr, m =>
            {
                string field = m.Groups[1].Value;
                return GetTriggerContextValue(triggerContext, field, ctx);
            });

            expr = _entityFieldPattern.Replace(expr, m =>
            {
                string who   = m.Groups[1].Value.ToLowerInvariant();
                string field = m.Groups[2].Value.ToLowerInvariant();

                Entity? targetEntity = null;
                if (who == "self")
                {
                    targetEntity = source;
                }
                else // opponent
                {
                    foreach (var kv in ctx.AllPlayers)
                    {
                        if (kv.Key != source.OwnerPlayerId)
                        {
                            targetEntity = kv.Value.HeroEntity;
                            break;
                        }
                    }
                }

                if (targetEntity == null)
                {
                    ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ 找不到 '{who}' 实体，替换为 0。");
                    return "0";
                }

                switch (field)
                {
                    case "hp":        return targetEntity.Hp.ToString();
                    case "maxhp":     return targetEntity.MaxHp.ToString();
                    case "shield":    return targetEntity.Shield.ToString();
                    case "armor":     return targetEntity.Armor.ToString();
                    case "handcount": return GetHandCount(ctx, targetEntity.OwnerPlayerId).ToString();
                    case "deckcount": return GetDeckCount(ctx, targetEntity.OwnerPlayerId).ToString();
                    default:
                        ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ 未知字段 '{field}'（来源 '{who}'），替换为 0。");
                        return "0";
                }
            });

            return expr;
        }

        // ══════════════════════════════════════════════════════════
        // 简单算术求值（纯整数，不引入浮点）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 对展开后的纯数字算术表达式求值。
        /// 支持：括号、+、-、*、/（整除，向零截断）。
        /// </summary>
        private int EvaluateArithmetic(string expr, BattleContext ctx)
        {
            expr = expr.Trim();

            // 先尝试直接解析（展开后可能已经是纯数字）
            if (int.TryParse(expr, out int simple))
                return simple;

            var tokens = Tokenize(expr);
            int pos = 0;
            long result = ParseExpr(tokens, ref pos);

            // 结果钳制在 int 范围内
            if (result > int.MaxValue) result = int.MaxValue;
            if (result < int.MinValue) result = int.MinValue;
            return (int)result;
        }

        // ── 词法分析 ──────────────────────────────────────────────

        private enum TokenType { Number, Plus, Minus, Mul, Div, LParen, RParen, Eof }

        private struct Token
        {
            public TokenType Type;
            public long      NumValue;
        }

        private static List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(') { tokens.Add(new Token { Type = TokenType.LParen }); i++; continue; }
                if (c == ')') { tokens.Add(new Token { Type = TokenType.RParen }); i++; continue; }
                if (c == '+') { tokens.Add(new Token { Type = TokenType.Plus  }); i++; continue; }
                if (c == '-')
                {
                    // 判断是减法还是负数前缀（前面没有数字/右括号时为负号）
                    bool isNeg = tokens.Count == 0 ||
                                 (tokens[tokens.Count - 1].Type != TokenType.Number &&
                                  tokens[tokens.Count - 1].Type != TokenType.RParen);
                    if (isNeg)
                    {
                        // 负数：合并到下一个数字
                        i++;
                        int start = i;
                        while (i < expr.Length && char.IsDigit(expr[i])) i++;
                        long val = long.Parse("-" + expr.Substring(start, i - start));
                        tokens.Add(new Token { Type = TokenType.Number, NumValue = val });
                    }
                    else
                    {
                        tokens.Add(new Token { Type = TokenType.Minus }); i++;
                    }
                    continue;
                }
                if (c == '*') { tokens.Add(new Token { Type = TokenType.Mul }); i++; continue; }
                if (c == '/') { tokens.Add(new Token { Type = TokenType.Div }); i++; continue; }

                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < expr.Length && char.IsDigit(expr[i])) i++;
                    long val = long.Parse(expr.Substring(start, i - start));
                    tokens.Add(new Token { Type = TokenType.Number, NumValue = val });
                    continue;
                }

                // 未识别字符，跳过
                i++;
            }
            tokens.Add(new Token { Type = TokenType.Eof });
            return tokens;
        }

        // ── 递归下降解析 ──────────────────────────────────────────

        /// <summary>解析加减表达式（最低优先级）</summary>
        private long ParseExpr(List<Token> tokens, ref int pos)
        {
            long left = ParseTerm(tokens, ref pos);

            while (pos < tokens.Count)
            {
                var t = tokens[pos];
                if (t.Type == TokenType.Plus)
                {
                    pos++;
                    left += ParseTerm(tokens, ref pos);
                }
                else if (t.Type == TokenType.Minus)
                {
                    pos++;
                    left -= ParseTerm(tokens, ref pos);
                }
                else break;
            }
            return left;
        }

        /// <summary>解析乘除表达式（中优先级）</summary>
        private long ParseTerm(List<Token> tokens, ref int pos)
        {
            long left = ParsePrimary(tokens, ref pos);

            while (pos < tokens.Count)
            {
                var t = tokens[pos];
                if (t.Type == TokenType.Mul)
                {
                    pos++;
                    left *= ParsePrimary(tokens, ref pos);
                }
                else if (t.Type == TokenType.Div)
                {
                    pos++;
                    long divisor = ParsePrimary(tokens, ref pos);
                    left = divisor != 0 ? left / divisor : 0; // 除零保护
                }
                else break;
            }
            return left;
        }

        /// <summary>解析基元（数字或括号表达式）</summary>
        private long ParsePrimary(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count) return 0;
            var t = tokens[pos];

            if (t.Type == TokenType.Number)
            {
                pos++;
                return t.NumValue;
            }

            if (t.Type == TokenType.LParen)
            {
                pos++; // 跳过 (
                long val = ParseExpr(tokens, ref pos);
                if (pos < tokens.Count && tokens[pos].Type == TokenType.RParen)
                    pos++; // 跳过 )
                return val;
            }

            return 0;
        }

        // ══════════════════════════════════════════════════════════
        // 辅助工具方法
        // ══════════════════════════════════════════════════════════

        /// <summary>从 EffectResult.Extra 中读取自定义字段（返回整数）</summary>
        private string GetTriggerContextValue(TriggerContext? triggerContext, string field, BattleContext ctx)
        {
            if (triggerContext == null)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ trigCtx '{field}' 不可用，替换为 0。");
                return "0";
            }

            if (field.Equals("value", StringComparison.OrdinalIgnoreCase))
                return triggerContext.Value.ToString();

            if (field.Equals("round", StringComparison.OrdinalIgnoreCase))
                return triggerContext.Round.ToString();

            const string extraPrefix = "extra.";
            if (field.StartsWith(extraPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string extraKey = field.Substring(extraPrefix.Length);
                if (triggerContext.Extra.TryGetValue(extraKey, out var extraValue))
                {
                    if (extraValue is int intValue) return intValue.ToString();
                    if (int.TryParse(extraValue?.ToString(), out int parsed)) return parsed.ToString();
                }
            }

            ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ trigCtx '{field}' 不可解析为数値，替换为 0。");
            return "0";
        }

        private string GetExtraValue(EffectResult result, string field, BattleContext ctx)
        {
            if (result.Extra.TryGetValue(field, out var obj))
            {
                if (obj is int iv) return iv.ToString();
                if (int.TryParse(obj?.ToString(), out int pv)) return pv.ToString();
            }
            ctx.RoundLog.Add($"[DynamicParamResolver] ⚠️ EffectResult '{result.EffectId}' 无 Extra 字段 '{field}'，替换为 0。");
            return "0";
        }

        /// <summary>获取玩家手牌数量</summary>
        private int GetHandCount(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return 0;
            return player.GetCardsInZone(CardZone.Hand).Count;
        }

        /// <summary>获取玩家卡组剩余牌数</summary>
        private int GetDeckCount(BattleContext ctx, string playerId)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null) return 0;
            return player.GetCardsInZone(CardZone.Deck).Count;
        }
    }
}
