
#pragma warning disable CS8632

using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Resolvers;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// TriggerManager —— ITriggerManager 的具体实现。
    ///
    /// 存储结构：按 TriggerTiming 分组，每组按 Priority 升序排列。
    /// Fire() 不直接执行效果，而是将满足条件的触发器效果推入 ctx.PendingQueue。
    /// </summary>
    public class TriggerManager : ITriggerManager
    {
        // 按时机分组存储触发器（触发时只遍历当前时机分组，O(k)）
        private readonly Dictionary<TriggerTiming, List<TriggerUnit>> _triggers
            = new Dictionary<TriggerTiming, List<TriggerUnit>>();

        private int _idCounter = 0;

        // 条件检查器（无状态，单例复用）
        private readonly ConditionChecker _conditionChecker = new ConditionChecker();

        // ──────────────────────────────────────────────────────────
        // 注册 / 注销
        // ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string Register(TriggerUnit trigger)
        {
            if (string.IsNullOrEmpty(trigger.TriggerId))
                trigger.TriggerId = $"tr_{++_idCounter:D6}";

            if (!_triggers.TryGetValue(trigger.Timing, out var list))
            {
                list = new List<TriggerUnit>();
                _triggers[trigger.Timing] = list;
            }

            list.Add(trigger);
            // 按优先级升序排列（数值小的先触发）
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            return trigger.TriggerId;
        }

        /// <inheritdoc/>
        public bool Unregister(string triggerId)
        {
            foreach (var kv in _triggers)
            {
                var removed = kv.Value.RemoveAll(t => t.TriggerId == triggerId);
                if (removed > 0) return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public int UnregisterBySourceId(string sourceId)
        {
            int total = 0;
            foreach (var kv in _triggers)
                total += kv.Value.RemoveAll(t => t.SourceId == sourceId);
            return total;
        }

        // ──────────────────────────────────────────────────────────
        // 触发
        // ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Fire(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx)
        {
            if (!_triggers.TryGetValue(timing, out var list) || list.Count == 0)
                return;

            // 遍历时使用快照，避免触发器在执行过程中修改列表
            var snapshot = list.ToList();
            foreach (var trigger in snapshot)
            {
                // RemainingTriggers 为 0 则跳过（已耗尽）
                if (trigger.RemainingTriggers == 0)
                    continue;

                // 条件检查（字符串条件列表，通过 ConditionChecker 评估）
                if (trigger.Conditions != null && trigger.Conditions.Count > 0)
                {
                    // 获取触发器归属者的英雄实体（用于条件中 self.hp 等引用）
                    var ownerData = ctx.GetPlayer(trigger.OwnerPlayerId);
                    if (ownerData == null) continue;
                    var ownerEntity = ownerData.HeroEntity;

                    if (!_conditionChecker.Check(trigger.Conditions, ctx, ownerEntity, triggerCtx))
                        continue;
                }

                // ── 分支 A：InlineExecute lambda（Buff 触发器专用，直接执行）──
                if (trigger.InlineExecute != null)
                {
                    trigger.InlineExecute(ctx, triggerCtx);
                }
                else
                {
                    // ── 分支 B：将 EffectUnit 推入 PendingQueue（常规卡牌触发器）──
                    // 修复：携带 triggerCtx 至 PendingEffectEntry，供条件检查和参数解析使用
                    var ownerPlayer = ctx.GetPlayer(trigger.OwnerPlayerId);
                    string sourceEntityId = ownerPlayer?.HeroEntity.EntityId ?? trigger.OwnerPlayerId;

                    // 将 TriggerContext 序列化为 Dictionary<string, object>
                    foreach (var effectUnit in trigger.Effects)
                    {
                        ctx.PendingQueue.Enqueue(new PendingEffectEntry
                        {
                            Effect           = effectUnit,
                            SourceEntityId   = sourceEntityId,
                            SourceTriggerId  = trigger.TriggerId,
                            TriggerContext   = CloneTriggerContext(triggerCtx),
                        });
                    }
                }

                // 衰减剩余触发次数（-1 = 无限）
                if (trigger.RemainingTriggers > 0)
                    trigger.RemainingTriggers--;
            }

            // 清理已耗尽触发次数的触发器
            list.RemoveAll(t => t.RemainingTriggers == 0);
        }

        // ──────────────────────────────────────────────────────────
        // 回合衰减
        // ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void TickDecay(BattleContext ctx)
        {
            foreach (var kv in _triggers)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var t = list[i];
                    if (t.RemainingRounds > 0)
                    {
                        t.RemainingRounds--;
                        if (t.RemainingRounds == 0)
                        {
                            list.RemoveAt(i);
                            ctx.RoundLog.Add($"[TriggerManager] 触发器 {t.TriggerId}（{t.TriggerName}）已到期，自动注销。");
                        }
                    }
                }
            }
        }

        private static TriggerContext CloneTriggerContext(TriggerContext triggerContext)
        {
            return new TriggerContext
            {
                SourceEntityId = triggerContext.SourceEntityId,
                TargetEntityId = triggerContext.TargetEntityId,
                Value          = triggerContext.Value,
                Round          = triggerContext.Round,
                Extra          = new Dictionary<string, object>(triggerContext.Extra),
            };
        }
    }
}
