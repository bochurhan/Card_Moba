
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Handlers
{
    /// <summary>
    /// 效果 Handler 接口（V2）—— 所有 Handler 必须实现此接口。
    ///
    /// Handler 设计原则：
    ///   1. 无状态单例：Handler 禁止存储私有实例字段，所有状态写入 BattleContext
    ///   2. 单一职责：每个 Handler 只处理一种 EffectType
    ///   3. 不直接调用触发器：伤害/治疗发生后的副效果通过推入 PendingQueue 实现
    /// </summary>
    public interface IEffectHandler
    {
        /// <summary>
        /// 执行效果原子。
        /// </summary>
        /// <param name="ctx">战斗上下文（唯一状态写入入口）</param>
        /// <param name="effect">效果原子数据（只读）</param>
        /// <param name="source">施法者实体</param>
        /// <param name="targets">已解析的目标实体列表</param>
        /// <param name="priorResults">同一张卡前置效果的执行结果（供动态参数引用，如死亡收割读取实际伤害量）</param>
        /// <returns>本次效果的执行结果（供后续效果通过 DynamicParamResolver 引用）</returns>
        EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext);
    }
}
