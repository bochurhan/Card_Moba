#pragma warning disable CS8632

using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Rules.Play;

namespace CardMoba.BattleCore.Costs
{
    /// <summary>
    /// 统一出牌费用解析器。
    /// 只负责读取当前有效配置与规则层给出的费用指令，计算最终费用。
    /// </summary>
    public sealed class PlayCostResolver
    {
        public PlayCostResolution Resolve(
            BattleContext ctx,
            string playerId,
            BattleCard card,
            PlayRuleResolution playRules)
        {
            var definition = ctx.GetEffectiveCardDefinition(card);
            int baseCost = definition?.EnergyCost ?? 0;
            int finalCost = playRules.CostSetTo ?? baseCost;

            if (finalCost < 0)
                finalCost = 0;

            return new PlayCostResolution
            {
                BaseCost = baseCost,
                FinalCost = finalCost,
            };
        }
    }
}
