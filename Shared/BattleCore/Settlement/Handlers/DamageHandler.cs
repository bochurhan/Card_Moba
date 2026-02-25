using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 伤害效果处理器 —— 处理 DealDamage 效果。
    /// 
    /// 结算流程：
    /// 1. 计算来源的输出伤害（基础 + 力量 - 虚弱）
    /// 2. 计算目标的实际受伤（易伤 - 护甲 - 减伤）
    /// 3. 优先扣护盾，再扣血
    /// 4. 记录伤害统计（用于触发式效果）
    /// 5. 检查濒死状态
    /// </summary>
    public class DamageHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive || target.IsMarkedForDeath)
                return;

            // 1. 计算来源输出伤害
            int baseDamage = effect.Value;
            int outgoingDamage = source.CalculateOutgoingDamage(baseDamage);

            // 2. 计算目标实际受伤
            bool hasPierce = HasPierceEffect(card);
            int actualDamage = target.CalculateIncomingDamage(outgoingDamage, hasPierce);

            // 3. 优先扣护盾
            int shieldAbsorbed = 0;
            if (target.Shield > 0)
            {
                shieldAbsorbed = target.Shield >= actualDamage ? actualDamage : target.Shield;
                target.Shield -= shieldAbsorbed;
                actualDamage -= shieldAbsorbed;

                if (shieldAbsorbed > 0)
                {
                    ctx.RoundLog.Add($"[DamageHandler] 「{card.Config.CardName}」被护盾吸收{shieldAbsorbed}点伤害");
                }
            }

            // 4. 扣血并记录统计
            if (actualDamage > 0)
            {
                target.Hp -= actualDamage;
                target.DamageTakenThisRound += actualDamage;
                source.DamageDealtThisRound += actualDamage;

                ctx.RoundLog.Add($"[DamageHandler] 玩家{source.PlayerId}对玩家{target.PlayerId}造成{actualDamage}点伤害");

                // 检查是否有吸血效果
                if (HasLifestealEffect(card))
                {
                    ctx.PendingTriggerEffects.Add(new PendingTriggerEffect
                    {
                        SourcePlayerId = source.PlayerId,
                        TargetPlayerId = source.PlayerId,
                        EffectType = EffectType.Lifesteal,
                        Value = actualDamage,
                        TriggerReason = $"「{card.Config.CardName}」吸血"
                    });
                }

                // 检查反伤效果
                if (HasThornsEffect(target))
                {
                    int thornsValue = GetThornsValue(target);
                    ctx.PendingTriggerEffects.Add(new PendingTriggerEffect
                    {
                        SourcePlayerId = target.PlayerId,
                        TargetPlayerId = source.PlayerId,
                        EffectType = EffectType.Thorns,
                        Value = thornsValue,
                        TriggerReason = $"玩家{target.PlayerId}的反伤"
                    });
                }
            }

            // 5. 检查濒死
            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.IsMarkedForDeath = true;
                source.HasKilledThisRound = true;
                ctx.RoundLog.Add($"[DamageHandler] 玩家{target.PlayerId}进入濒死状态");
            }
        }

        private bool HasPierceEffect(PlayedCard card)
        {
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.Pierce)
                    return true;
            }
            return false;
        }

        private bool HasLifestealEffect(PlayedCard card)
        {
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.Lifesteal)
                    return true;
            }
            return false;
        }

        private bool HasThornsEffect(PlayerBattleState player)
        {
            foreach (var buff in player.ActiveBuffs)
            {
                if (buff.BuffId.StartsWith("thorns"))
                    return true;
            }
            return false;
        }

        private int GetThornsValue(PlayerBattleState player)
        {
            foreach (var buff in player.ActiveBuffs)
            {
                if (buff.BuffId.StartsWith("thorns"))
                    return buff.Value;
            }
            return 0;
        }
    }
}
