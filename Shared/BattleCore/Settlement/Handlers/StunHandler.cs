using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 眩晕效果处理器 —— 处理 Stun 效果。
    ///
    /// 通过 BuffManager.AddBuff() 施加眩晕状态，由 BuffManager 统一管理
    /// IsStunned 字段的写入和回合衰减，不直接操作 PlayerBattleState。
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

            // 确定叠加规则：优先使用 CardEffect 配置，否则默认刷新持续时间
            var stackRule = effect.AppliesBuff
                ? effect.BuffStackRule
                : BuffStackRule.RefreshDuration;

            var buffInstance = new BuffInstance
            {
                BuffId    = $"stun_{source.PlayerId}",
                BuffType  = BuffType.Stun,
                SourceId  = source.PlayerId,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = stackRule,
                Stacks    = 1,
                Value     = 0,
            };

            var mgr = ctx.GetBuffManager(target.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[StunHandler] 找不到玩家{target.PlayerId}的BuffManager，眩晕失败");
                return;
            }

            mgr.AddBuff(buffInstance);
            ctx.RoundLog.Add($"[StunHandler] 玩家{target.PlayerId}被眩晕{duration}回合（来源：{source.PlayerId}）");
        }
    }
}