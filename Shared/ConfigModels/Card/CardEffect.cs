using System.Collections.Generic;
using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 单个卡牌效果的配置语义模型。
    /// 只保留当前 BattleCore 主链路已经消费的字段。
    /// </summary>
    public class CardEffect
    {
        /// <summary>效果类型，决定运行时处理方式和默认结算层。</summary>
        public EffectType EffectType { get; set; }

        /// <summary>静态数值。未配置 ValueExpression 时使用。</summary>
        public int Value { get; set; }

        /// <summary>动态数值表达式。非空时优先于 Value。</summary>
        public string ValueExpression { get; set; } = string.Empty;

        /// <summary>AddBuff 使用的 Buff 配置 ID。</summary>
        public string BuffConfigId { get; set; } = string.Empty;

        /// <summary>GenerateCard 使用的目标卡牌配置 ID。</summary>
        public string GenerateCardConfigId { get; set; } = string.Empty;

        /// <summary>GenerateCard 的目标区位。</summary>
        public string GenerateCardZone { get; set; } = "Hand";

        /// <summary>GenerateCard 生成的卡是否在回合末销毁。</summary>
        public bool GenerateCardIsTemp { get; set; }

        /// <summary>UpgradeCardsInHand 使用的投影持续时间。</summary>
        public string ProjectionLifetime { get; set; } = string.Empty;

        /// <summary>效果持续回合数。0 表示即时。</summary>
        public int Duration { get; set; }

        /// <summary>重复执行次数，默认 1。</summary>
        public int RepeatCount { get; set; } = 1;

        /// <summary>效果目标覆盖。为空时沿用卡牌默认目标。</summary>
        public CardTargetType? TargetOverride { get; set; }

        /// <summary>效果条件列表，全部满足时才执行。</summary>
        public List<EffectCondition> EffectConditions { get; set; } = new();

        /// <summary>同层主优先级，数值越小越先执行。</summary>
        public int Priority { get; set; } = 500;

        /// <summary>同优先级内次级排序键。</summary>
        public int SubPriority { get; set; }

        /// <summary>获取该效果的默认结算层。</summary>
        public int GetSettlementLayer()
        {
            return EffectType switch
            {
                EffectType.Counter => 0,

                EffectType.Shield => 1,
                EffectType.Armor => 1,
                EffectType.AttackBuff => 1,
                EffectType.AttackDebuff => 1,
                EffectType.Reflect => 1,
                EffectType.DamageReduction => 1,
                EffectType.Invincible => 1,

                EffectType.Damage => 2,
                EffectType.Lifesteal => 2,
                EffectType.Thorns => 2,
                EffectType.ArmorOnHit => 2,
                EffectType.Pierce => 2,
                EffectType.DOT => 2,

                EffectType.Draw => 3,
                EffectType.Discard => 3,
                EffectType.GainEnergy => 3,
                EffectType.GenerateCard => 3,
                EffectType.ReturnSourceCardToHandAtRoundEnd => 3,
                EffectType.MoveSelectedCardToDeckTop => 3,
                EffectType.UpgradeCardsInHand => 4,

                _ => 4
            };
        }

        /// <summary>判断该效果是否属于触发型派生效果。</summary>
        public bool IsTriggerEffect()
        {
            return EffectType == EffectType.Lifesteal
                || EffectType == EffectType.Thorns
                || EffectType == EffectType.ArmorOnHit;
        }
    }

    /// <summary>
    /// 运行时可检查的效果条件。
    /// </summary>
    public class EffectCondition
    {
        public EffectConditionType ConditionType { get; set; }

        public int ConditionValue { get; set; }

        public BuffType ConditionBuffType { get; set; }

        public bool Negate { get; set; }

        public override string ToString()
        {
            string desc = ConditionType switch
            {
                EffectConditionType.EnemyPlayedDamageCard => "敌方打出了伤害牌",
                EffectConditionType.EnemyPlayedDefenseCard => "敌方打出了防御牌",
                EffectConditionType.EnemyPlayedCounterCard => "敌方打出了反制牌",
                EffectConditionType.MyDeckIsEmpty => "我方牌库为空",
                EffectConditionType.MyHandCardCountAtMost => $"手牌 <= {ConditionValue}",
                EffectConditionType.MyHandCardCountAtLeast => $"手牌 >= {ConditionValue}",
                EffectConditionType.MyHpPercentAtMost => $"我方血量 <= {ConditionValue}%",
                EffectConditionType.EnemyHpPercentAtMost => $"敌方血量 <= {ConditionValue}%",
                EffectConditionType.EnemyIsStunned => "敌方处于眩晕",
                EffectConditionType.MyHasBuffType => $"我方拥有 {ConditionBuffType}",
                EffectConditionType.EnemyHasBuffType => $"敌方拥有 {ConditionBuffType}",
                _ => ConditionType.ToString()
            };

            return Negate ? $"NOT ({desc})" : desc;
        }
    }
}
