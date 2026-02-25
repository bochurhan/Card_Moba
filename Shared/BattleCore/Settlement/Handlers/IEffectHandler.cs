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
}
