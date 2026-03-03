using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 治疗效果处理器 —— 处理 Heal 效果。
    /// </summary>
    public class HealHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            // 治疗效果的目标默认是自己，也可以作用于指定目标
            var healTarget = target ?? source;

            if (!healTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[HealHandler] 目标玩家{healTarget.PlayerId}已死亡，治疗无效");
                return;
            }

            // 解析实际数值（支持 ValueSource 跨效果依赖，如等量伤害回血）
            int healAmount = EffectHandlerHelper.ResolveValue(effect, card, ctx);
            int oldHp = healTarget.Hp;

            healTarget.Hp += healAmount;
            if (healTarget.Hp > healTarget.MaxHp)
                healTarget.Hp = healTarget.MaxHp;

            int actualHeal = healTarget.Hp - oldHp;

            // 将实际回血量写入 EffectContext，供后续效果读取
            card.EffectContext["LastHealAmount"] = actualHeal;

            ctx.RoundLog.Add($"[HealHandler] 玩家{healTarget.PlayerId}回复{actualHeal}点生命（{oldHp} → {healTarget.Hp}）");
        }
    }
}
