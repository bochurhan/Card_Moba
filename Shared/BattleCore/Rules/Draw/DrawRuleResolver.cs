#pragma warning disable CS8632

using CardMoba.BattleCore.Context;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Rules.Draw
{
    public sealed class DrawRuleResolver
    {
        public DrawRuleResolution Resolve(BattleContext ctx, string playerId)
        {
            var resolution = new DrawRuleResolution();
            var player = ctx.GetPlayer(playerId);
            if (player == null)
            {
                resolution.Allowed = false;
                resolution.BlockReason = $"找不到玩家 {playerId}";
                return resolution;
            }

            if (ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, BuffType.NoDrawThisTurn))
            {
                resolution.Allowed = false;
                resolution.BlockReason = "本回合不能抽牌";
            }

            return resolution;
        }
    }
}
