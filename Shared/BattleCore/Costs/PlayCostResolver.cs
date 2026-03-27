#pragma warning disable CS8632

using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Costs
{
    /// <summary>
    /// 统一出牌费用解析器。
    /// 负责读取当前有效配置、规则型 Buff 与玩家回合态，
    /// 计算最终费用以及是否命中额外规则副作用（例如腐化后的强制 Consume）。
    /// </summary>
    public sealed class PlayCostResolver
    {
        public PlayCostResolution Resolve(
            BattleContext ctx,
            string playerId,
            BattleCard card)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return new PlayCostResolution();

            var definition = ctx.GetEffectiveCardDefinition(card);
            int baseCost = definition?.EnergyCost ?? 0;
            int finalCost = baseCost;
            bool hitCorruption = false;

            if (player.CorruptionFreePlaysRemainingThisRound > 0
                && ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, BuffType.Corruption))
            {
                finalCost = 0;
                hitCorruption = true;
            }

            if (finalCost < 0)
                finalCost = 0;

            return new PlayCostResolution
            {
                BaseCost = baseCost,
                FinalCost = finalCost,
                HitCorruption = hitCorruption,
                ConsumesCorruptionCharge = hitCorruption,
                ForceConsumeAfterResolve = hitCorruption,
            };
        }

        public void Commit(
            BattleContext ctx,
            string playerId,
            PlayCostResolution resolution)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return;

            if (resolution.ConsumesCorruptionCharge && player.CorruptionFreePlaysRemainingThisRound > 0)
            {
                player.CorruptionFreePlaysRemainingThisRound--;
                ctx.RoundLog.Add(
                    $"[PlayCostResolver] {playerId} 消耗1次腐化名额，剩余 {player.CorruptionFreePlaysRemainingThisRound} 次。");
            }
        }

        public void ResetTurnRuleState(BattleContext ctx)
        {
            foreach (var player in ctx.AllPlayers.Values)
            {
                int corruptionValue = 0;
                var buffs = ctx.BuffManager.GetBuffs(player.HeroEntity.EntityId);
                foreach (var buff in buffs)
                {
                    if (buff.BuffType == BuffType.Corruption)
                        corruptionValue += buff.Value;
                }

                player.CorruptionFreePlaysRemainingThisRound = corruptionValue;
                if (corruptionValue > 0)
                {
                    ctx.RoundLog.Add(
                        $"[PlayCostResolver] {player.PlayerId} 第 {ctx.CurrentRound} 回合腐化名额重置为 {corruptionValue}。");
                }
            }
        }
    }
}
