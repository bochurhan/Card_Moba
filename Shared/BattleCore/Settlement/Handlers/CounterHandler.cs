using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 反制效果处理器 —— 处理堆叠0层的反制牌。
    /// 
    /// 反制牌特点：
    /// - 本回合打出锁定，下回合堆叠0层触发
    /// - 不需要玩家选择目标，由 Handler 内部查找符合条件的卡牌
    /// - 不走 TargetResolver，目标是"卡牌"而非"玩家"
    /// 
    /// 支持的反制类型：
    /// - CounterCard: 反制指定类型的卡牌
    /// - CounterFirstDamage: 反制首张伤害牌
    /// - CounterAndReflect: 反制并反弹效果
    /// </summary>
    public class CounterHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            // 反制牌的目标不是玩家，而是卡牌
            // 根据效果类型查找要反制的目标卡牌
            var targetCards = FindCounterableCards(card, effect, source, ctx);

            if (targetCards.Count == 0)
            {
                ctx.RoundLog.Add($"[CounterHandler] 玩家{source.PlayerId}的反制牌「{card.Config.CardName}」未找到可反制目标，效果落空");
                return;
            }

            foreach (var targetCard in targetCards)
            {
                // 标记目标卡牌为已反制
                targetCard.IsCountered = true;
                ctx.CounteredCards.Add(targetCard);

                ctx.RoundLog.Add($"[CounterHandler] 玩家{source.PlayerId}的「{card.Config.CardName}」成功反制了「{targetCard.Config.CardName}」！");

                // 处理额外效果（如反弹）
                ApplyCounterEffects(card, effect, source, targetCard, ctx);
            }
        }

        /// <summary>
        /// 查找符合反制条件的目标卡牌。
        /// </summary>
        private List<PlayedCard> FindCounterableCards(
            PlayedCard counterCard,
            CardEffect effect,
            PlayerBattleState source,
            BattleContext ctx)
        {
            var result = new List<PlayedCard>();

            // 获取敌方队伍ID
            int enemyTeamId = source.TeamId == 0 ? 1 : 0;

            switch (effect.EffectType)
            {
                case EffectType.CounterFirstDamage:
                    // 反制敌方首张伤害牌
                    var firstDamageCard = FindFirstDamageCard(ctx, enemyTeamId, source.LaneIndex);
                    if (firstDamageCard != null)
                    {
                        result.Add(firstDamageCard);
                    }
                    break;

                case EffectType.CounterCard:
                    // 反制指定类型的卡牌（根据配置中的条件）
                    var matchingCards = FindMatchingCards(ctx, enemyTeamId, source.LaneIndex, effect);
                    result.AddRange(matchingCards);
                    break;

                case EffectType.CounterAndReflect:
                    // 反制并反弹 —— 通常反制首张伤害牌
                    var targetCard = FindFirstDamageCard(ctx, enemyTeamId, source.LaneIndex);
                    if (targetCard != null)
                    {
                        result.Add(targetCard);
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// 查找敌方在指定分路的首张伤害牌。
        /// </summary>
        private PlayedCard? FindFirstDamageCard(BattleContext ctx, int enemyTeamId, int laneIndex)
        {
            foreach (var card in ctx.PendingPlanCards)
            {
                // 已被反制的跳过
                if (card.IsCountered) continue;

                // 检查是否是敌方的牌
                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner == null || cardOwner.TeamId != enemyTeamId) continue;

                // 分路期只能反制同路的牌
                if (ctx.MatchPhase == MatchPhase.LanePhase1 || ctx.MatchPhase == MatchPhase.LanePhase2)
                {
                    if (card.LaneIndex != laneIndex) continue;
                }

                // 检查是否是伤害牌
                if (card.Config.HasTag(CardTag.Damage))
                {
                    return card;
                }

                // 或者检查效果列表中是否有伤害效果
                foreach (var eff in card.Config.Effects)
                {
                    if (eff.EffectType == EffectType.DealDamage)
                    {
                        return card;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 查找符合条件的所有卡牌（用于通用反制）。
        /// </summary>
        private List<PlayedCard> FindMatchingCards(
            BattleContext ctx,
            int enemyTeamId,
            int laneIndex,
            CardEffect effect)
        {
            var result = new List<PlayedCard>();

            // 根据 TriggerCondition 解析反制条件
            // 格式示例: "tag:Damage" 或 "layer:DamageTrigger"
            var condition = effect.TriggerCondition;

            foreach (var card in ctx.PendingPlanCards)
            {
                if (card.IsCountered) continue;

                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner == null || cardOwner.TeamId != enemyTeamId) continue;

                // 分路期限制
                if (ctx.MatchPhase == MatchPhase.LanePhase1 || ctx.MatchPhase == MatchPhase.LanePhase2)
                {
                    if (card.LaneIndex != laneIndex) continue;
                }

                // 检查条件匹配
                if (MatchesCondition(card, condition))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查卡牌是否匹配反制条件。
        /// </summary>
        private bool MatchesCondition(PlayedCard card, string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                // 无条件 = 反制所有牌（通常不会这样配置）
                return true;
            }

            // 解析条件格式: "tag:XXX" 或 "layer:XXX"
            var parts = condition.Split(':');
            if (parts.Length != 2) return false;

            string condType = parts[0].ToLower();
            string condValue = parts[1];

            switch (condType)
            {
                case "tag":
                    // 检查卡牌是否有指定标签
                    if (System.Enum.TryParse<CardTag>(condValue, out var tag))
                    {
                        return card.Config.HasTag(tag);
                    }
                    break;

                case "layer":
                    // 检查卡牌是否属于指定结算层
                    if (System.Enum.TryParse<SettlementLayer>(condValue, out var layer))
                    {
                        return card.Config.Layer == layer;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// 应用反制牌的额外效果（如反弹伤害）。
        /// </summary>
        private void ApplyCounterEffects(
            PlayedCard counterCard,
            CardEffect effect,
            PlayerBattleState source,
            PlayedCard targetCard,
            BattleContext ctx)
        {
            if (effect.EffectType == EffectType.CounterAndReflect)
            {
                // 反弹伤害给原攻击者
                var attacker = ctx.GetPlayer(targetCard.SourcePlayerId);
                if (attacker != null && attacker.IsAlive)
                {
                    // 获取目标卡牌的伤害值
                    int reflectDamage = GetCardDamageValue(targetCard);

                    if (reflectDamage > 0)
                    {
                        attacker.Hp -= reflectDamage;
                        if (attacker.Hp < 0) attacker.Hp = 0;

                        ctx.RoundLog.Add($"[CounterHandler] 反弹{reflectDamage}点伤害给玩家{attacker.PlayerId}");

                        if (attacker.Hp <= 0)
                        {
                            attacker.IsMarkedForDeath = true;
                            ctx.RoundLog.Add($"[CounterHandler] 玩家{attacker.PlayerId}因反弹伤害进入濒死状态");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取卡牌的伤害值。
        /// </summary>
        private int GetCardDamageValue(PlayedCard card)
        {
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.DealDamage)
                {
                    return eff.Value;
                }
            }
            return card.Config.EffectValue;
        }
    }
}
