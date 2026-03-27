#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Resolvers
{
    /// <summary>
    /// 解析 EffectUnit.ValueExpression，输出整数值。
    /// 支持：
    /// - 字面值：10 / -3
    /// - preEffect[effectId].field
    /// - self.field / opponent.field
    /// - trigCtx.value / trigCtx.round / trigCtx.extra.key
    /// - sourceCard.instancePlayedCount / sourceCard.configPlayedCount
    /// - 简单整数算术：+ - * /
    /// </summary>
    public class DynamicParamResolver
    {
        private static readonly Regex TemplatePattern =
            new Regex(@"\{\{(.+?)\}\}", RegexOptions.Compiled);

        private static readonly Regex PreEffectPattern =
            new Regex(@"preEffect\[([^\]]+)\]\.(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex EntityFieldPattern =
            new Regex(@"(self|opponent)\.(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TriggerContextFieldPattern =
            new Regex(@"trigCtx\.(value|round|extra\.\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SourceCardFieldPattern =
            new Regex(@"sourceCard\.(instancePlayedCount|configPlayedCount)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public int Resolve(
            string expression,
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext = null)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            if (int.TryParse(expression.Trim(), out int literal))
                return literal;

            var match = TemplatePattern.Match(expression);
            if (!match.Success)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] invalid ValueExpression='{expression}', fallback 0.");
                return 0;
            }

            string innerExpr = match.Groups[1].Value.Trim();

            try
            {
                string expanded = ExpandReferences(innerExpr, ctx, effect, source, priorResults, triggerContext);
                return EvaluateArithmetic(expanded);
            }
            catch (Exception ex)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] evaluate '{expression}' failed: {ex.Message}, fallback 0.");
                return 0;
            }
        }

        private string ExpandReferences(
            string expr,
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            expr = PreEffectPattern.Replace(expr, m =>
            {
                string effectId = m.Groups[1].Value;
                string field = m.Groups[2].Value.ToLowerInvariant();

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
                    ctx.RoundLog.Add($"[DynamicParamResolver] preEffect '{effectId}' not found, fallback 0.");
                    return "0";
                }

                return field switch
                {
                    "totalrealhpdamage" => found.TotalRealHpDamage.ToString(),
                    "totalrealheal" => found.TotalRealHeal.ToString(),
                    "totalrealshield" => found.TotalRealShield.ToString(),
                    "success" => found.Success ? "1" : "0",
                    _ => GetExtraValue(found, field, ctx),
                };
            });

            expr = TriggerContextFieldPattern.Replace(expr, m =>
            {
                string field = m.Groups[1].Value;
                return GetTriggerContextValue(triggerContext, field, ctx);
            });

            expr = SourceCardFieldPattern.Replace(expr, m =>
            {
                string field = m.Groups[1].Value.ToLowerInvariant();
                return field switch
                {
                    "instanceplayedcount" => GetEffectParamValue(effect, "sourceCardInstancePlayedCount", ctx),
                    "configplayedcount" => GetEffectParamValue(effect, "sourceCardConfigPlayedCount", ctx),
                    _ => "0",
                };
            });

            expr = EntityFieldPattern.Replace(expr, m =>
            {
                string who = m.Groups[1].Value.ToLowerInvariant();
                string field = m.Groups[2].Value.ToLowerInvariant();

                Entity? targetEntity = who == "self" ? source : GetOpponent(ctx, source);
                if (targetEntity == null)
                {
                    ctx.RoundLog.Add($"[DynamicParamResolver] entity '{who}' not found, fallback 0.");
                    return "0";
                }

                return field switch
                {
                    "hp" => targetEntity.Hp.ToString(),
                    "maxhp" => targetEntity.MaxHp.ToString(),
                    "shield" => targetEntity.Shield.ToString(),
                    "armor" => targetEntity.Armor.ToString(),
                    "handcount" => GetHandCount(ctx, targetEntity.OwnerPlayerId).ToString(),
                    "deckcount" => GetDeckCount(ctx, targetEntity.OwnerPlayerId).ToString(),
                    _ => UnknownField(field, ctx),
                };
            });

            return expr;
        }

        private int EvaluateArithmetic(string expr)
        {
            expr = expr.Trim();
            if (int.TryParse(expr, out int simple))
                return simple;

            var tokens = Tokenize(expr);
            int pos = 0;
            long result = ParseExpr(tokens, ref pos);

            if (result > int.MaxValue) result = int.MaxValue;
            if (result < int.MinValue) result = int.MinValue;
            return (int)result;
        }

        private enum TokenType
        {
            Number,
            Plus,
            Minus,
            Mul,
            Div,
            LParen,
            RParen,
            Eof,
        }

        private struct Token
        {
            public TokenType Type;
            public long NumValue;
        }

        private static List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '(') { tokens.Add(new Token { Type = TokenType.LParen }); i++; continue; }
                if (c == ')') { tokens.Add(new Token { Type = TokenType.RParen }); i++; continue; }
                if (c == '+') { tokens.Add(new Token { Type = TokenType.Plus }); i++; continue; }
                if (c == '*') { tokens.Add(new Token { Type = TokenType.Mul }); i++; continue; }
                if (c == '/') { tokens.Add(new Token { Type = TokenType.Div }); i++; continue; }

                if (c == '-')
                {
                    bool isNegative = tokens.Count == 0
                        || (tokens[tokens.Count - 1].Type != TokenType.Number && tokens[tokens.Count - 1].Type != TokenType.RParen);
                    if (isNegative)
                    {
                        i++;
                        int start = i;
                        while (i < expr.Length && char.IsDigit(expr[i])) i++;
                        long value = long.Parse("-" + expr.Substring(start, i - start));
                        tokens.Add(new Token { Type = TokenType.Number, NumValue = value });
                    }
                    else
                    {
                        tokens.Add(new Token { Type = TokenType.Minus });
                        i++;
                    }
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < expr.Length && char.IsDigit(expr[i])) i++;
                    long value = long.Parse(expr.Substring(start, i - start));
                    tokens.Add(new Token { Type = TokenType.Number, NumValue = value });
                    continue;
                }

                i++;
            }

            tokens.Add(new Token { Type = TokenType.Eof });
            return tokens;
        }

        private long ParseExpr(List<Token> tokens, ref int pos)
        {
            long left = ParseTerm(tokens, ref pos);

            while (pos < tokens.Count)
            {
                var token = tokens[pos];
                if (token.Type == TokenType.Plus)
                {
                    pos++;
                    left += ParseTerm(tokens, ref pos);
                }
                else if (token.Type == TokenType.Minus)
                {
                    pos++;
                    left -= ParseTerm(tokens, ref pos);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private long ParseTerm(List<Token> tokens, ref int pos)
        {
            long left = ParsePrimary(tokens, ref pos);

            while (pos < tokens.Count)
            {
                var token = tokens[pos];
                if (token.Type == TokenType.Mul)
                {
                    pos++;
                    left *= ParsePrimary(tokens, ref pos);
                }
                else if (token.Type == TokenType.Div)
                {
                    pos++;
                    long divisor = ParsePrimary(tokens, ref pos);
                    left = divisor != 0 ? left / divisor : 0;
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private long ParsePrimary(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count)
                return 0;

            var token = tokens[pos];
            if (token.Type == TokenType.Number)
            {
                pos++;
                return token.NumValue;
            }

            if (token.Type == TokenType.LParen)
            {
                pos++;
                long value = ParseExpr(tokens, ref pos);
                if (pos < tokens.Count && tokens[pos].Type == TokenType.RParen)
                    pos++;
                return value;
            }

            return 0;
        }

        private static string GetEffectParamValue(EffectUnit effect, string key, BattleContext ctx)
        {
            if (!effect.Params.TryGetValue(key, out var rawValue))
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] effect param '{key}' missing, fallback 0.");
                return "0";
            }

            return int.TryParse(rawValue, out int parsed) ? parsed.ToString() : "0";
        }

        private static string GetTriggerContextValue(TriggerContext? triggerContext, string field, BattleContext ctx)
        {
            if (triggerContext == null)
            {
                ctx.RoundLog.Add($"[DynamicParamResolver] trigCtx '{field}' unavailable, fallback 0.");
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
                    if (extraValue is int intValue)
                        return intValue.ToString();

                    if (int.TryParse(extraValue?.ToString(), out int parsed))
                        return parsed.ToString();
                }
            }

            ctx.RoundLog.Add($"[DynamicParamResolver] trigCtx '{field}' not numeric, fallback 0.");
            return "0";
        }

        private static string GetExtraValue(EffectResult result, string field, BattleContext ctx)
        {
            if (result.Extra.TryGetValue(field, out var obj))
            {
                if (obj is int intValue)
                    return intValue.ToString();

                if (int.TryParse(obj?.ToString(), out int parsed))
                    return parsed.ToString();
            }

            ctx.RoundLog.Add($"[DynamicParamResolver] EffectResult '{result.EffectId}' has no extra field '{field}', fallback 0.");
            return "0";
        }

        private static int GetHandCount(BattleContext ctx, string playerId)
        {
            return ctx.GetPlayer(playerId)?.GetCardsInZone(CardZone.Hand).Count ?? 0;
        }

        private static int GetDeckCount(BattleContext ctx, string playerId)
        {
            return ctx.GetPlayer(playerId)?.GetCardsInZone(CardZone.Deck).Count ?? 0;
        }

        private static Entity? GetOpponent(BattleContext ctx, Entity source)
        {
            foreach (var kv in ctx.AllPlayers)
            {
                if (kv.Key != source.OwnerPlayerId)
                    return kv.Value.HeroEntity;
            }

            return null;
        }

        private static string UnknownField(string field, BattleContext ctx)
        {
            ctx.RoundLog.Add($"[DynamicParamResolver] unknown field '{field}', fallback 0.");
            return "0";
        }
    }
}
