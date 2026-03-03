using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 效果处理器接口 —— 定义单个效果类型的执行逻辑。
    /// 
    /// 每个实现类处理一种 EffectType，例如：
    /// - DamageHandler 处理 DealDamage
    /// - HealHandler 处理 Heal
    /// - StunHandler 处理 Stun
    /// 
    /// 设计原则：
    /// - 职责单一：每个 Handler 只处理一种效果
    /// - 无状态：Handler 本身不持有状态，所有状态都在 BattleContext 中
    /// - 确定性：相同输入必须产生相同输出
    /// </summary>
    public interface IEffectHandler
    {
        /// <summary>
        /// 执行效果。
        /// </summary>
        /// <param name="card">打出的卡牌（包含效果配置和目标信息）</param>
        /// <param name="effect">当前要执行的效果配置</param>
        /// <param name="source">效果来源玩家</param>
        /// <param name="target">效果目标玩家（可能为 null）</param>
        /// <param name="ctx">战斗上下文</param>
        void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx
        );
    }

    /// <summary>
    /// Handler 公共辅助方法 —— 所有 IEffectHandler 实现类均可调用。
    /// 静态类，无状态，无副作用。
    /// </summary>
    public static class EffectHandlerHelper
    {
        /// <summary>
        /// 解析效果的实际数值。
        ///
        /// 若 effect.ValueSource 非空，则从 card.EffectContext[ValueSource] 读取，
        /// 实现同一张牌内效果间的数值依赖（如「死亡收割」：回血 = 本次实际伤害）。
        /// 若 EffectContext 中不存在对应 Key，记录警告并回退到 effect.Value。
        ///
        /// 若 effect.ValueSource 为空，直接返回 effect.Value（默认行为，向后兼容）。
        /// </summary>
        /// <param name="effect">当前效果配置</param>
        /// <param name="card">当前打出的卡牌实例</param>
        /// <param name="ctx">战斗上下文（用于写入警告日志）</param>
        /// <returns>本次效果应使用的数值</returns>
        public static int ResolveValue(CardEffect effect, PlayedCard card, BattleContext ctx)
        {
            if (string.IsNullOrEmpty(effect.ValueSource))
                return effect.Value;

            if (card.EffectContext.TryGetValue(effect.ValueSource, out int contextValue))
            {
                ctx.RoundLog.Add(
                    $"[EffectHandlerHelper] 效果 {effect.EffectType} 从 EffectContext[{effect.ValueSource}] 读取数值: {contextValue}");
                return contextValue;
            }

            ctx.RoundLog.Add(
                $"[Warning][EffectHandlerHelper] EffectContext 中不存在 Key \"{effect.ValueSource}\"，" +
                $"效果 {effect.EffectType} 回退使用 Value={effect.Value}");
            return effect.Value;
        }
    }
}
