using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Definitions;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.Server.Host.Config
{
    /// <summary>
    /// 服务端 BattleFactory 装配器。
    /// 负责把 cards.json 和运行时 Buff 定义接入 BattleCore。
    /// </summary>
    public sealed class ServerBattleFactoryFactory
    {
        private readonly ServerCardCatalog _cardCatalog;

        public ServerBattleFactoryFactory(ServerCardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
        }

        public BattleFactory Create()
        {
            return new BattleFactory
            {
                BuffConfigProvider = ResolveRuntimeBuffConfig,
                CardDefinitionProvider = BuildCardDefinition,
            };
        }

        private BattleCardDefinition? BuildCardDefinition(string configId)
        {
            var cardConfig = _cardCatalog.GetCard(configId);
            if (cardConfig == null)
                return null;

            string defaultTarget = ServerCardEffectAdapter.CardTargetTypeToString(cardConfig.TargetType);
            return new BattleCardDefinition
            {
                ConfigId = configId,
                IsExhaust = cardConfig.HasTag(CardTag.Exhaust),
                IsStatCard = cardConfig.HasTag(CardTag.Status),
                EnergyCost = cardConfig.EnergyCost,
                UpgradedConfigId = cardConfig.UpgradedCardConfigId,
                Effects = ServerCardEffectAdapter.ConvertEffects(cardConfig, defaultTarget),
            };
        }

        private static BuffConfig? ResolveRuntimeBuffConfig(string buffId)
        {
            return buffId switch
            {
                "strength" => CreateBuff("strength", "力量", "提高造成的伤害。", BuffType.Strength, true, BuffStackRule.RefreshDuration, 99, 0, 0),
                "weak" => CreateBuff("weak", "虚弱", "造成的伤害降低 25%。", BuffType.Weak, false, BuffStackRule.RefreshDuration, 99, 1, 25),
                "vulnerable" => CreateBuff("vulnerable", "易伤", "受到的伤害增加 50%。", BuffType.Vulnerable, false, BuffStackRule.StackValue, 99, 1, 50),
                "no_draw_this_turn" => CreateBuff("no_draw_this_turn", "本回合禁止抽牌", "本回合剩余时间内无法再抽牌。", BuffType.NoDrawThisTurn, false, BuffStackRule.RefreshDuration, 1, 1, 0),
                "no_damage_card_this_turn" => CreateBuff("no_damage_card_this_turn", "本回合禁止伤害牌", "本回合剩余时间内无法再打出伤害牌。", BuffType.NoDamageCardThisTurn, false, BuffStackRule.RefreshDuration, 1, 1, 0),
                "delayed_vulnerable_next_round" => CreateBuff("delayed_vulnerable_next_round", "下回合易伤", "下回合开始时获得对应数值的易伤。", BuffType.DelayedVulnerableNextRound, false, BuffStackRule.StackValue, 99, 2, 50, isHidden: true),
                "blood_ritual" => CreateBuff("blood_ritual", "血祭", "每次失去生命时获得力量。", BuffType.BloodRitual, true, BuffStackRule.RefreshDuration, 1, 0, 1),
                "corruption" => CreateBuff("corruption", "腐化", "每回合前 X 张牌费用变为 0，且结算后消耗。", BuffType.Corruption, true, BuffStackRule.StackValue, 99, 0, 2),
                _ => null,
            };
        }

        private static BuffConfig CreateBuff(
            string id,
            string name,
            string description,
            BuffType buffType,
            bool isBuff,
            BuffStackRule stackRule,
            int maxStacks,
            int duration,
            int value,
            bool isHidden = false)
        {
            return new BuffConfig
            {
                BuffId = id,
                BuffName = name,
                Description = description,
                BuffType = buffType,
                IsBuff = isBuff,
                StackRule = stackRule,
                MaxStacks = maxStacks,
                DefaultDuration = duration,
                DefaultValue = value,
                IsDispellable = true,
                IsPurgeable = true,
                IsHidden = isHidden,
            };
        }

        private static class ServerCardEffectAdapter
        {
            private static readonly HashSet<EffectType> SupportedEffectTypes = new HashSet<EffectType>
            {
                EffectType.Damage,
                EffectType.Pierce,
                EffectType.Heal,
                EffectType.Shield,
                EffectType.AddBuff,
                EffectType.Draw,
                EffectType.GainEnergy,
                EffectType.GenerateCard,
                EffectType.MoveSelectedCardToDeckTop,
                EffectType.Lifesteal,
                EffectType.ReturnSourceCardToHandAtRoundEnd,
                EffectType.UpgradeCardsInHand,
            };

            private static readonly Dictionary<EffectType, SettlementLayer> EffectLayerMap = new Dictionary<EffectType, SettlementLayer>
            {
                { EffectType.Shield, SettlementLayer.Defense },
                { EffectType.Damage, SettlementLayer.Damage },
                { EffectType.Lifesteal, SettlementLayer.Damage },
                { EffectType.Pierce, SettlementLayer.Damage },
                { EffectType.Draw, SettlementLayer.Resource },
                { EffectType.GainEnergy, SettlementLayer.Resource },
                { EffectType.GenerateCard, SettlementLayer.Resource },
                { EffectType.MoveSelectedCardToDeckTop, SettlementLayer.Resource },
                { EffectType.ReturnSourceCardToHandAtRoundEnd, SettlementLayer.Resource },
                { EffectType.Heal, SettlementLayer.BuffSpecial },
                { EffectType.AddBuff, SettlementLayer.BuffSpecial },
                { EffectType.UpgradeCardsInHand, SettlementLayer.BuffSpecial },
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
                        units.Add(ConvertSingle(effect, effectIndex, repeatIndex, repeatCount, defaultTargetType));
                }

                return units;
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
                    _ => "Enemy",
                };
            }

            private static EffectUnit ConvertSingle(CardEffect effect, int effectIndex, int repeatIndex, int repeatCount, string defaultTargetType)
            {
                string effectId = repeatCount > 1 ? $"fx_{effectIndex:D2}_{repeatIndex:D2}" : $"fx_{effectIndex:D2}";
                return new EffectUnit
                {
                    EffectId = effectId,
                    Type = effect.EffectType,
                    TargetType = effect.TargetOverride.HasValue ? CardTargetTypeToString(effect.TargetOverride.Value) : defaultTargetType,
                    ValueExpression = string.IsNullOrWhiteSpace(effect.ValueExpression) ? effect.Value.ToString() : effect.ValueExpression,
                    Layer = EffectLayerMap.TryGetValue(effect.EffectType, out var layer) ? layer : SettlementLayer.BuffSpecial,
                    Conditions = BuildConditions(effect),
                    Params = BuildParams(effect),
                };
            }

            private static void ValidateSupportedFields(CardConfig card, CardEffect effect)
            {
                if (!SupportedEffectTypes.Contains(effect.EffectType))
                    throw new InvalidOperationException($"卡牌 {card.CardId} [{card.CardName}] 效果 {effect.EffectType} 不在当前 BattleCore 支持白名单内。");
            }

            private static List<string> BuildConditions(CardEffect effect)
            {
                var conditions = new List<string>();
                if (effect.EffectConditions == null)
                    return conditions;

                foreach (var condition in effect.EffectConditions)
                    conditions.Add(ConvertCondition(condition));
                return conditions;
            }

            private static string ConvertCondition(EffectCondition condition)
            {
                return condition.ConditionType switch
                {
                    EffectConditionType.EnemyPlayedDamageCard => BuildNumericCondition("opponent.playedDamageCardCount", ">=", "<", 1, condition.Negate),
                    EffectConditionType.EnemyPlayedDefenseCard => BuildNumericCondition("opponent.playedDefenseCardCount", ">=", "<", 1, condition.Negate),
                    EffectConditionType.EnemyPlayedCounterCard => BuildNumericCondition("opponent.playedCounterCardCount", ">=", "<", 1, condition.Negate),
                    EffectConditionType.EnemyPlayedCardCountAtLeast => BuildNumericCondition("opponent.playedCardCount", ">=", "<", condition.ConditionValue, condition.Negate),
                    EffectConditionType.MyDeckIsEmpty => condition.Negate ? "self.deckCount != 0" : "self.deckCount == 0",
                    EffectConditionType.MyHandCardCountAtMost => BuildNumericCondition("self.handCount", "<=", ">", condition.ConditionValue, condition.Negate),
                    EffectConditionType.MyHandCardCountAtLeast => BuildNumericCondition("self.handCount", ">=", "<", condition.ConditionValue, condition.Negate),
                    EffectConditionType.MyPlayedCardCountAtLeast => BuildNumericCondition("self.playedCardCount", ">=", "<", condition.ConditionValue, condition.Negate),
                    EffectConditionType.MyHpPercentAtMost => BuildNumericCondition("self.hp", "<=", ">", $"{condition.ConditionValue}%", condition.Negate),
                    EffectConditionType.MyHpPercentAtLeast => BuildNumericCondition("self.hp", ">=", "<", $"{condition.ConditionValue}%", condition.Negate),
                    EffectConditionType.EnemyHpPercentAtMost => BuildNumericCondition("opponent.hp", "<=", ">", $"{condition.ConditionValue}%", condition.Negate),
                    EffectConditionType.EnemyIsStunned => BuildNumericCondition("opponent.isStunned", "==", "!=", 1, condition.Negate),
                    EffectConditionType.RoundNumberAtLeast => BuildNumericCondition("round", ">=", "<", condition.ConditionValue, condition.Negate),
                    _ => throw new InvalidOperationException($"当前 BattleCore 配置路径不支持条件 {condition.ConditionType}。"),
                };
            }

            private static string BuildNumericCondition(string lhs, string positiveOp, string negativeOp, int rhs, bool negate)
            {
                return BuildNumericCondition(lhs, positiveOp, negativeOp, rhs.ToString(), negate);
            }

            private static string BuildNumericCondition(string lhs, string positiveOp, string negativeOp, string rhs, bool negate)
            {
                return $"{lhs} {(negate ? negativeOp : positiveOp)} {rhs}";
            }

            private static Dictionary<string, string> BuildParams(CardEffect effect)
            {
                var parameters = new Dictionary<string, string>();

                if (effect.Duration > 0)
                    parameters["duration"] = effect.Duration.ToString();
                if (effect.EffectType == EffectType.Lifesteal)
                    parameters["percent"] = effect.Value.ToString();
                if (effect.EffectType == EffectType.AddBuff)
                    parameters["buffConfigId"] = effect.BuffConfigId;

                if (effect.EffectType == EffectType.GenerateCard)
                {
                    parameters["configId"] = effect.GenerateCardConfigId;
                    parameters["targetZone"] = string.IsNullOrWhiteSpace(effect.GenerateCardZone) ? "Hand" : effect.GenerateCardZone;
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
        }
    }
}
