using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 眩晕效果处理器 —— 处理 Stun 效果。
    /// </summary>
    public class StunHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive)
            {
                ctx.RoundLog.Add($"[StunHandler] 目标无效，眩晕失败");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            target.IsStunned = true;
            target.StunnedRounds = duration;

            ctx.RoundLog.Add($"[StunHandler] 玩家{target.PlayerId}被眩晕{duration}回合");
        }
    }
}
