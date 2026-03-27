#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.Data.ConfigData
{
    public static class CardConfigToEffectAdapter
    {
        private static readonly HashSet<EffectType> SupportedEffectTypes = new()
        {
            EffectType.Damage,
            EffectType.Pierce,
            EffectType.Heal,
            EffectType.Shield,
            EffectType.AddBuff,
            EffectType.Draw,
            EffectType.GainEnergy,
            EffectType.GenerateCard,
            EffectType.Lifesteal,
            EffectType.ReturnSourceCardToHandAtRoundEnd,
            EffectType.UpgradeCardsInHand,
        };

        private static readonly Dictionary<EffectType, SettleLayer> EffectLayerMap = new()
        {
            { EffectType.Shield, SettleLayer.Defense },
            { EffectType.Damage, SettleLayer.Damage },
            { EffectType.Lifesteal, SettleLayer.Damage },
            { EffectType.Pierce, SettleLayer.Damage },
            { EffectType.Draw, SettleLayer.Resource },
            { EffectType.GainEnergy, SettleLayer.Resource },
            { EffectType.GenerateCard, SettleLayer.Resource },
            { EffectType.ReturnSourceCardToHandAtRoundEnd, SettleLayer.Resource },
            { EffectType.Heal, SettleLayer.BuffSpecial },
            { EffectType.AddBuff, SettleLayer.BuffSpecial },
            { EffectType.UpgradeCardsInHand, SettleLayer.BuffSpecial },
        };

        public static List<EffectUnit> ConvertEffects(CardConfig card, string defaultTargetType = "Enemy")
        {
            var units = new List<EffectUnit>();
            if (card.Effects == null || card.Effects.Count == 0)
                return units;

            for (int effectIndex = 0; effectIndex < card.Effects.Count; effectIndex++)
            {
                var effect = card.Effects[effectIndex];
                ValidateSupportedFields(card, effect);

                int repeatCount = effect.RepeatCount > 0 ? effect.RepeatCount : 1;
                for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    units.Add(ConvertSingle(effect, effectIndex, repeatIndex, repeatCount, defaultTargetType));
                }
            }

            return units;
        }

        private static EffectUnit ConvertSingle(
            CardEffect effect,
            int effectIndex,
            int repeatIndex,
            int repeatCount,
            string defaultTargetType)
        {
            string targetType = ResolveTargetType(effect.TargetOverride, defaultTargetType);
            SettleLayer layer = ResolveLayer(effect.EffectType);
            string effectId = repeatCount > 1
                ? $"fx_{effectIndex:D2}_{repeatIndex:D2}"
                : $"fx_{effectIndex:D2}";

            return new EffectUnit
            {
                EffectId = effectId,
                Type = effect.EffectType,
                TargetType = targetType,
                ValueExpression = ResolveValueExpression(effect),
                Layer = layer,
                Conditions = BuildConditions(effect),
                Params = BuildParams(effect),
            };
        }

        private static void ValidateSupportedFields(CardConfig card, CardEffect effect)
        {
            string prefix = $"卡牌 {card.CardId} [{card.CardName}] 效果 {effect.EffectType}";

            if (!SupportedEffectTypes.Contains(effect.EffectType))
            {
                throw new InvalidOperationException(
                    $"{prefix} 不在当前 BattleCore 支持白名单内，请改为 Damage/Pierce/Heal/Shield/AddBuff/Draw/GainEnergy/GenerateCard/Lifesteal/ReturnSourceCardToHandAtRoundEnd/UpgradeCardsInHand。");
            }
        }

        private static string ResolveValueExpression(CardEffect effect)
        {
            return !string.IsNullOrWhiteSpace(effect.ValueExpression)
                ? effect.ValueExpression
                : effect.Value.ToString();
        }

        private static List<string> BuildConditions(CardEffect effect)
        {
            var conditions = new List<string>();
            if (effect.EffectConditions == null || effect.EffectConditions.Count == 0)
                return conditions;

            foreach (var condition in effect.EffectConditions)
                conditions.Add(ConvertCondition(condition));

            return conditions;
        }

        private static string ConvertCondition(EffectCondition condition)
        {
            return condition.ConditionType switch
            {
                EffectConditionType.EnemyPlayedDamageCard => BuildNumericCondition(
                    "opponent.playedDamageCardCount", ">=", "<", 1, condition.Negate),
                EffectConditionType.EnemyPlayedDefenseCard => BuildNumericCondition(
                    "opponent.playedDefenseCardCount", ">=", "<", 1, condition.Negate),
                EffectConditionType.EnemyPlayedCounterCard => BuildNumericCondition(
                    "opponent.playedCounterCardCount", ">=", "<", 1, condition.Negate),
                EffectConditionType.EnemyPlayedCardCountAtLeast => BuildNumericCondition(
                    "opponent.playedCardCount", ">=", "<", condition.ConditionValue, condition.Negate),
                EffectConditionType.MyDeckIsEmpty => condition.Negate
                    ? "self.deckCount != 0"
                    : "self.deckCount == 0",
                EffectConditionType.MyHandCardCountAtMost => BuildNumericCondition(
                    "self.handCount", "<=", ">", condition.ConditionValue, condition.Negate),
                EffectConditionType.MyHandCardCountAtLeast => BuildNumericCondition(
                    "self.handCount", ">=", "<", condition.ConditionValue, condition.Negate),
                EffectConditionType.MyPlayedCardCountAtLeast => BuildNumericCondition(
                    "self.playedCardCount", ">=", "<", condition.ConditionValue, condition.Negate),
                EffectConditionType.MyHpPercentAtMost => BuildNumericCondition(
                    "self.hp", "<=", ">", $"{condition.ConditionValue}%", condition.Negate),
                EffectConditionType.MyHpPercentAtLeast => BuildNumericCondition(
                    "self.hp", ">=", "<", $"{condition.ConditionValue}%", condition.Negate),
                EffectConditionType.EnemyHpPercentAtMost => BuildNumericCondition(
                    "opponent.hp", "<=", ">", $"{condition.ConditionValue}%", condition.Negate),
                EffectConditionType.EnemyIsStunned => BuildNumericCondition(
                    "opponent.isStunned", "==", "!=", 1, condition.Negate),
                EffectConditionType.RoundNumberAtLeast => BuildNumericCondition(
                    "round", ">=", "<", condition.ConditionValue, condition.Negate),
                _ => throw new InvalidOperationException(
                    $"当前 BattleCore 配置路径不支持条件 {condition.ConditionType}，请改为运行时已支持的条件表达。"),
            };
        }

        private static string BuildNumericCondition(string lhs, string positiveOp, string negativeOp, int rhs, bool negate)
        {
            return BuildNumericCondition(lhs, positiveOp, negativeOp, rhs.ToString(), negate);
        }

        private static string BuildNumericCondition(string lhs, string positiveOp, string negativeOp, string rhs, bool negate)
        {
            string op = negate ? negativeOp : positiveOp;
            return $"{lhs} {op} {rhs}";
        }

        private static string ResolveTargetType(CardTargetType? targetOverride, string defaultTargetType)
        {
            return targetOverride.HasValue
                ? CardTargetTypeToString(targetOverride.Value)
                : defaultTargetType;
        }

        private static SettleLayer ResolveLayer(EffectType effectType)
        {
            return EffectLayerMap.TryGetValue(effectType, out var layer)
                ? layer
                : SettleLayer.BuffSpecial;
        }

        private static Dictionary<string, string> BuildParams(CardEffect effect)
        {
            var parameters = new Dictionary<string, string>();

            if (effect.Duration > 0)
                parameters["duration"] = effect.Duration.ToString();

            if (effect.EffectType == EffectType.Lifesteal)
                parameters["percent"] = effect.Value.ToString();

            if (effect.EffectType == EffectType.AddBuff)
            {
                if (string.IsNullOrWhiteSpace(effect.BuffConfigId))
                    throw new InvalidOperationException("AddBuff 缺少 BuffConfigId。");

                parameters["buffConfigId"] = effect.BuffConfigId;
            }

            if (effect.EffectType == EffectType.GenerateCard)
            {
                if (string.IsNullOrWhiteSpace(effect.GenerateCardConfigId))
                    throw new InvalidOperationException("GenerateCard 缺少 GenerateCardConfigId。");

                parameters["configId"] = effect.GenerateCardConfigId;
                parameters["targetZone"] = string.IsNullOrWhiteSpace(effect.GenerateCardZone)
                    ? "Hand"
                    : effect.GenerateCardZone;
                parameters["count"] = effect.Value > 0 ? effect.Value.ToString() : "1";
                parameters["tempCard"] = effect.GenerateCardIsTemp ? "true" : "false";
            }

            if (effect.EffectType == EffectType.UpgradeCardsInHand)
            {
                parameters["projectionLifetime"] = string.IsNullOrWhiteSpace(effect.ProjectionLifetime)
                    ? "EndOfTurn"
                    : effect.ProjectionLifetime;
            }

            return parameters;
        }

        public static string CardTargetTypeToString(CardTargetType targetType)
        {
            return targetType switch
            {
                CardTargetType.None => "None",
                CardTargetType.Self => "Self",
                CardTargetType.CurrentEnemy => "Enemy",
                CardTargetType.AnyEnemy => "Enemy",
                CardTargetType.AnyAlly => "AllAllies",
                CardTargetType.AllAllies => "AllAllies",
                CardTargetType.AllEnemies => "AllEnemies",
                CardTargetType.All => "All",
                CardTargetType.Opponent => "Enemy",
                CardTargetType.AllOpponents => "AllEnemies",
                _ => "Enemy",
            };
        }
    }
}
