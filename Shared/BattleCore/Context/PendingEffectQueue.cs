
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 待执行效果条目 —— 包含效果原子及其执行所需上下文。
    /// </summary>
    public class PendingEffectEntry
    {
        /// <summary>要执行的效果原子</summary>
        public EffectUnit Effect { get; set; } = new EffectUnit();

        /// <summary>施法者 Entity ID</summary>
        public string SourceEntityId { get; set; } = string.Empty;

        /// <summary>
        /// 已提前解析的目标 Entity ID 列表（可为空，若为空则由 TargetResolver 在执行时解析）。
        /// 当触发器在特定时机产生效果时，可能已经知道目标（如 AfterTakeDamage 知道攻击来源）。
        /// </summary>
        public List<string>? PreResolvedTargetIds { get; set; }

        /// <summary>此条目来源的触发器 ID（可为空，直接出牌触发的效果无此字段）</summary>
        public string? SourceTriggerId { get; set; }

        /// <summary>触发上下文数据（来自触发时机的附带数据，供条件检查和参数解析使用）</summary>
        public Dictionary<string, object> TriggerContext { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 延迟效果队列（PendingEffectQueue）—— V2 架构解耦核心。
    ///
    /// 设计目标：
    ///   触发器响应时，不直接调用 SettlementEngine（会造成递归/双向依赖），
    ///   而是将待执行的效果推入此队列。
    ///   SettlementEngine 在每次主结算完成后，调用 DrainPendingQueue 统一消化。
    ///
    /// 执行栈永远是平的：
    ///   Resolve(主效果) → Push(子效果) → [主效果栈帧结束]
    ///   while(queue > 0) Resolve(子效果) → 可能再 Push → [子效果栈帧结束]
    /// </summary>
    public class PendingEffectQueue
    {
        private readonly Queue<PendingEffectEntry> _queue = new Queue<PendingEffectEntry>();

        /// <summary>队列中待处理的条目数量</summary>
        public int Count => _queue.Count;

        /// <summary>推入一个待执行的效果条目</summary>
        public void Enqueue(PendingEffectEntry entry)
        {
            _queue.Enqueue(entry);
        }

        /// <summary>推入一个简单效果（指定施法者，目标由 TargetResolver 解析）</summary>
        public void Enqueue(EffectUnit effect, string sourceEntityId, string? sourceTriggerId = null)
        {
            _queue.Enqueue(new PendingEffectEntry
            {
                Effect           = effect,
                SourceEntityId   = sourceEntityId,
                SourceTriggerId  = sourceTriggerId,
            });
        }

        /// <summary>取出队列头部的条目（若队列为空则返回 null）</summary>
        public PendingEffectEntry? Dequeue()
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }

        /// <summary>清空队列（一般在战斗异常终止时使用）</summary>
        public void Clear()
        {
            _queue.Clear();
        }
    }
}
