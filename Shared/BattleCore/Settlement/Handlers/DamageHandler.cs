using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 伤害效果处理器 —— 处理 DealDamage 效果。
    /// 
    /// 职责：
    /// - 解析卡牌伤害效果的参数
    /// - 调用 DamageHelper 统一处理伤害流程
    /// 
    /// 注意：实际伤害计算和回调触发由 DamageHelper 负责
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
            ctx.RoundLog.Add($"[DamageHandler] 开始执行 - 来源:{source?.PlayerId}, 目标:{target?.PlayerId}, 伤害值:{effect.Value}");
            
            if (target == null)
            {
                ctx.RoundLog.Add("[DamageHandler] 目标为空，跳过伤害");
                return;
            }
            
            if (!target.IsAlive)
            {
                ctx.RoundLog.Add($"[DamageHandler] 目标 {target.PlayerId} 已死亡，跳过伤害");
                return;
            }
            
            if (target.IsMarkedForDeath)
            {
                ctx.RoundLog.Add($"[DamageHandler] 目标 {target.PlayerId} 已濒死，跳过伤害");
                return;
            }

            // 检查是否有穿透效果（无视护甲）
            bool hasPierce = HasPierceEffect(card);

            // 使用 DamageHelper 统一处理伤害
            int actualDamage = DamageHelper.ApplyDamage(
                ctx: ctx,
                sourceId: source.PlayerId,
                targetId: target.PlayerId,
                baseDamage: effect.Value,
                triggerCallbacks: true,
                ignoreArmor: hasPierce,
                damageSource: $"「{card.Config.CardName}」"
            );

            // 检查卡牌自带的吸血效果（不是 Buff 的吸血）
            if (actualDamage > 0 && HasCardLifestealEffect(card, out int lifestealPercent))
            {
                int healAmount = (actualDamage * lifestealPercent) / 100;
                if (healAmount > 0)
                {
                    DamageHelper.ApplyHeal(
                        ctx: ctx,
                        sourceId: source.PlayerId,
                        targetId: source.PlayerId,
                        healAmount: healAmount,
                        healSource: $"「{card.Config.CardName}」卡牌吸血"
                    );
                }
            }
        }

        /// <summary>
        /// 检查卡牌是否有穿透效果。
        /// </summary>
        private bool HasPierceEffect(PlayedCard card)
        {
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.Pierce)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查卡牌是否自带吸血效果（不是 Buff）。
        /// </summary>
        private bool HasCardLifestealEffect(PlayedCard card, out int lifestealPercent)
        {
            lifestealPercent = 0;
            foreach (var eff in card.Config.Effects)
            {
                if (eff.EffectType == EffectType.Lifesteal)
                {
                    lifestealPercent = eff.Value;
                    return true;
                }
            }
            return false;
        }
    }
}