using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 反制效果处理器 —— 处理堆叠0层的反制牌（EffectType.Counter）。
    ///
    /// 反制牌特点：
    ///   - 本回合打出锁定，下回合堆叠0层触发
    ///   - 不走 TargetResolver，目标是"卡牌"而非"玩家"
    ///
    /// 反制变体由 CardEffect.Params 或 TriggerCondition 区分：
    ///   - TriggerCondition 为空     → 反制敌方首张伤害牌
    ///   - TriggerCondition = "tag:X" → 反制含有指定标签的牌
    ///   - TriggerCondition = "layer:X" → 反制属于指定结算层的牌
    ///
    /// 是否附带反弹效果由卡牌是否带有 CardTag.Reflect 标签决定。
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
            var targetCards = FindCounterableCards(card, effect, source, ctx);

            if (targetCards.Count == 0)
            {
                ctx.RoundLog.Add($"[CounterHandler] 玩家{source.PlayerId}的反制牌「{card.Config.CardName}」未找到可反制目标，效果落空");
                return;
            }

            foreach (var targetCard in targetCards)
            {
                targetCard.IsCountered = true;
                ctx.CounteredCards.Add(targetCard);

                ctx.RoundLog.Add($"[CounterHandler] 玩家{source.PlayerId}的「{card.Config.CardName}」成功反制了「{targetCard.Config.CardName}」！");

                // 检查是否有反弹标签，决定是否反弹伤害
                if (card.Config.HasTag(CardTag.Reflect))
                {
                    ApplyReflectDamage(source, targetCard, ctx);
                }
            }
        }

        /// <summary>
        /// 查找符合反制条件的目标卡牌。
        /// 条件由 TriggerCondition 字段解析，空字符串默认反制首张伤害牌。
        /// </summary>
        private List<PlayedCard> FindCounterableCards(
            PlayedCard counterCard,
            CardEffect effect,
            PlayerBattleState source,
            BattleContext ctx)
        {
            var result = new List<PlayedCard>();
            int enemyTeamId = source.TeamId == 0 ? 1 : 0;

            string condition = effect.TriggerCondition;

            if (string.IsNullOrEmpty(condition))
            {
                // 默认：反制敌方首张伤害牌
                var firstDamageCard = FindFirstDamageCard(ctx, enemyTeamId, source.LaneIndex);
                if (firstDamageCard != null)
                    result.Add(firstDamageCard);
            }
            else
            {
                // 按条件筛选
                var matchingCards = FindMatchingCards(ctx, enemyTeamId, source.LaneIndex, condition);
                result.AddRange(matchingCards);
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
                if (card.IsCountered) continue;

                var cardOwner = ctx.GetPlayer(card.SourcePlayerId);
                if (cardOwner == null || cardOwner.TeamId != enemyTeamId) continue;

                // 分路期只能反制同路的牌
                if (ctx.MatchPhase == MatchPhase.LanePhase1 || ctx.MatchPhase == MatchPhase.LanePhase2)
                {
                    if (card.LaneIndex != laneIndex) continue;
                }

                // 检查是否是伤害牌（标签或效果列表）
                if (card.Config.HasTag(CardTag.Damage))
                    return card;

                foreach (var eff in card.Config.Effects)
                {
                    if (eff.EffectType == EffectType.Damage)
                        return card;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找符合条件的所有卡牌（用于通用反制）。
        /// 条件格式："tag:XXX" 或 "layer:XXX"
        /// </summary>
        private List<PlayedCard> FindMatchingCards(
            BattleContext ctx,
            int enemyTeamId,
            int laneIndex,
            string condition)
        {
            var result = new List<PlayedCard>();

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

                if (MatchesCondition(card, condition))
                    result.Add(card);
            }

            return result;
        }

        /// <summary>
        /// 检查卡牌是否匹配反制条件。
        /// </summary>
        private bool MatchesCondition(PlayedCard card, string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return true;

            // 解析条件格式: "tag:XXX" 或 "layer:XXX"
            var parts = condition.Split(':');
            if (parts.Length != 2) return false;

            string condType = parts[0].ToLower();
            string condValue = parts[1];

            switch (condType)
            {
                case "tag":
                    if (System.Enum.TryParse<CardTag>(condValue, out var tag))
                        return card.Config.HasTag(tag);
                    break;

                case "layer":
                    if (System.Enum.TryParse<SettlementLayer>(condValue, out var layer))
                        return card.Config.Layer == layer;
                    break;
            }

            return false;
        }

        /// <summary>
        /// 反弹目标卡牌的伤害值给原攻击者（用于带反弹标签的反制牌）。
        /// </summary>
        private void ApplyReflectDamage(
            PlayerBattleState counterSource,
            PlayedCard targetCard,
            BattleContext ctx)
        {
            var attacker = ctx.GetPlayer(targetCard.SourcePlayerId);
            if (attacker == null || !attacker.IsAlive) return;

            int reflectDamage = GetCardDamageValue(targetCard);
            if (reflectDamage <= 0) return;

            attacker.Hp -= reflectDamage;
            if (attacker.Hp < 0) attacker.Hp = 0;

            ctx.RoundLog.Add($"[CounterHandler] 反弹{reflectDamage}点伤害给玩家{attacker.PlayerId}");

            if (attacker.Hp <= 0)
            {
                attacker.IsMarkedForDeath = true;
                ctx.RoundLog.Add($"[CounterHandler] 玩家{attacker.PlayerId}因反弹伤害进入濒死状态");
            }
        }

        /// <summary>
        /// 获取卡牌的伤害值（从 Damage 效果或 EffectValue 字段读取）。
        /// </summary>
        private int GetCardDamageValue(PlayedCard card)
        {
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.Damage)
                    return eff.Value;
            }
            return card.Config.EffectValue;
        }
    }
}