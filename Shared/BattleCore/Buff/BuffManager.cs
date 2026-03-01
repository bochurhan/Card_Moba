using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Settlement;
using CardMoba.BattleCore.Trigger;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Buff
{
    /// <summary>
    /// Buff 管理器 —— 负责管理单个玩家的所有 Buff。
    /// 
    /// 职责（TD-01 重构后）：
    /// - 添加/移除/查询 Buff
    /// - 处理 Buff 叠加规则
    /// - 维护 PlayerBattleState 上的布尔镜像字段（IsSilenced 等）
    /// - 回合结束时衰减持续时间（纯数据层，不执行效果）
    /// - 添加触发型 Buff 时，向 TriggerManager 注册对应触发器
    /// 
    /// 不负责：
    /// - 执行任何伤害/治疗效果（由 DamageHelper 负责）
    /// - 决定触发时机（由 TriggerManager 负责调度）
    /// </summary>
    public class BuffManager
    {
        private readonly PlayerBattleState _owner;
        private readonly BattleContext _ctx;
        private readonly List<BuffInstance> _buffs = new List<BuffInstance>();
        private int _runtimeIdCounter = 0;

        /// <summary>当前所有 Buff（只读）</summary>
        public IReadOnlyList<BuffInstance> Buffs => _buffs;

        /// <summary>Buff 添加事件</summary>
        public event Action<BuffInstance> OnBuffAdded;

        /// <summary>Buff 移除事件</summary>
        public event Action<BuffInstance> OnBuffRemoved;

        /// <summary>Buff 层数变化事件</summary>
        public event Action<BuffInstance, int> OnBuffStackChanged;

        public BuffManager(PlayerBattleState owner, BattleContext ctx)
        {
            _owner = owner;
            _ctx = ctx;
        }

        /// <summary>
        /// 添加一个 Buff。
        /// 若该 Buff 有触发效果（Thorns/Lifesteal/Poison 等），
        /// 将自动向 TriggerManager 注册对应的触发器。
        /// </summary>
        /// <param name="buff">要添加的 Buff 实例</param>
        /// <returns>实际添加或更新的 Buff 实例</returns>
        public BuffInstance AddBuff(BuffInstance buff)
        {
            if (buff == null) return null;

            // 分配运行时 ID
            if (string.IsNullOrEmpty(buff.RuntimeId))
            {
                buff.RuntimeId = $"BUFF_{_owner.PlayerId}_{++_runtimeIdCounter}";
            }

            // 检查是否已存在同类型 Buff（尝试叠加）
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].BuffId == buff.BuffId)
                {
                    int oldStacks = _buffs[i].Stacks;
                    if (_buffs[i].TryStack(buff))
                    {
                        if (_buffs[i].Stacks != oldStacks)
                        {
                            OnBuffStackChanged?.Invoke(_buffs[i], oldStacks);
                        }
                        // 叠加刷新后重新应用属性修改（如护甲值变化）
                        ApplyBuffModifiers(_buffs[i]);
                        return _buffs[i];
                    }
                    // Independent 规则 → 继续添加新实例
                }
            }

            // 添加新 Buff
            _buffs.Add(buff);
            ApplyBuffModifiers(buff);

            // 为有触发效果的 Buff 注册触发器
            RegisterBuffTriggers(buff);

            OnBuffAdded?.Invoke(buff);
            return buff;
        }

        /// <summary>
        /// 通过配置创建并添加 Buff。
        /// </summary>
        public BuffInstance AddBuff(BuffConfig config, string sourcePlayerId, int? value = null, int? duration = null)
        {
            var instance = config.CreateInstance(sourcePlayerId, value, duration);
            return AddBuff(instance);
        }

        /// <summary>
        /// 移除指定 Buff，并注销其在 TriggerManager 中的触发器。
        /// </summary>
        public bool RemoveBuff(BuffInstance buff)
        {
            if (buff == null) return false;

            int index = _buffs.IndexOf(buff);
            if (index >= 0)
            {
                UnregisterBuffTriggers(buff);
                RemoveBuffModifiers(buff);
                _buffs.RemoveAt(index);
                OnBuffRemoved?.Invoke(buff);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 通过 RuntimeId 移除 Buff。
        /// </summary>
        public bool RemoveBuffByRuntimeId(string runtimeId)
        {
            var buff = GetBuffByRuntimeId(runtimeId);
            return RemoveBuff(buff);
        }

        /// <summary>
        /// 移除所有指定类型的 Buff。
        /// </summary>
        public int RemoveBuffsByType(BuffType buffType)
        {
            int count = 0;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].BuffType == buffType)
                {
                    UnregisterBuffTriggers(_buffs[i]);
                    RemoveBuffModifiers(_buffs[i]);
                    OnBuffRemoved?.Invoke(_buffs[i]);
                    _buffs.RemoveAt(i);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 驱散所有可驱散的增益 Buff。
        /// </summary>
        public int DispelBuffs()
        {
            int count = 0;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].IsBuff && _buffs[i].IsDispellable)
                {
                    UnregisterBuffTriggers(_buffs[i]);
                    RemoveBuffModifiers(_buffs[i]);
                    OnBuffRemoved?.Invoke(_buffs[i]);
                    _buffs.RemoveAt(i);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 净化所有可净化的减益 Buff。
        /// </summary>
        public int PurgeDebuffs()
        {
            int count = 0;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (!_buffs[i].IsBuff && _buffs[i].IsPurgeable)
                {
                    UnregisterBuffTriggers(_buffs[i]);
                    RemoveBuffModifiers(_buffs[i]);
                    OnBuffRemoved?.Invoke(_buffs[i]);
                    _buffs.RemoveAt(i);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 减少指定 Buff 一层叠加。
        /// </summary>
        public bool RemoveStack(BuffInstance buff)
        {
            if (buff == null) return false;

            int oldStacks = buff.Stacks;
            if (buff.RemoveStack())
            {
                return RemoveBuff(buff);
            }
            else
            {
                OnBuffStackChanged?.Invoke(buff, oldStacks);
                return true;
            }
        }

        /// <summary>
        /// 通过 RuntimeId 查找 Buff。
        /// </summary>
        public BuffInstance GetBuffByRuntimeId(string runtimeId)
        {
            foreach (var buff in _buffs)
            {
                if (buff.RuntimeId == runtimeId)
                    return buff;
            }
            return null;
        }

        /// <summary>
        /// 通过 BuffId 查找第一个匹配的 Buff。
        /// </summary>
        public BuffInstance GetBuffById(string buffId)
        {
            foreach (var buff in _buffs)
            {
                if (buff.BuffId == buffId)
                    return buff;
            }
            return null;
        }

        /// <summary>
        /// 通过类型查找所有 Buff。
        /// </summary>
        public List<BuffInstance> GetBuffsByType(BuffType buffType)
        {
            var result = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.BuffType == buffType)
                    result.Add(buff);
            }
            return result;
        }

        /// <summary>
        /// 检查是否拥有指定类型的 Buff。
        /// </summary>
        public bool HasBuffType(BuffType buffType)
        {
            foreach (var buff in _buffs)
            {
                if (buff.BuffType == buffType)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取指定类型 Buff 的总层数。
        /// </summary>
        public int GetTotalStacks(BuffType buffType)
        {
            int total = 0;
            foreach (var buff in _buffs)
            {
                if (buff.BuffType == buffType)
                    total += buff.Stacks;
            }
            return total;
        }

        /// <summary>
        /// 回合结束时触发：仅负责持续时间衰减，不执行任何效果。
        /// 效果的触发（中毒扣血、再生加血等）已通过 TriggerManager 注册，
        /// 由 BattleContext.OnRoundEnd 中的 TriggerManager.FireTriggers(OnRoundEnd) 统一调度。
        /// </summary>
        public void OnRoundEnd()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.IsPermanent) continue;

                buff.RemainingRounds--;
                if (buff.RemainingRounds <= 0)
                {
                    UnregisterBuffTriggers(buff);
                    RemoveBuffModifiers(buff);
                    OnBuffRemoved?.Invoke(buff);
                    _buffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清除所有 Buff（注销触发器 + 移除属性修改）。
        /// </summary>
        public void ClearAllBuffs()
        {
            foreach (var buff in _buffs)
            {
                UnregisterBuffTriggers(buff);
                RemoveBuffModifiers(buff);
                OnBuffRemoved?.Invoke(buff);
            }
            _buffs.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // 私有方法 —— 属性修改
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 应用 Buff 对玩家属性的静态修改（仅数值/布尔字段，不执行效果）。
        /// </summary>
        private void ApplyBuffModifiers(BuffInstance buff)
        {
            switch (buff.BuffType)
            {
                case BuffType.Armor:
                    _owner.Armor += buff.TotalValue;
                    break;
                case BuffType.Strength:
                    _owner.Strength += buff.TotalValue;
                    break;
                case BuffType.Shield:
                    _owner.Shield += buff.TotalValue;
                    break;
                case BuffType.MaxHpBonus:
                    _owner.MaxHp += buff.TotalValue;
                    _owner.Hp += buff.TotalValue;
                    break;
                case BuffType.Invincible:
                    _owner.IsInvincible = true;
                    break;
                case BuffType.Lifesteal:
                    _owner.LifestealPercent += buff.TotalValue;
                    break;
                case BuffType.Thorns:
                    _owner.ThornsValue += buff.TotalValue;
                    break;
                case BuffType.DamageReduction:
                    _owner.DamageReductionPercent += buff.TotalValue;
                    break;
                case BuffType.Vulnerable:
                    _owner.VulnerableStacks += buff.Stacks;
                    _owner.IsVulnerable = true;
                    break;
                case BuffType.Weak:
                    _owner.WeakStacks += buff.Stacks;
                    _owner.IsWeak = true;
                    break;
                case BuffType.Stun:
                    _owner.IsStunned = true;
                    break;
                case BuffType.Silence:
                    _owner.IsSilenced = true;
                    break;
            }
        }

        /// <summary>
        /// 移除 Buff 对玩家属性的静态修改。
        /// </summary>
        private void RemoveBuffModifiers(BuffInstance buff)
        {
            switch (buff.BuffType)
            {
                case BuffType.Armor:
                    _owner.Armor -= buff.TotalValue;
                    if (_owner.Armor < 0) _owner.Armor = 0;
                    break;
                case BuffType.Strength:
                    _owner.Strength -= buff.TotalValue;
                    break;
                case BuffType.Shield:
                    // 护盾消耗掉了就不返还
                    break;
                case BuffType.MaxHpBonus:
                    _owner.MaxHp -= buff.TotalValue;
                    if (_owner.Hp > _owner.MaxHp)
                        _owner.Hp = _owner.MaxHp;
                    break;
                case BuffType.Invincible:
                    if (!HasBuffType(BuffType.Invincible))
                        _owner.IsInvincible = false;
                    break;
                case BuffType.Lifesteal:
                    _owner.LifestealPercent -= buff.TotalValue;
                    if (_owner.LifestealPercent < 0) _owner.LifestealPercent = 0;
                    break;
                case BuffType.Thorns:
                    _owner.ThornsValue -= buff.TotalValue;
                    if (_owner.ThornsValue < 0) _owner.ThornsValue = 0;
                    break;
                case BuffType.DamageReduction:
                    _owner.DamageReductionPercent -= buff.TotalValue;
                    if (_owner.DamageReductionPercent < 0) _owner.DamageReductionPercent = 0;
                    break;
                case BuffType.Vulnerable:
                    _owner.VulnerableStacks -= buff.Stacks;
                    if (_owner.VulnerableStacks <= 0)
                    {
                        _owner.VulnerableStacks = 0;
                        _owner.IsVulnerable = false;
                    }
                    break;
                case BuffType.Weak:
                    _owner.WeakStacks -= buff.Stacks;
                    if (_owner.WeakStacks <= 0)
                    {
                        _owner.WeakStacks = 0;
                        _owner.IsWeak = false;
                    }
                    break;
                case BuffType.Stun:
                    if (!HasBuffType(BuffType.Stun))
                        _owner.IsStunned = false;
                    break;
                case BuffType.Silence:
                    if (!HasBuffType(BuffType.Silence))
                        _owner.IsSilenced = false;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 私有方法 —— 触发器注册/注销
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 为有触发效果的 Buff 向 TriggerManager 注册触发器。
        /// 每个 Buff 实例的 RegisteredTriggerIds 记录所有注册的 ID，
        /// 方便 RemoveBuff 时批量注销。
        /// </summary>
        private void RegisterBuffTriggers(BuffInstance buff)
        {
            if (_ctx?.TriggerManager == null) return;

            string ownerId = _owner.PlayerId;

            switch (buff.BuffType)
            {
                // ── 中毒：回合开始时结算，真实伤害，无视护盾/减免，无敌有效，层数-1后归零移除 ──
                case BuffType.Poison:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.OnRoundStart,
                        ownerPlayerId: ownerId,
                        effect: trigCtx =>
                        {
                            // 层数为 0 时（已被净化等情况）跳过
                            if (capturedBuff.Stacks <= 0) return;

                            int dmg = capturedBuff.Stacks; // 伤害 = 当前层数
                            // 通过 ApplyDot：无敌有效，无视护盾/减免，触发 AfterTakeDamage 和 OnNearDeath
                            DamageHelper.ApplyDot(
                                ctx: _ctx,
                                targetId: ownerId,
                                sourceId: capturedBuff.SourcePlayerId,
                                dotDamage: dmg,
                                dotSource: capturedBuff.BuffName
                            );

                            // 层数衰减 -1，归零时移除 Buff（触发器会随 RemoveBuff 一并注销）
                            int oldStacks = capturedBuff.Stacks;
                            capturedBuff.Stacks--;
                            OnBuffStackChanged?.Invoke(capturedBuff, oldStacks);
                            if (capturedBuff.Stacks <= 0)
                            {
                                RemoveBuff(capturedBuff);
                            }
                        },
                        triggerName: $"{buff.BuffName}_POISON_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // ── 燃烧/流血：规则待定，暂在回合结束结算，走 ApplyDot 管道（真实伤害）
                // TODO: 确认 Burn/Bleed 的结算时机、是否受减免影响、层数衰减方式
                case BuffType.Burn:
                case BuffType.Bleed:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.OnRoundEnd,
                        ownerPlayerId: ownerId,
                        effect: trigCtx =>
                        {
                            if (capturedBuff.Stacks <= 0) return;

                            int dmg = capturedBuff.TotalValue;
                            DamageHelper.ApplyDot(
                                ctx: _ctx,
                                targetId: ownerId,
                                sourceId: capturedBuff.SourcePlayerId,
                                dotDamage: dmg,
                                dotSource: capturedBuff.BuffName
                            );
                            // 注意：Burn/Bleed 的层数衰减由 OnRoundEnd 的 Buff 持续时间机制统一处理，
                            // 此处不手动衰减，等待规则确认后再调整
                        },
                        triggerName: $"{buff.BuffName}_DOT_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // ── 持续回复：回合开始时加血 ──
                case BuffType.Regeneration:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.OnRoundStart,
                        ownerPlayerId: ownerId,
                        effect: trigCtx =>
                        {
                            int heal = capturedBuff.TotalValue;
                            int oldHp = _owner.Hp;
                            _owner.Hp = Math.Min(_owner.Hp + heal, _owner.MaxHp);
                            int actualHeal = _owner.Hp - oldHp;
                            if (actualHeal > 0)
                                _ctx.RoundLog.Add(
                                    $"[BuffTrigger] {capturedBuff.BuffName}：{ownerId} 回复 {actualHeal} 点生命");
                        },
                        triggerName: $"{buff.BuffName}_REGEN_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // ── 反伤（荆棘）：受到来自卡牌的敌方伤害后，对攻击者造成固定反伤 ──
                // 触发条件：
                //   1. SourcePlayerId == ownerId —— 是自己受伤（AfterTakeDamage 中 Source = 受伤方）
                //   2. TargetPlayerId != ownerId —— 攻击者不是自己（排除自伤）
                //   3. DamageSource == CardDamage —— 来自卡牌打出的伤害（排除 DOT/燃烧/流血等持续伤害）
                case BuffType.Thorns:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.AfterTakeDamage,
                        ownerPlayerId: ownerId,
                        condition: trigCtx =>
                            trigCtx.SourcePlayerId == ownerId                          // 自己受伤
                            && trigCtx.TargetPlayerId != ownerId                       // 非自伤
                            && trigCtx.DamageSource == DamageSourceType.CardDamage,   // 来自卡牌
                        effect: trigCtx =>
                        {
                            // AfterTakeDamage 中：SourcePlayerId = 受伤方（自己），TargetPlayerId = 攻击方
                            string attackerId = trigCtx.TargetPlayerId;
                            var attacker = _ctx.GetPlayer(attackerId);
                            if (attacker == null || !attacker.IsAlive) return;

                            int reflectDmg = capturedBuff.TotalValue;
                            attacker.Hp -= reflectDmg;
                            attacker.DamageTakenThisRound += reflectDmg;
                            _ctx.RoundLog.Add(
                                $"[BuffTrigger] {capturedBuff.BuffName}：{ownerId} 对 {attackerId} 反伤 {reflectDmg} 点（来源卡牌伤害）");
                            if (attacker.Hp <= 0)
                            {
                                attacker.Hp = 0;
                                attacker.IsMarkedForDeath = true;
                            }
                        },
                        triggerName: $"{buff.BuffName}_THORNS_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // ── 吸血：造成伤害后按百分比回血 ──
                case BuffType.Lifesteal:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.AfterDealDamage,
                        ownerPlayerId: ownerId,
                        // 条件：必须是自己造成伤害才触发
                        condition: trigCtx => trigCtx.SourcePlayerId == ownerId,
                        effect: trigCtx =>
                        {
                            int dealtDamage = trigCtx.Value;
                            int healAmount = (dealtDamage * capturedBuff.TotalValue) / 100;
                            if (healAmount <= 0) return;

                            int oldHp = _owner.Hp;
                            _owner.Hp = Math.Min(_owner.Hp + healAmount, _owner.MaxHp);
                            int actualHeal = _owner.Hp - oldHp;
                            if (actualHeal > 0)
                                _ctx.RoundLog.Add(
                                    $"[BuffTrigger] {capturedBuff.BuffName}：{ownerId} 吸血回复 {actualHeal} 点生命（{capturedBuff.TotalValue}%）");
                        },
                        triggerName: $"{buff.BuffName}_LIFESTEAL_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // ── 复活：濒死时触发一次 ──
                case BuffType.Resurrection:
                {
                    var capturedBuff = buff;
                    string triggerId = _ctx.TriggerManager.RegisterTrigger(
                        timing: TriggerTiming.OnNearDeath,
                        ownerPlayerId: ownerId,
                        condition: trigCtx => trigCtx.TargetPlayerId == ownerId,
                        effect: trigCtx =>
                        {
                            _owner.Hp = Math.Max(capturedBuff.TotalValue, 1);
                            _owner.IsMarkedForDeath = false;
                            _ctx.RoundLog.Add(
                                $"[BuffTrigger] {capturedBuff.BuffName}：{ownerId} 触发复活，恢复至 {_owner.Hp} 点生命");
                            // 复活是一次性的，立即移除 Buff
                            RemoveBuff(capturedBuff);
                        },
                        remainingTriggers: 1, // 只触发一次
                        triggerName: $"{buff.BuffName}_RESURRECTION_{buff.RuntimeId}",
                        sourceId: buff.RuntimeId
                    );
                    if (triggerId != null) buff.RegisteredTriggerIds.Add(triggerId);
                    break;
                }

                // 其他类型（Armor/Shield/Stun/Silence 等）为纯属性型，
                // 在 ApplyBuffModifiers 中已处理，无需注册触发器
                default:
                    break;
            }
        }

        /// <summary>
        /// 注销 Buff 在 TriggerManager 中注册的所有触发器。
        /// </summary>
        private void UnregisterBuffTriggers(BuffInstance buff)
        {
            if (_ctx?.TriggerManager == null) return;
            if (buff.RegisteredTriggerIds.Count == 0) return;

            foreach (var triggerId in buff.RegisteredTriggerIds)
            {
                _ctx.TriggerManager.UnregisterTrigger(triggerId);
            }
            buff.RegisteredTriggerIds.Clear();
        }
    }
}