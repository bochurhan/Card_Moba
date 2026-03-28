#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Resolvers;

namespace CardMoba.BattleCore.Managers
{
    public class TriggerManager : ITriggerManager
    {
        private readonly Dictionary<TriggerTiming, List<TriggerUnit>> _triggers
            = new Dictionary<TriggerTiming, List<TriggerUnit>>();

        private readonly ConditionChecker _conditionChecker = new ConditionChecker();
        private int _idCounter;

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
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            return trigger.TriggerId;
        }

        public bool Unregister(string triggerId)
        {
            foreach (var kv in _triggers)
            {
                if (kv.Value.RemoveAll(t => t.TriggerId == triggerId) > 0)
                    return true;
            }

            return false;
        }

        public int UnregisterBySourceId(string sourceId)
        {
            int total = 0;
            foreach (var kv in _triggers)
                total += kv.Value.RemoveAll(t => t.SourceId == sourceId);
            return total;
        }

        public void Fire(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx)
        {
            if (!_triggers.TryGetValue(timing, out var list) || list.Count == 0)
                return;

            var snapshot = list.ToList();
            foreach (var trigger in snapshot)
            {
                if (trigger.RemainingTriggers == 0)
                    continue;

                if (trigger.Conditions.Count > 0)
                {
                    var ownerData = ctx.GetPlayer(trigger.OwnerPlayerId);
                    if (ownerData == null)
                        continue;

                    if (!_conditionChecker.Check(trigger.Conditions, ctx, ownerData.HeroEntity, triggerCtx))
                        continue;
                }

                var ownerPlayer = ctx.GetPlayer(trigger.OwnerPlayerId);
                var sourceEntityId = ownerPlayer?.HeroEntity.EntityId ?? trigger.OwnerPlayerId;

                foreach (var effectUnit in trigger.Effects)
                {
                    var clonedEffect = EffectUnitCloner.Clone(effectUnit);
                    ctx.PendingQueue.Enqueue(new PendingEffectEntry
                    {
                        Effect = clonedEffect,
                        SourceEntityId = sourceEntityId,
                        SourceTriggerId = trigger.TriggerId,
                        TriggerContext = CloneTriggerContext(triggerCtx),
                        PreResolvedTargetIds = BuildPreResolvedTargetIds(clonedEffect.TargetType, triggerCtx),
                    });
                }

                if (trigger.RemainingTriggers > 0)
                    trigger.RemainingTriggers--;
            }

            list.RemoveAll(t => t.RemainingTriggers == 0);
        }

        public void TickDecay(BattleContext ctx)
        {
            foreach (var kv in _triggers)
            {
                var list = kv.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var trigger = list[i];
                    if (trigger.RemainingRounds <= 0)
                        continue;

                    trigger.RemainingRounds--;
                    if (trigger.RemainingRounds == 0)
                    {
                        list.RemoveAt(i);
                        ctx.RoundLog.Add($"[TriggerManager] 触发器 {trigger.TriggerId}（{trigger.TriggerName}）已到期，自动注销。");
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
                Value = triggerContext.Value,
                Round = triggerContext.Round,
                Extra = new Dictionary<string, object>(triggerContext.Extra),
            };
        }

        private static List<string>? BuildPreResolvedTargetIds(string targetType, TriggerContext triggerContext)
        {
            if (targetType.Equals("TriggerSource", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(triggerContext.SourceEntityId))
                    return new List<string> { triggerContext.SourceEntityId };
                return new List<string>();
            }

            if (targetType.Equals("TriggerTarget", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(triggerContext.TargetEntityId))
                    return new List<string> { triggerContext.TargetEntityId };
                return new List<string>();
            }

            return null;
        }
    }
}
