#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    public class BuffManager : IBuffManager
    {
        private readonly Dictionary<string, List<BuffUnit>> _buffs
            = new Dictionary<string, List<BuffUnit>>();

        private readonly Func<string, Buff.BuffConfig?> _configProvider;
        private int _idCounter;

        public BuffManager(Func<string, Buff.BuffConfig?>? configProvider = null)
        {
            _configProvider = configProvider ?? (_ => null);
        }

        public BuffUnit AddBuff(
            BattleContext ctx,
            string targetEntityId,
            string buffConfigId,
            string sourcePlayerId,
            int value = 0,
            int duration = -1)
        {
            var config = _configProvider(buffConfigId);
            if (config == null)
            {
                ctx.RoundLog.Add($"[BuffManager] Missing BuffConfig[{buffConfigId}].");
                return null;
            }

            int finalValue = value != 0 ? value : config.DefaultValue;
            int finalDuration = duration != -1 ? duration : config.DefaultDuration;
            if (finalDuration == 0)
                finalDuration = -1;

            if (!_buffs.TryGetValue(targetEntityId, out var list))
            {
                list = new List<BuffUnit>();
                _buffs[targetEntityId] = list;
            }

            BuffUnit result;
            switch (config.StackRule)
            {
                case BuffStackRule.None:
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        ctx.RoundLog.Add($"[BuffManager] Ignore non-stackable buff {buffConfigId} on {targetEntityId}.");
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.RefreshDuration:
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (finalDuration < 0)
                        {
                            result.RemainingRounds = -1;
                        }
                        else if (result.RemainingRounds < 0 || finalDuration > result.RemainingRounds)
                        {
                            result.RemainingRounds = finalDuration;
                        }

                        result.SourcePlayerId = sourcePlayerId;
                        ctx.RoundLog.Add($"[BuffManager] Refresh duration of {buffConfigId} on {targetEntityId} to {result.RemainingRounds}.");
                        PublishAdded(ctx, targetEntityId, result);
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.StackValue:
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (result.Stacks < config.MaxStacks)
                        {
                            result.Stacks++;
                            result.Value += finalValue;
                            UnregisterBuffTriggers(ctx, result);
                            RegisterBuffTriggers(ctx, result);
                        }

                        if (finalDuration > 0 && (result.RemainingRounds < 0 || finalDuration > result.RemainingRounds))
                            result.RemainingRounds = finalDuration;

                        ctx.RoundLog.Add($"[BuffManager] Stack {buffConfigId} on {targetEntityId} to {result.Stacks} (Value={result.Value}).");
                        PublishAdded(ctx, targetEntityId, result);
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.KeepHighest:
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (finalValue > result.Value)
                        {
                            result.Value = finalValue;
                            UnregisterBuffTriggers(ctx, result);
                            RegisterBuffTriggers(ctx, result);
                            ctx.RoundLog.Add($"[BuffManager] Upgrade {buffConfigId} on {targetEntityId} to {finalValue}.");
                            PublishAdded(ctx, targetEntityId, result);
                        }
                        else
                        {
                            ctx.RoundLog.Add($"[BuffManager] Keep higher value {result.Value} for {buffConfigId} on {targetEntityId}.");
                        }

                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.Independent:
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                default:
                    ctx.RoundLog.Add($"[BuffManager] Unknown stack rule {config.StackRule}, fallback to existing or create.");
                    result = TryGetExisting(list, buffConfigId)
                             ?? CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;
            }

            return result;
        }

        public bool RemoveBuff(BattleContext ctx, string targetEntityId, string buffRuntimeId)
        {
            if (!_buffs.TryGetValue(targetEntityId, out var list))
                return false;

            var buff = list.FirstOrDefault(b => b.RuntimeId == buffRuntimeId);
            if (buff == null)
                return false;

            list.Remove(buff);
            UnregisterBuffTriggers(ctx, buff);

            ctx.RoundLog.Add($"[BuffManager] Remove buff {buff.ConfigId} ({buffRuntimeId}) from {targetEntityId}.");

            ctx.EventBus.Publish(new BuffRemovedEvent
            {
                TargetEntityId = targetEntityId,
                BuffRuntimeId = buffRuntimeId,
                BuffConfigId = buff.ConfigId,
            });
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnBuffRemoved, new TriggerContext
            {
                SourceEntityId = targetEntityId,
                Extra = new Dictionary<string, object>
                {
                    ["buffRuntimeId"] = buffRuntimeId,
                    ["buffConfigId"] = buff.ConfigId,
                },
            });

            return true;
        }

        public int RemoveBuffsByConfig(BattleContext ctx, string targetEntityId, string buffConfigId)
        {
            if (!_buffs.TryGetValue(targetEntityId, out var list))
                return 0;

            var toRemove = list.Where(b => b.ConfigId == buffConfigId).ToList();
            foreach (var buff in toRemove)
                RemoveBuff(ctx, targetEntityId, buff.RuntimeId);

            return toRemove.Count;
        }

        public bool HasBuff(BattleContext ctx, string entityId, string buffConfigId)
        {
            if (!_buffs.TryGetValue(entityId, out var list))
                return false;

            return list.Any(b => b.ConfigId == buffConfigId);
        }

        public IReadOnlyList<BuffUnit> GetBuffs(string entityId)
        {
            if (_buffs.TryGetValue(entityId, out var list))
                return list.AsReadOnly();

            return Array.Empty<BuffUnit>();
        }

        public void TickDecay(BattleContext ctx)
        {
            foreach (var kv in _buffs.ToList())
            {
                var entityId = kv.Key;
                var list = kv.Value;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var buff = list[i];
                    if (buff.RemainingRounds < 0)
                        continue;

                    buff.RemainingRounds--;
                    if (buff.RemainingRounds <= 0)
                    {
                        ctx.RoundLog.Add($"[BuffManager] Buff {buff.ConfigId} on {entityId} expired.");
                        RemoveBuff(ctx, entityId, buff.RuntimeId);
                    }
                }
            }
        }

        public void OnRoundEnd(BattleContext ctx, int round)
        {
            TickDecay(ctx);
        }

        private BuffUnit CreateAndAdd(
            BattleContext ctx,
            List<BuffUnit> list,
            Buff.BuffConfig config,
            string targetEntityId,
            string sourcePlayerId,
            int value,
            int duration)
        {
            var buff = new BuffUnit
            {
                RuntimeId = $"buff_{++_idCounter:D6}",
                ConfigId = config.BuffId,
                DisplayName = config.BuffName,
                OwnerEntityId = targetEntityId,
                OwnerPlayerId = ctx.GetPlayerByEntityId(targetEntityId)?.PlayerId ?? targetEntityId,
                SourcePlayerId = sourcePlayerId,
                Value = value,
                StackRule = config.StackRule,
                MaxStacks = config.MaxStacks,
                Stacks = 1,
                RemainingRounds = duration,
            };

            list.Add(buff);
            RegisterBuffTriggers(ctx, buff);
            PublishAdded(ctx, targetEntityId, buff);

            ctx.RoundLog.Add($"[BuffManager] Add buff {config.BuffId} ({buff.RuntimeId}) to {targetEntityId} with Value={value}, Duration={duration}.");
            return buff;
        }

        private static BuffUnit? TryGetExisting(List<BuffUnit> list, string configId)
        {
            return list.FirstOrDefault(b => b.ConfigId == configId);
        }

        private static void PublishAdded(BattleContext ctx, string targetEntityId, BuffUnit buff)
        {
            ctx.EventBus.Publish(new BuffAddedEvent
            {
                TargetEntityId = targetEntityId,
                Buff = buff,
            });
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnBuffAdded, new TriggerContext
            {
                SourceEntityId = targetEntityId,
                Extra = new Dictionary<string, object>
                {
                    ["buffRuntimeId"] = buff.RuntimeId,
                    ["buffConfigId"] = buff.ConfigId,
                },
            });
        }

        private void RegisterBuffTriggers(BattleContext ctx, BuffUnit buff)
        {
            var config = _configProvider(buff.ConfigId);
            if (config == null)
            {
                ctx.RoundLog.Add($"[BuffManager] Missing BuffConfig[{buff.ConfigId}] during trigger registration.");
                return;
            }

            string ownerPlayerId = buff.OwnerPlayerId;
            string runtimeId = buff.RuntimeId;
            string configId = buff.ConfigId;

            switch (config.BuffType)
            {
                case BuffType.Burn:
                case BuffType.Poison:
                case BuffType.Bleed:
                {
                    string triggerId = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName = $"{configId}-DoT-{runtimeId}",
                        Timing = TriggerTiming.OnRoundStart,
                        OwnerPlayerId = ownerPlayerId,
                        SourceId = runtimeId,
                        Priority = 300,
                        RemainingTriggers = -1,
                        RemainingRounds = -1,
                        Effects = new List<EffectUnit>
                        {
                            new EffectUnit
                            {
                                EffectId = $"buff_dot_{runtimeId}",
                                Type = EffectType.Pierce,
                                TargetType = "Self",
                                ValueExpression = buff.Value.ToString(),
                                Layer = SettleLayer.Damage,
                                Params = new Dictionary<string, string>
                                {
                                    ["isDot"] = "true",
                                },
                            },
                        },
                    });
                    buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                case BuffType.Regeneration:
                {
                    string triggerId = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName = $"regen-{runtimeId}",
                        Timing = TriggerTiming.OnRoundStart,
                        OwnerPlayerId = ownerPlayerId,
                        SourceId = runtimeId,
                        Priority = 100,
                        RemainingTriggers = -1,
                        RemainingRounds = -1,
                        Effects = new List<EffectUnit>
                        {
                            new EffectUnit
                            {
                                EffectId = $"buff_regen_{runtimeId}",
                                Type = EffectType.Heal,
                                TargetType = "Self",
                                ValueExpression = buff.Value.ToString(),
                                Layer = SettleLayer.BuffSpecial,
                            },
                        },
                    });
                    buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                case BuffType.Lifesteal:
                {
                    string triggerId = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName = $"lifesteal-{runtimeId}",
                        Timing = TriggerTiming.AfterDealDamage,
                        OwnerPlayerId = ownerPlayerId,
                        SourceId = runtimeId,
                        Priority = 100,
                        RemainingTriggers = -1,
                        RemainingRounds = -1,
                        Effects = new List<EffectUnit>
                        {
                            new EffectUnit
                            {
                                EffectId = $"buff_lifesteal_{runtimeId}",
                                Type = EffectType.Heal,
                                TargetType = "Self",
                                ValueExpression = $"{{{{trigCtx.value * {buff.Value} / 100}}}}",
                                Layer = SettleLayer.BuffSpecial,
                                Conditions = new List<string> { "trigCtx.value > 0" },
                            },
                        },
                    });
                    buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                case BuffType.Thorns:
                {
                    string triggerId = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName = $"thorns-{runtimeId}",
                        Timing = TriggerTiming.AfterTakeDamage,
                        OwnerPlayerId = ownerPlayerId,
                        SourceId = runtimeId,
                        Priority = 300,
                        RemainingTriggers = -1,
                        RemainingRounds = -1,
                        Effects = new List<EffectUnit>
                        {
                            new EffectUnit
                            {
                                EffectId = $"buff_thorns_{runtimeId}",
                                Type = EffectType.Damage,
                                TargetType = "TriggerTarget",
                                ValueExpression = $"{{{{trigCtx.value * {buff.Value} / 100}}}}",
                                Layer = SettleLayer.Damage,
                                Conditions = new List<string> { "trigCtx.value > 0" },
                                Params = new Dictionary<string, string>
                                {
                                    ["isThorns"] = "true",
                                },
                            },
                        },
                    });
                    buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                case BuffType.Strength:
                {
                    foreach (var effectType in new[] { EffectType.Damage, EffectType.Pierce })
                    {
                        string modifierId = ctx.ValueModifierManager.AddModifier(new ValueModifier
                        {
                            Type = ModifierType.Add,
                            Value = buff.Value,
                            OwnerPlayerId = ownerPlayerId,
                            TargetEffectType = effectType,
                            Scope = ModifierScope.OutgoingDamage,
                        });
                        buff.RegisteredModifierIds.Add(modifierId);
                    }
                    break;
                }

                case BuffType.Armor:
                {
                    string modifierId = ctx.ValueModifierManager.AddModifier(new ValueModifier
                    {
                        Type = ModifierType.Add,
                        Value = -buff.Value,
                        OwnerPlayerId = ownerPlayerId,
                        TargetEffectType = EffectType.Damage,
                        Scope = ModifierScope.IncomingDamage,
                    });
                    buff.RegisteredModifierIds.Add(modifierId);
                    break;
                }

                case BuffType.Vulnerable:
                {
                    foreach (var effectType in new[] { EffectType.Damage, EffectType.Pierce })
                    {
                        string modifierId = ctx.ValueModifierManager.AddModifier(new ValueModifier
                        {
                            Type = ModifierType.Mul,
                            Value = 100 + buff.Value,
                            OwnerPlayerId = ownerPlayerId,
                            TargetEffectType = effectType,
                            Scope = ModifierScope.IncomingDamage,
                        });
                        buff.RegisteredModifierIds.Add(modifierId);
                    }
                    break;
                }

                case BuffType.Weak:
                {
                    foreach (var effectType in new[] { EffectType.Damage, EffectType.Pierce })
                    {
                        string modifierId = ctx.ValueModifierManager.AddModifier(new ValueModifier
                        {
                            Type = ModifierType.Mul,
                            Value = 100 - buff.Value,
                            OwnerPlayerId = ownerPlayerId,
                            TargetEffectType = effectType,
                            Scope = ModifierScope.OutgoingDamage,
                        });
                        buff.RegisteredModifierIds.Add(modifierId);
                    }
                    break;
                }

                default:
                    ctx.RoundLog.Add($"[BuffManager] BuffType={config.BuffType} does not register runtime hooks.");
                    break;
            }
        }

        private static void UnregisterBuffTriggers(BattleContext ctx, BuffUnit buff)
        {
            foreach (var triggerId in buff.RegisteredTriggerIds)
                ctx.TriggerManager.Unregister(triggerId);
            buff.RegisteredTriggerIds.Clear();

            foreach (var modifierId in buff.RegisteredModifierIds)
                ctx.ValueModifierManager.RemoveModifier(modifierId);
            buff.RegisteredModifierIds.Clear();
        }
    }
}
