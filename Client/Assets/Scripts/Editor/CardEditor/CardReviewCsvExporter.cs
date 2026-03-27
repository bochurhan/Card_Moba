using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Editor.CardEditor
{
    internal static class CardReviewCsvExporter
    {
        private static readonly UTF8Encoding Utf8WithBom = new UTF8Encoding(true);

        public static string Export(string assetsPath, IReadOnlyList<CardEditData> cards)
        {
            string reviewDir = Path.GetFullPath(Path.Combine(assetsPath, "..", "..", "Config", "Excel"));
            Directory.CreateDirectory(reviewDir);

            string outPath = Path.Combine(reviewDir, "Cards.csv");
            using var writer = new StreamWriter(outPath, false, Utf8WithBom);

            writer.WriteLine("CardId,CardName,Description,TrackType,TargetType,HeroClass,EffectRange,Layer,Tags,EnergyCost,Rarity,UpgradedCardConfigId,EffectSummary,EffectsJson");

            foreach (var card in cards.OrderBy(c => c.CardId))
            {
                string tags = string.Join("|", card.GetTagList());
                string effectSummary = string.Join(" ; ", card.Effects.Select(BuildEffectSummary));
                string effectsJson = SerializeEffectsJson(card.Effects);

                writer.WriteLine(string.Join(",",
                    card.CardId.ToString(),
                    EscapeCsv(card.CardName),
                    EscapeCsv(card.Description),
                    card.TrackType,
                    card.TargetType,
                    card.HeroClass,
                    card.EffectRange,
                    card.Layer,
                    EscapeCsv(tags),
                    card.EnergyCost.ToString(),
                    card.Rarity.ToString(),
                    EscapeCsv(card.UpgradedCardConfigId),
                    EscapeCsv(effectSummary),
                    EscapeCsv(effectsJson)));
            }

            return outPath;
        }

        private static string BuildEffectSummary(EffectEditData effect)
        {
            string valueText = string.IsNullOrWhiteSpace(effect.ValueExpression)
                ? effect.Value.ToString()
                : effect.ValueExpression;

            var parts = new List<string>
            {
                $"{effect.EffectType} {valueText}"
            };

            if (effect.RepeatCount > 1)
                parts.Add($"x{effect.RepeatCount}");

            if (effect.TargetOverride.HasValue)
                parts.Add($"->{effect.TargetOverride.Value}");

            if (effect.Duration > 0)
                parts.Add($"dur={effect.Duration}");

            if (effect.EffectType == EffectType.AddBuff && !string.IsNullOrWhiteSpace(effect.BuffConfigId))
                parts.Add($"buff={effect.BuffConfigId}");

            if (effect.EffectType == EffectType.GenerateCard && !string.IsNullOrWhiteSpace(effect.GenerateCardConfigId))
                parts.Add($"card={effect.GenerateCardConfigId}@{effect.GenerateCardZone}" + (effect.GenerateCardIsTemp ? " temp" : ""));

            if (effect.EffectType == EffectType.ReturnSourceCardToHandAtRoundEnd)
                parts.Add("return-source-card@end-round");

            if (effect.EffectType == EffectType.UpgradeCardsInHand && !string.IsNullOrWhiteSpace(effect.ProjectionLifetime))
                parts.Add($"lifetime={effect.ProjectionLifetime}");

            if (effect.EffectConditions.Count > 0)
            {
                string conditions = string.Join("&", effect.EffectConditions.Select(BuildConditionSummary));
                parts.Add($"if[{conditions}]");
            }

            return string.Join(" ", parts);
        }

        private static string BuildConditionSummary(EffectConditionEditData condition)
        {
            string thresholdPart = condition.Threshold > 0 ? $":{condition.Threshold}" : string.Empty;
            return condition.Negate
                ? $"!{condition.ConditionType}{thresholdPart}"
                : $"{condition.ConditionType}{thresholdPart}";
        }

        private static string SerializeEffectsJson(IEnumerable<EffectEditData> effects)
        {
            return "[" + string.Join(",", effects.Select(SerializeEffectJson)) + "]";
        }

        private static string SerializeEffectJson(EffectEditData effect)
        {
            var fields = new List<string>
            {
                $"\"effectType\":{(int)effect.EffectType}",
                $"\"value\":{effect.Value}",
                $"\"repeatCount\":{effect.RepeatCount}",
                $"\"duration\":{effect.Duration}"
            };

            if (!string.IsNullOrWhiteSpace(effect.ValueExpression))
                fields.Add($"\"valueExpression\":{QuoteJson(effect.ValueExpression)}");

            if (effect.TargetOverride.HasValue)
                fields.Add($"\"targetOverride\":{QuoteJson(effect.TargetOverride.Value.ToString())}");

            if (effect.EffectType == EffectType.AddBuff && !string.IsNullOrWhiteSpace(effect.BuffConfigId))
                fields.Add($"\"buffConfigId\":{QuoteJson(effect.BuffConfigId)}");

            if (effect.EffectType == EffectType.GenerateCard)
            {
                if (!string.IsNullOrWhiteSpace(effect.GenerateCardConfigId))
                    fields.Add($"\"generateCardConfigId\":{QuoteJson(effect.GenerateCardConfigId)}");

                if (!string.IsNullOrWhiteSpace(effect.GenerateCardZone))
                    fields.Add($"\"generateCardZone\":{QuoteJson(effect.GenerateCardZone)}");

                if (effect.GenerateCardIsTemp)
                    fields.Add("\"generateCardIsTemp\":true");
            }

            if (effect.EffectType == EffectType.UpgradeCardsInHand && !string.IsNullOrWhiteSpace(effect.ProjectionLifetime))
                fields.Add($"\"projectionLifetime\":{QuoteJson(effect.ProjectionLifetime)}");

            if (effect.EffectConditions.Count > 0)
            {
                string conditions = string.Join(",", effect.EffectConditions.Select(SerializeConditionJson));
                fields.Add($"\"effectConditions\":[{conditions}]");
            }

            return "{" + string.Join(",", fields) + "}";
        }

        private static string SerializeConditionJson(EffectConditionEditData condition)
        {
            var fields = new List<string>
            {
                $"\"conditionType\":{QuoteJson(condition.ConditionType.ToString())}",
                $"\"threshold\":{condition.Threshold}",
                $"\"negate\":{condition.Negate.ToString().ToLowerInvariant()}",
                $"\"conditionBuffType\":{QuoteJson(string.Empty)}"
            };

            return "{" + string.Join(",", fields) + "}";
        }

        private static string QuoteJson(string value)
        {
            return "\"" + EscapeJson(value) + "\"";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
    }
}
