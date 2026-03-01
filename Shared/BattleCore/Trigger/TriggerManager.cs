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
        /// <summary>
        /// 最大触发链深度（防止 Thorns ↔ Thorns、反制 ↔ 反制等死递归）。
        /// 超过此深度时，后续触发器将被跳过并记录警告日志。
        /// </summary>
        private const int MaxTriggerDepth = 8;

        private readonly Dictionary<TriggerTiming, List<TriggerInstance>> _triggersByTiming;
        private int _triggerIdCounter = 0;

        /// <summary>当前触发链深度（用于递归保护）</summary>
        private int _triggerDepth = 0;

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
                // 按优先级排序：Priority 小的在前（先执行），Priority 相同时 SubPriority 小的在前
                list.Sort((a, b) =>
                {
                    int cmp = a.Priority.CompareTo(b.Priority);
                    return cmp != 0 ? cmp : a.SubPriority.CompareTo(b.SubPriority);
                });
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
        /// 执行指定时机的所有触发器（使用预构建的 TriggerContext）。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="timing">触发时机</param>
        /// <param name="triggerCtx">预构建的触发上下文</param>
        /// <returns>是否有任何触发器执行</returns>
        public bool FireTriggers(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx)
        {
            if (!_triggersByTiming.TryGetValue(timing, out var triggers))
                return false;

            if (triggers.Count == 0)
                return false;

            // ── 递归深度保护 ──
            if (_triggerDepth >= MaxTriggerDepth)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TriggerManager] ⚠️ 触发链超过最大深度({MaxTriggerDepth})，" +
                    $"跳过 {timing} 触发器，防止死递归。");
                return false;
            }

            _triggerDepth++;
            bool anyExecuted = false;

            try
            {
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
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但继续执行其他触发器
                        System.Diagnostics.Debug.WriteLine($"[TriggerManager] 触发器执行异常: {trigger.TriggerId} - {ex.Message}");
                    }
                }
            }
            finally
            {
                _triggerDepth--;
            }

            // 清理应该移除的触发器
            CleanupExpiredTriggers(timing);

            return anyExecuted;
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

            // ── 递归深度保护 ──
            if (_triggerDepth >= MaxTriggerDepth)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TriggerManager] ⚠️ 触发链超过最大深度({MaxTriggerDepth})，" +
                    $"跳过 {timing} 触发器，防止死递归。");
                return false;
            }

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

            _triggerDepth++;
            bool anyExecuted = false;

            try
            {
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
            }
            finally
            {
                _triggerDepth--;
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

            // ── 递归深度保护 ──
            if (_triggerDepth >= MaxTriggerDepth)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TriggerManager] ⚠️ 触发链超过最大深度({MaxTriggerDepth})，" +
                    $"跳过 {timing} 修改型触发器，防止死递归。");
                return value;
            }

            var triggerCtx = new TriggerContext
            {
                BattleContext = ctx,
                Timing = timing,
                SourcePlayerId = sourcePlayerId,
                TargetPlayerId = targetPlayerId,
                Value = value,
                ModifiedValue = value,
            };

            _triggerDepth++;

            try
            {
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
            }
            finally
            {
                _triggerDepth--;
            }

            CleanupExpiredTriggers(timing);

            return triggerCtx.ModifiedValue;
        }

        /// <summary>
        /// 回合结束时处理触发器的回合衰减。
        ///
        /// 单一所有权规则（R-03）：
        ///   - 有 SourceId 的触发器（即 Buff 归属触发器）生命周期完全由 BuffManager 管理，
        ///     此处跳过衰减，防止触发器与 Buff 生命周期出现双重计数不同步的问题。
        ///   - 没有 SourceId 的触发器（卡牌/遗物等一次性触发器）才在此处做轮次衰减。
        /// </summary>
        public void OnRoundEnd()
        {
            foreach (var kvp in _triggersByTiming)
            {
                var list = kvp.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var trigger = list[i];

                    // ── 单一所有权：Buff 归属触发器跳过轮次衰减，由 BuffManager 统一管理 ──
                    if (!string.IsNullOrEmpty(trigger.SourceId))
                        continue;

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
        /// 检测并清理孤儿触发器（R-03 防御性校验）。
        ///
        /// 孤儿触发器定义：SourceId 不为空（属于某个 Buff），
        /// 但对应 Buff 的 RuntimeId 已不在任何玩家的 BuffManager 中。
        ///
        /// 调用时机：
        ///   - 战斗结束时（EndBattle）— 全量扫描，日志警告并强制清理
        ///   - 每回合结束后（可选）— 轻量断言，发现即记录
        /// </summary>
        /// <param name="activeBuff RuntimeIds">当前所有玩家 BuffManager 中仍存活的 Buff RuntimeId 集合</param>
        /// <returns>检测到并清理的孤儿触发器数量</returns>
        public int ValidateOrphanTriggers(System.Collections.Generic.HashSet<string> activeBuffRuntimeIds)
        {
            int orphanCount = 0;

            foreach (var kvp in _triggersByTiming)
            {
                var list = kvp.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var trigger = list[i];

                    // 只检查有 SourceId 的 Buff 归属触发器
                    if (string.IsNullOrEmpty(trigger.SourceId))
                        continue;

                    // 若 SourceId 对应的 Buff 已不存在 → 孤儿触发器
                    if (!activeBuffRuntimeIds.Contains(trigger.SourceId))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[TriggerManager] ⚠️ 孤儿触发器检测：{trigger.TriggerId} " +
                            $"(名称={trigger.TriggerName}, 归属Buff={trigger.SourceId}) " +
                            $"已无对应 Buff，强制注销");

                        OnTriggerRemoved?.Invoke(trigger);
                        list.RemoveAt(i);
                        orphanCount++;
                    }
                }
            }

            if (orphanCount > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TriggerManager] 孤儿触发器清理完毕，共移除 {orphanCount} 个");
            }

            return orphanCount;
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
