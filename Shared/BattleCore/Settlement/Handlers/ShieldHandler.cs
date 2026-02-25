using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 护盾效果处理器 —— 处理 GainShield 效果。
    /// </summary>
    public class ShieldHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            // 护盾效果的目标默认是自己
            var shieldTarget = target ?? source;

            if (!shieldTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[ShieldHandler] 目标玩家{shieldTarget.PlayerId}已死亡，护盾无效");
                return;
            }

            int shieldAmount = effect.Value;
            shieldTarget.Shield += shieldAmount;

            ctx.RoundLog.Add($"[ShieldHandler] 玩家{shieldTarget.PlayerId}获得{shieldAmount}点护盾（当前护盾：{shieldTarget.Shield}）");
        }
    }
}
