#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// BuffManager —— 负责 BuffUnit 实例的完整生命周期管理。
    ///
    /// 核心职责：
    ///   1. AddBuff：按 BuffConfig.StackRule 决定叠加策略，创建新实例或修改已有实例。
    ///   2. RemoveBuff：注销该 Buff 注册的所有触发器和 ValueModifier。
    ///   3. TickDecay：回合结束时统一对有限持续时间的 Buff 执行 -1 衰减并移除到期项。
    ///   4. RegisterBuffTriggers：新 Buff 加入时根据 BuffType 向 TriggerManager 注册对应触发器。
    ///
    /// ⚠️ 所有 Buff 触发器必须通过此处的 RegisterBuffTriggers 集中注册，
    ///    禁止在 Handler 或其他地方直接向 TriggerManager 注册 Buff 相关触发器。
    /// </summary>
    public class BuffManager : IBuffManager
    {
        // 实体 EntityId → 该实体拥有的所有 Buff 列表
        private readonly Dictionary<string, List<BuffUnit>> _buffs
            = new Dictionary<string, List<BuffUnit>>();

        // BuffConfig 提供者（构造注入，key = BuffId）
        private readonly Func<string, Buff.BuffConfig?> _configProvider;

        private int _idCounter = 0;

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="configProvider">
        /// 按 BuffId 返回 BuffConfig 的委托（通常由游戏层提供字典查找）。
        /// 若为 null，则 RegisterBuffTriggers 仅记录警告并跳过。
        /// </param>
        public BuffManager(Func<string, Buff.BuffConfig?>? configProvider = null)
        {
            _configProvider = configProvider ?? (_ => null);
        }

        // ══════════════════════════════════════════════════════════
        // 添加 Buff（核心入口）
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public BuffUnit AddBuff(
            BattleContext ctx,
            string targetEntityId,
            string buffConfigId,
            string sourcePlayerId,
            int value = 0,
            int duration = -1)
        {
            // 查询 BuffConfig 以获取叠加规则（通过构造注入的 configProvider）
            var config = _configProvider(buffConfigId);
            if (config == null)
            {
                ctx.RoundLog.Add($"[BuffManager] ⚠️ 未找到 BuffConfig[{buffConfigId}]，跳过。");
                return null;
            }

            // 实际数值 / 持续时间：若调用方未指定则使用 Config 默认值
            int finalValue    = value    != 0  ? value    : config.DefaultValue;
            int finalDuration = duration != -1  ? duration : config.DefaultDuration;
            if (finalDuration == 0) finalDuration = -1; // Config.DefaultDuration==0 表示永久

            if (!_buffs.TryGetValue(targetEntityId, out var list))
            {
                list = new List<BuffUnit>();
                _buffs[targetEntityId] = list;
            }

            // ── 按 StackRule 处理叠加 ─────────────────────────────
            BuffUnit result;
            switch (config.StackRule)
            {
                case BuffStackRule.None:
                    // 不可叠加：已有时直接返回，不做任何修改
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        ctx.RoundLog.Add($"[BuffManager] 实体 {targetEntityId} 已有不可叠加 Buff [{buffConfigId}]，忽略。");
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.RefreshDuration:
                    // 刷新时长：已有时取最大持续时间；无则新建
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (finalDuration < 0)
                            result.RemainingRounds = -1;
                        else if (result.RemainingRounds < 0 || finalDuration > result.RemainingRounds)
                            result.RemainingRounds = finalDuration;
                        result.SourcePlayerId = sourcePlayerId;
                        ctx.RoundLog.Add(
                            $"[BuffManager] 实体 {targetEntityId} 的 Buff [{buffConfigId}] " +
                            $"持续时间刷新为 {result.RemainingRounds}。");
                        PublishAdded(ctx, targetEntityId, result);
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.StackValue:
                    // 叠加数值：同 ConfigId Buff 累加 Value 和 Stacks，受 MaxStacks 限制
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (result.Stacks < config.MaxStacks)
                        {
                            result.Stacks++;
                            result.Value += finalValue;
                            // 数值变化 → 重新注册触发器
                            UnregisterBuffTriggers(ctx, result);
                            RegisterBuffTriggers(ctx, result);
                        }
                        if (finalDuration > 0 && (result.RemainingRounds < 0 || finalDuration > result.RemainingRounds))
                            result.RemainingRounds = finalDuration;
                        ctx.RoundLog.Add(
                            $"[BuffManager] 实体 {targetEntityId} 的 Buff [{buffConfigId}] " +
                            $"叠加至第 {result.Stacks} 层（Value={result.Value}）。");
                        PublishAdded(ctx, targetEntityId, result);
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.KeepHighest:
                    // 保留最高值：新 Value 更大则替换，否则忽略
                    result = TryGetExisting(list, buffConfigId);
                    if (result != null)
                    {
                        if (finalValue > result.Value)
                        {
                            int oldValue = result.Value;
                            result.Value = finalValue;
                            UnregisterBuffTriggers(ctx, result);
                            RegisterBuffTriggers(ctx, result);
                            ctx.RoundLog.Add(
                                $"[BuffManager] 实体 {targetEntityId} 的 Buff [{buffConfigId}] " +
                                $"数值从 {oldValue} 提升至 {finalValue}（KeepHighest）。");
                            PublishAdded(ctx, targetEntityId, result);
                        }
                        else
                        {
                            ctx.RoundLog.Add(
                                $"[BuffManager] 实体 {targetEntityId} 的 Buff [{buffConfigId}] " +
                                $"已有更高值 {result.Value}，新值 {finalValue} 被忽略（KeepHighest）。");
                        }
                        return result;
                    }
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                case BuffStackRule.Independent:
                    // 独立存在：每次都新建实例（适用于不同来源独立计算的 Buff）
                    result = CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;

                default:
                    ctx.RoundLog.Add($"[BuffManager] ⚠️ 未知 StackRule={config.StackRule}，以 RefreshDuration 兜底。");
                    result = TryGetExisting(list, buffConfigId)
                             ?? CreateAndAdd(ctx, list, config, targetEntityId, sourcePlayerId, finalValue, finalDuration);
                    break;
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════
        // 移除 Buff
        // ══════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public bool RemoveBuff(BattleContext ctx, string targetEntityId, string buffRuntimeId)
        {
            if (!_buffs.TryGetValue(targetEntityId, out var list))
                return false;

            var buff = list.FirstOrDefault(b => b.RuntimeId == buffRuntimeId);
            if (buff == null) return false;

            list.Remove(buff);

            // 统一注销该 Buff 持有的所有触发器和数值修正器
            UnregisterBuffTriggers(ctx, buff);

            ctx.RoundLog.Add(
                $"[BuffManager] 实体 {targetEntityId} 的 Buff [{buff.ConfigId}]" +
                $"（RuntimeId={buffRuntimeId}）已移除。");

            ctx.EventBus.Publish(new BuffRemovedEvent
            {
                TargetEntityId = targetEntityId,
                BuffRuntimeId  = buffRuntimeId,
                BuffConfigId   = buff.ConfigId,
            });

            return true;
        }

        /// <inheritdoc/>
        public int RemoveBuffsByConfig(BattleContext ctx, string targetEntityId, string buffConfigId)
        {
            if (!_buffs.TryGetValue(targetEntityId, out var list))
                return 0;

            var toRemove = list.Where(b => b.ConfigId == buffConfigId).ToList();
            foreach (var b in toRemove)
                RemoveBuff(ctx, targetEntityId, b.RuntimeId);

            return toRemove.Count;
        }

        // ──────────────────────────────────────────────────────────
        // 查询
        // ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public bool HasBuff(BattleContext ctx, string entityId, string buffConfigId)
        {
            if (!_buffs.TryGetValue(entityId, out var list))
                return false;
            return list.Any(b => b.ConfigId == buffConfigId);
        }

        /// <summary>获取指定实体的所有 Buff 列表（只读视图）</summary>
        public IReadOnlyList<BuffUnit> GetBuffs(string entityId)
        {
            if (_buffs.TryGetValue(entityId, out var list))
                return list.AsReadOnly();
            return Array.Empty<BuffUnit>();
        }

        // ──────────────────────────────────────────────────────────
        // 回合衰减
        // ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void TickDecay(BattleContext ctx)
        {
            // 遍历所有实体的 Buff 列表，后向迭代避免移除时下标错乱
            foreach (var kv in _buffs.ToList()) // ToList 避免迭代器失效
            {
                var entityId = kv.Key;
                var list     = kv.Value;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var buff = list[i];
                    if (buff.RemainingRounds < 0) continue; // -1 = 永久

                    buff.RemainingRounds--;
                    if (buff.RemainingRounds <= 0)
                    {
                        ctx.RoundLog.Add($"[BuffManager] 实体 {entityId} 的 Buff [{buff.ConfigId}] 到期，即将移除。");
                        RemoveBuff(ctx, entityId, buff.RuntimeId);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void OnRoundEnd(BattleContext ctx, int round)
        {
            // 衰减回合数（到期自动移除）
            TickDecay(ctx);
        }

        // ══════════════════════════════════════════════════════════
        // 私有辅助：创建 BuffUnit、查找已有实例、EventBus 通知
        // ══════════════════════════════════════════════════════════

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
                RuntimeId       = $"buff_{++_idCounter:D6}",
                ConfigId        = config.BuffId,
                DisplayName     = config.BuffName,
                OwnerEntityId   = targetEntityId,
                OwnerPlayerId   = targetEntityId,   // 玩家单位 EntityId == PlayerId
                SourcePlayerId  = sourcePlayerId,
                Value           = value,
                StackRule       = config.StackRule,
                MaxStacks       = config.MaxStacks,
                Stacks          = 1,
                RemainingRounds = duration,
            };

            list.Add(buff);
            RegisterBuffTriggers(ctx, buff);
            PublishAdded(ctx, targetEntityId, buff);

            ctx.RoundLog.Add(
                $"[BuffManager] 实体 {targetEntityId} 获得 Buff [{config.BuffId}]" +
                $"（RuntimeId={buff.RuntimeId}，Value={value}，Duration={duration}）。");

            return buff;
        }

        /// <summary>在 list 中查找第一个匹配 ConfigId 的 BuffUnit（Independent 模式不走此方法）。</summary>
        private static BuffUnit? TryGetExisting(List<BuffUnit> list, string configId)
            => list.FirstOrDefault(b => b.ConfigId == configId);

        private static void PublishAdded(BattleContext ctx, string targetEntityId, BuffUnit buff)
        {
            ctx.EventBus.Publish(new BuffAddedEvent
            {
                TargetEntityId = targetEntityId,
                Buff           = buff,
            });
        }

        // ══════════════════════════════════════════════════════════
        // 触发器注册 / 注销（按 BuffType 决定注册哪些触发器）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 根据 Buff 的 BuffType 向 TriggerManager 注册对应的游戏触发器或 ValueModifier。
        /// 所有注册的 ID 写入 buff.RegisteredTriggerIds / buff.RegisteredModifierIds，
        /// 以便 RemoveBuff 时统一注销。
        ///
        /// 支持的 BuffType → 触发行为 映射：
        ///   Burn / Poison / Bleed → OnRoundStart（DoT 持续伤害，忽护甲）
        ///   Regeneration          → OnRoundStart（持续回血）
        ///   Lifesteal             → AfterDealDamage（吸血，Value=百分比）
        ///   Thorns                → AfterTakeDamage（反伤，Value=百分比）
        ///   Strength              → ValueModifier（+出伤固定值）
        ///   Armor                 → ValueModifier（-入伤固定值）
        ///   Vulnerable            → ValueModifier（+入伤百分比）
        ///   Weak                  → ValueModifier（-出伤百分比）
        /// </summary>
        private void RegisterBuffTriggers(BattleContext ctx, BuffUnit buff)
        {
            var config = _configProvider(buff.ConfigId);
            if (config == null)
            {
                ctx.RoundLog.Add($"[BuffManager] ⚠️ RegisterBuffTriggers：未找到 BuffConfig[{buff.ConfigId}]，跳过触发器注册。");
                return;
            }

            // 本地变量捕获（避免 lambda 闭包中的字段变更）
            string ownerEntityId   = buff.OwnerEntityId;
            string ownerPlayerId   = buff.OwnerPlayerId;
            string sourcePlayerId  = buff.SourcePlayerId;
            string runtimeId       = buff.RuntimeId;
            string configId        = buff.ConfigId;

            switch (config.BuffType)
            {
                // ── DoT 类：每回合开始造成持续伤害（忽护甲）──────────
                case BuffType.Burn:
                case BuffType.Poison:
                case BuffType.Bleed:
                {
                    // 注意：value 捕获当前数值，StackValue 规则重新注册时会刷新
                    int value = buff.Value;
                    string tid = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName       = $"{configId}-DoT-{runtimeId}",
                        Timing            = TriggerTiming.OnRoundStart,
                        OwnerPlayerId     = ownerPlayerId,
                        SourceId          = runtimeId,
                        Priority          = 300,
                        RemainingTriggers = -1,
                        RemainingRounds   = -1,
                        InlineExecute     = (bCtx, _) =>
                        {
                            var target = bCtx.GetEntity(ownerEntityId);
                            if (target == null || !target.IsAlive) return;
                            target.Hp -= value;
                            bCtx.RoundLog.Add(
                                $"[Buff-DoT|{configId}] {ownerEntityId}" +
                                $" 受到 {value} 持续伤害，剩余HP={target.Hp}");
                            bCtx.EventBus.Publish(new DamageDealtEvent
                            {
                                SourceEntityId = sourcePlayerId,
                                TargetEntityId = ownerEntityId,
                                RealHpDamage   = value,
                                IsDot          = true,
                            });
                        }
                    });
                    buff.RegisteredTriggerIds.Add(tid);
                    break;
                }

                // ── 回复类：每回合开始回血 ─────────────────────────────
                case BuffType.Regeneration:
                {
                    int value = buff.Value;
                    string tid = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName       = $"regen-{runtimeId}",
                        Timing            = TriggerTiming.OnRoundStart,
                        OwnerPlayerId     = ownerPlayerId,
                        SourceId          = runtimeId,
                        Priority          = 100,
                        RemainingTriggers = -1,
                        RemainingRounds   = -1,
                        InlineExecute     = (bCtx, _) =>
                        {
                            var self = bCtx.GetEntity(ownerEntityId);
                            if (self == null || !self.IsAlive) return;
                            int heal = Math.Min(value, self.MaxHp - self.Hp);
                            if (heal <= 0) return;
                            self.Hp += heal;
                            bCtx.RoundLog.Add(
                                $"[Buff-Regen] {ownerEntityId} 回复 {heal} HP，当前HP={self.Hp}");
                            bCtx.EventBus.Publish(new HealEvent
                            {
                                SourceEntityId = ownerEntityId,
                                TargetEntityId = ownerEntityId,
                                RealHealAmount = heal,
                            });
                        }
                    });
                    buff.RegisteredTriggerIds.Add(tid);
                    break;
                }

                // ── 吸血：造成伤害后按百分比回血 ──────────────────────
                case BuffType.Lifesteal:
                {
                    int pct = buff.Value; // 百分比
                    string tid = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName       = $"lifesteal-{runtimeId}",
                        Timing            = TriggerTiming.AfterDealDamage,
                        OwnerPlayerId     = ownerPlayerId,
                        SourceId          = runtimeId,
                        Priority          = 100,
                        RemainingTriggers = -1,
                        RemainingRounds   = -1,
                        InlineExecute     = (bCtx, tCtx) =>
                        {
                            // AfterDealDamage：tCtx.SourceEntityId == 施害方
                            if (tCtx.SourceEntityId != ownerEntityId) return;
                            var self = bCtx.GetEntity(ownerEntityId);
                            if (self == null || !self.IsAlive) return;
                            int heal = Math.Min(tCtx.Value * pct / 100, self.MaxHp - self.Hp);
                            if (heal <= 0) return;
                            self.Hp += heal;
                            bCtx.RoundLog.Add(
                                $"[Buff-Lifesteal] {ownerEntityId} 吸血 {heal} HP" +
                                $"（伤害={tCtx.Value}×{pct}%），当前HP={self.Hp}");
                            bCtx.EventBus.Publish(new HealEvent
                            {
                                SourceEntityId = ownerEntityId,
                                TargetEntityId = ownerEntityId,
                                RealHealAmount = heal,
                            });
                        }
                    });
                    buff.RegisteredTriggerIds.Add(tid);
                    break;
                }

                // ── 反伤：受到伤害后将部分伤害反弹给攻击者 ────────────
                // ⚠️ AfterTakeDamage 约定：tCtx.SourceEntityId=受害方（即自己），tCtx.TargetEntityId=攻击方
                case BuffType.Thorns:
                {
                    int pct = buff.Value;
                    string tid = ctx.TriggerManager.Register(new TriggerUnit
                    {
                        TriggerName       = $"thorns-{runtimeId}",
                        Timing            = TriggerTiming.AfterTakeDamage,
                        OwnerPlayerId     = ownerPlayerId,
                        SourceId          = runtimeId,
                        Priority          = 300,
                        RemainingTriggers = -1,
                        RemainingRounds   = -1,
                        InlineExecute     = (bCtx, tCtx) =>
                        {
                            // AfterTakeDamage：SourceEntityId = 受害方（自己）
                            if (tCtx.SourceEntityId != ownerEntityId) return;
                            var attacker = bCtx.GetEntity(tCtx.TargetEntityId);
                            if (attacker == null || !attacker.IsAlive) return;
                            int dmg = tCtx.Value * pct / 100;
                            if (dmg <= 0) return;
                            attacker.Hp -= dmg;
                            bCtx.RoundLog.Add(
                                $"[Buff-Thorns] {ownerEntityId} 对 {attacker.EntityId}" +
                                $" 反伤 {dmg}（受伤={tCtx.Value}×{pct}%），攻击者HP={attacker.Hp}");
                            bCtx.EventBus.Publish(new DamageDealtEvent
                            {
                                SourceEntityId = ownerEntityId,
                                TargetEntityId = attacker.EntityId,
                                RealHpDamage   = dmg,
                                IsThorns       = true,
                            });
                        }
                    });
                    buff.RegisteredTriggerIds.Add(tid);
                    break;
                }

                // ── 属性修正类：注册 ValueModifier ────────────────────
                // 通过 IValueModifierManager 注册 Add / Mul 类修正器。
                // Strength/Armor = 固定加减（Add，正负区分方向）
                // Vulnerable/Weak = 百分比放大/缩小（Mul，百分比整数，如 150=+50%，75=-25%）
                case BuffType.Strength:
                {
                    // 力量：施害方每次出伤 +Value（Add 加成）
                    string mid = ctx.ValueModifierManager.AddModifier(new ValueModifier
                    {
                        Type             = ModifierType.Add,
                        Value            = buff.Value,
                        OwnerPlayerId    = ownerPlayerId,
                        TargetEffectType = EffectType.Damage,
                    });
                    buff.RegisteredModifierIds.Add(mid);
                    ctx.RoundLog.Add($"[BuffManager] 注册力量修正器 {mid}，+{buff.Value} 出伤。");
                    break;
                }

                case BuffType.Armor:
                {
                    // 护甲：持有方受到伤害 -Value（Add 负值减免）
                    // ⚠️ 注意：Armor 修正的是受伤方，OwnerPlayerId 为受伤方
                    string mid = ctx.ValueModifierManager.AddModifier(new ValueModifier
                    {
                        Type             = ModifierType.Add,
                        Value            = -buff.Value,    // 负数 = 减少伤害
                        OwnerPlayerId    = ownerPlayerId,
                        TargetEffectType = EffectType.Damage,
                    });
                    buff.RegisteredModifierIds.Add(mid);
                    ctx.RoundLog.Add($"[BuffManager] 注册护甲修正器 {mid}，-{buff.Value} 入伤。");
                    break;
                }

                case BuffType.Vulnerable:
                {
                    // 易伤：受伤时伤害乘以 (100 + Value)/100，buff.Value = 额外%（如 50 = +50%）
                    string mid = ctx.ValueModifierManager.AddModifier(new ValueModifier
                    {
                        Type             = ModifierType.Mul,
                        Value            = 100 + buff.Value,  // 如 Value=50 → 150% 伤害
                        OwnerPlayerId    = ownerPlayerId,
                        TargetEffectType = EffectType.Damage,
                    });
                    buff.RegisteredModifierIds.Add(mid);
                    ctx.RoundLog.Add($"[BuffManager] 注册易伤修正器 {mid}，+{buff.Value}% 入伤。");
                    break;
                }

                case BuffType.Weak:
                {
                    // 虚弱：出伤乘以 (100 - Value)/100，buff.Value = 减少%（如 25 = -25%）
                    string mid = ctx.ValueModifierManager.AddModifier(new ValueModifier
                    {
                        Type             = ModifierType.Mul,
                        Value            = 100 - buff.Value,  // 如 Value=25 → 75% 出伤
                        OwnerPlayerId    = ownerPlayerId,
                        TargetEffectType = EffectType.Damage,
                    });
                    buff.RegisteredModifierIds.Add(mid);
                    ctx.RoundLog.Add($"[BuffManager] 注册虚弱修正器 {mid}，-{buff.Value}% 出伤。");
                    break;
                }

                // 其他 BuffType 暂不注册（如 Invincible 由 Entity.IsInvincible 属性控制）
                default:
                    ctx.RoundLog.Add($"[BuffManager] BuffType={config.BuffType} 无自动触发器。");
                    break;
            }
        }

        /// <summary>注销 buff 持有的所有触发器和数值修正器，并清空列表。</summary>
        private static void UnregisterBuffTriggers(BattleContext ctx, BuffUnit buff)
        {
            foreach (var tid in buff.RegisteredTriggerIds)
                ctx.TriggerManager.Unregister(tid);   // 修复：用 TriggerId 精确注销，而非 SourceId
            buff.RegisteredTriggerIds.Clear();

            foreach (var mid in buff.RegisteredModifierIds)
                ctx.ValueModifierManager.RemoveModifier(mid);
            buff.RegisteredModifierIds.Clear();
        }
    }
}
