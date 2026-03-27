#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Rules.Play
{
    public sealed class PlayRuleResolver
    {
        public PlayRuleResolution Resolve(
            BattleContext ctx,
            string playerId,
            IReadOnlyList<EffectUnit> effects,
            PlayOrigin origin)
        {
            var resolution = new PlayRuleResolution();
            var player = ctx.GetPlayer(playerId);
            if (player == null)
            {
                resolution.Allowed = false;
                resolution.BlockReason = $"找不到玩家 {playerId}";
                return resolution;
            }

            if (ContainsDamageCardSemantics(effects)
                && ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, BuffType.NoDamageCardThisTurn))
            {
                resolution.Allowed = false;
                resolution.BlockReason = "本回合不能再打出伤害牌";
                return resolution;
            }

            if (origin == PlayOrigin.PlayerHandPlay
                && player.CorruptionFreePlaysRemainingThisRound > 0
                && ctx.BuffManager.HasBuffType(ctx, player.HeroEntity.EntityId, BuffType.Corruption))
            {
                resolution.CostSetTo = 0;
                resolution.ForceConsumeAfterResolve = true;
                resolution.ConsumeCorruptionChargeOnSuccess = true;
                resolution.HitCorruption = true;
            }

            return resolution;
        }

        public void ResetTurnState(BattleContext ctx)
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
                        $"[PlayRuleResolver] {player.PlayerId} 第 {ctx.CurrentRound} 回合腐化名额重置为 {corruptionValue}。");
                }
            }
        }

        public void CommitSuccessfulPlay(BattleContext ctx, string playerId, PlayRuleResolution resolution)
        {
            if (!resolution.ConsumeCorruptionChargeOnSuccess)
                return;

            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return;

            if (player.CorruptionFreePlaysRemainingThisRound <= 0)
                return;

            player.CorruptionFreePlaysRemainingThisRound--;
            ctx.RoundLog.Add(
                $"[PlayRuleResolver] {playerId} 消耗 1 次腐化名额，剩余 {player.CorruptionFreePlaysRemainingThisRound} 次。");
        }

        private static bool ContainsDamageCardSemantics(IReadOnlyList<EffectUnit> effects)
        {
            foreach (var effect in effects)
            {
                if (effect.Type == EffectType.Damage
                    || effect.Type == EffectType.Pierce
                    || effect.Type == EffectType.Lifesteal
                    || effect.Type == EffectType.Thorns
                    || effect.Type == EffectType.ArmorOnHit
                    || effect.Type == EffectType.DOT)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
