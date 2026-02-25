using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;

namespace CardMoba.BattleCore.Trigger
{
    /// <summary>
    /// 触发器管理器 —— 管理战斗中所有跨回合的触发效果。
    /// 
    /// 职责：
    /// - 注册/注销触发器
    /// - 在指定时机执行所有匹配的触发器
    /// - 管理触发器生命周期（回合衰减、次数限制）
    /// - 按优先级排序执行
    /// 
    /// 使用场景：
    /// - 卡牌的"下回合开始时..."效果
    /// - Buff 的触发效果
    /// - 英雄被动技能
    /// - 遗物效果
    /// </summary>
    public class TriggerManager
    {
        private readonly Dictionary<TriggerTiming, List<TriggerInstance>> _triggersByTiming;
        private int _triggerIdCounter = 0;

        /// <summary>触发器执行事件（用于日志/UI 更新）</summary>
        public event Action<TriggerInstance, TriggerContext> OnTriggerExecuted;

        /// <summary>触发器移除事件</summary>
        public event Action<TriggerInstance> OnTriggerRemoved;

        public TriggerManager()
        {
            _triggersByTiming = new Dictionary<TriggerTiming, List<TriggerInstance>>();

            // 初始化所有时机的列表
            foreach (TriggerTiming timing in Enum.GetValues(typeof(TriggerTiming)))
            {
                if (timing != TriggerTiming.None)
                {
                    _triggersByTiming[timing] = new List<TriggerInstance>();
                }
            }
        }

        /// <summary>
        /// 注册一个触发器。
        /// </summary>
        /// <param name="trigger">触发器实例</param>
        /// <returns>分配的触发器 ID</returns>
        public string RegisterTrigger(TriggerInstance trigger)
        {
            if (trigger == null) return null;
            if (trigger.Timing == TriggerTiming.None) return null;

            // 分配 ID
            if (string.IsNullOrEmpty(trigger.TriggerId))
            {
                trigger.TriggerId = $"TRIGGER_{++_triggerIdCounter}";
            }

            // 添加到对应时机的列表
            if (_triggersByTiming.TryGetValue(trigger.Timing, out var list))
            {
                list.Add(trigger);
                // 按优先级排序（高优先级在前）
                list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }

            return trigger.TriggerId;
        }

        /// <summary>
        /// 创建并注册一个简单触发器。
        /// </summary>
        public string RegisterTrigger(
            TriggerTiming timing,
            string ownerPlayerId,
            Action<TriggerContext> effect,
            Func<TriggerContext, bool> condition = null,
            int priority = 0,
            int remainingTriggers = -1,
            int remainingRounds = -1,
            string sourceId = null,
            string triggerName = null)
        {
            var trigger = new TriggerInstance
            {
                Timing = timing,
                OwnerPlayerId = ownerPlayerId,
                Effect = effect,
                Condition = condition,
                Priority = priority,
                RemainingTriggers = remainingTriggers,
                RemainingRounds = remainingRounds,
                SourceId = sourceId ?? string.Empty,
                TriggerName = triggerName ?? $"Trigger_{timing}",
            };

            return RegisterTrigger(trigger);
        }

        /// <summary>
        /// 注销一个触发器。
        /// </summary>
        public bool UnregisterTrigger(string triggerId)
        {
            foreach (var list in _triggersByTiming.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].TriggerId == triggerId)
                    {
                        var trigger = list[i];
                        list.RemoveAt(i);
                        OnTriggerRemoved?.Invoke(trigger);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 注销指定来源的所有触发器。
        /// </summary>
        public int UnregisterTriggersBySource(string sourceId)
        {
            int count = 0;
            foreach (var list in _triggersByTiming.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].SourceId == sourceId)
                    {
                        OnTriggerRemoved?.Invoke(list[i]);
                        list.RemoveAt(i);
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 注销指定玩家的所有触发器。
        /// </summary>
        public int UnregisterTriggersByOwner(string ownerPlayerId)
        {
            int count = 0;
            foreach (var list in _triggersByTiming.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].OwnerPlayerId == ownerPlayerId)
                    {
                        OnTriggerRemoved?.Invoke(list[i]);
                        list.RemoveAt(i);
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 执行指定时机的所有触发器。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="timing">触发时机</param>
        /// <param name="sourcePlayerId">触发源玩家 ID（可选）</param>
        /// <param name="targetPlayerId">触发目标玩家 ID（可选）</param>
        /// <param name="value">相关数值（可选）</param>
        /// <param name="relatedCard">相关卡牌（可选）</param>
        /// <returns>是否有任何触发器执行</returns>
        public bool FireTriggers(
            BattleContext ctx,
            TriggerTiming timing,
            string sourcePlayerId = null,
            string targetPlayerId = null,
            int value = 0,
            PlayedCard relatedCard = null)
        {
            return FireTriggers(ctx, timing, sourcePlayerId, targetPlayerId, value, relatedCard, out _);
        }

        /// <summary>
        /// 执行指定时机的所有触发器，并返回是否被取消。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="timing">触发时机</param>
        /// <param name="sourcePlayerId">触发源玩家 ID（可选）</param>
        /// <param name="targetPlayerId">触发目标玩家 ID（可选）</param>
        /// <param name="value">相关数值（可选）</param>
        /// <param name="relatedCard">相关卡牌（可选）</param>
        /// <param name="cancelled">输出参数：是否有触发器取消了后续处理</param>
        /// <returns>是否有任何触发器执行</returns>
        public bool FireTriggers(
            BattleContext ctx,
            TriggerTiming timing,
            string sourcePlayerId,
            string targetPlayerId,
            int value,
            PlayedCard relatedCard,
            out bool cancelled)
        {
            cancelled = false;

            if (!_triggersByTiming.TryGetValue(timing, out var triggers))
                return false;

            if (triggers.Count == 0)
                return false;

            // 创建触发上下文
            var triggerCtx = new TriggerContext
            {
                BattleContext = ctx,
                Timing = timing,
                SourcePlayerId = sourcePlayerId,
                TargetPlayerId = targetPlayerId,
                Value = value,
                ModifiedValue = value,
                RelatedCard = relatedCard,
            };

            bool anyExecuted = false;

            // 复制列表以防止迭代时修改
            var triggersToExecute = new List<TriggerInstance>(triggers);

            foreach (var trigger in triggersToExecute)
            {
                // 检查是否应该移除
                if (trigger.ShouldRemove)
                    continue;

                // 检查条件
                if (trigger.Condition != null && !trigger.Condition(triggerCtx))
                    continue;

                // 执行效果
                try
                {
                    trigger.Effect?.Invoke(triggerCtx);
                    anyExecuted = true;

                    // 减少触发次数
                    if (trigger.RemainingTriggers > 0)
                    {
                        trigger.RemainingTriggers--;
                    }

                    OnTriggerExecuted?.Invoke(trigger, triggerCtx);

                    // 检查是否应该取消后续处理
                    if (triggerCtx.ShouldCancel)
                    {
                        cancelled = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但继续执行其他触发器
                    System.Diagnostics.Debug.WriteLine($"[TriggerManager] 触发器执行异常: {trigger.TriggerId} - {ex.Message}");
                }
            }

            // 清理应该移除的触发器
            CleanupExpiredTriggers(timing);

            return anyExecuted;
        }

        /// <summary>
        /// 执行触发器并返回修改后的数值（用于伤害/治疗等可修改的效果）。
        /// </summary>
        public int FireTriggersWithModifier(
            BattleContext ctx,
            TriggerTiming timing,
            string sourcePlayerId,
            string targetPlayerId,
            int value,
            out bool cancelled)
        {
            cancelled = false;

            if (!_triggersByTiming.TryGetValue(timing, out var triggers))
                return value;

            if (triggers.Count == 0)
                return value;

            var triggerCtx = new TriggerContext
            {
                BattleContext = ctx,
                Timing = timing,
                SourcePlayerId = sourcePlayerId,
                TargetPlayerId = targetPlayerId,
                Value = value,
                ModifiedValue = value,
            };

            var triggersToExecute = new List<TriggerInstance>(triggers);

            foreach (var trigger in triggersToExecute)
            {
                if (trigger.ShouldRemove) continue;
                if (trigger.Condition != null && !trigger.Condition(triggerCtx)) continue;

                try
                {
                    trigger.Effect?.Invoke(triggerCtx);

                    if (trigger.RemainingTriggers > 0)
                        trigger.RemainingTriggers--;

                    OnTriggerExecuted?.Invoke(trigger, triggerCtx);

                    if (triggerCtx.ShouldCancel)
                    {
                        cancelled = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TriggerManager] 触发器执行异常: {trigger.TriggerId} - {ex.Message}");
                }
            }

            CleanupExpiredTriggers(timing);

            return triggerCtx.ModifiedValue;
        }

        /// <summary>
        /// 回合结束时处理所有触发器的回合衰减。
        /// </summary>
        public void OnRoundEnd()
        {
            foreach (var kvp in _triggersByTiming)
            {
                var list = kvp.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var trigger = list[i];
                    if (trigger.RemainingRounds > 0)
                    {
                        trigger.RemainingRounds--;
                        if (trigger.RemainingRounds == 0)
                        {
                            trigger.IsMarkedForRemoval = true;
                        }
                    }
                }
            }

            // 清理所有过期触发器
            foreach (TriggerTiming timing in _triggersByTiming.Keys)
            {
                CleanupExpiredTriggers(timing);
            }
        }

        /// <summary>
        /// 获取指定玩家的所有触发器。
        /// </summary>
        public List<TriggerInstance> GetTriggersByOwner(string ownerPlayerId)
        {
            var result = new List<TriggerInstance>();
            foreach (var list in _triggersByTiming.Values)
            {
                foreach (var trigger in list)
                {
                    if (trigger.OwnerPlayerId == ownerPlayerId)
                        result.Add(trigger);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取指定时机的所有触发器。
        /// </summary>
        public List<TriggerInstance> GetTriggersByTiming(TriggerTiming timing)
        {
            if (_triggersByTiming.TryGetValue(timing, out var list))
                return new List<TriggerInstance>(list);
            return new List<TriggerInstance>();
        }

        /// <summary>
        /// 检查指定玩家是否有某时机的触发器。
        /// </summary>
        public bool HasTrigger(string ownerPlayerId, TriggerTiming timing)
        {
            if (!_triggersByTiming.TryGetValue(timing, out var list))
                return false;

            foreach (var trigger in list)
            {
                if (trigger.OwnerPlayerId == ownerPlayerId && !trigger.ShouldRemove)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 清除所有触发器。
        /// </summary>
        public void ClearAllTriggers()
        {
            foreach (var list in _triggersByTiming.Values)
            {
                foreach (var trigger in list)
                {
                    OnTriggerRemoved?.Invoke(trigger);
                }
                list.Clear();
            }
        }

        /// <summary>
        /// 获取当前触发器总数。
        /// </summary>
        public int GetTotalTriggerCount()
        {
            int count = 0;
            foreach (var list in _triggersByTiming.Values)
            {
                count += list.Count;
            }
            return count;
        }

        // ══════════════════════════════════════════════════════════
        // 私有方法
        // ══════════════════════════════════════════════════════════

        private void CleanupExpiredTriggers(TriggerTiming timing)
        {
            if (!_triggersByTiming.TryGetValue(timing, out var list))
                return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].ShouldRemove)
                {
                    OnTriggerRemoved?.Invoke(list[i]);
                    list.RemoveAt(i);
                }
            }
        }
    }
}
