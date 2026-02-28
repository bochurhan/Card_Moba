using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Random;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Buff
{
    /// <summary>
    /// Buff 管理器 —— 负责管理单个玩家的所有 Buff。
    /// 
    /// 职责：
    /// - 添加/移除/查询 Buff
    /// - 处理 Buff 叠加规则
    /// - 触发时机回调
    /// - 回合结束时衰减持续时间
    /// </summary>
    public class BuffManager
    {
        private readonly PlayerBattleState _owner;
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

        public BuffManager(PlayerBattleState owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 添加一个 Buff。
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

            // 检查是否已存在同类型 Buff
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].BuffId == buff.BuffId)
                {
                    // 尝试叠加
                    int oldStacks = _buffs[i].Stacks;
                    if (_buffs[i].TryStack(buff))
                    {
                        if (_buffs[i].Stacks != oldStacks)
                        {
                            OnBuffStackChanged?.Invoke(_buffs[i], oldStacks);
                        }
                        ApplyBuffModifiers(_buffs[i]);
                        return _buffs[i];
                    }
                    // 叠加失败（Independent 规则），继续添加新的
                }
            }

            // 添加新 Buff
            _buffs.Add(buff);
            ApplyBuffModifiers(buff);
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
        /// 移除指定 Buff。
        /// </summary>
        /// <param name="buff">要移除的 Buff</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveBuff(BuffInstance buff)
        {
            if (buff == null) return false;

            int index = _buffs.IndexOf(buff);
            if (index >= 0)
            {
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
                // 应该完全移除
                return RemoveBuff(buff);
            }
            else
            {
                // 还有剩余层数
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
        /// 回合开始时触发。
        /// </summary>
        public void OnRoundStart(BattleContext ctx)
        {
            // 触发回合开始效果
            var roundStartBuffs = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.TriggerTiming == BuffTriggerTiming.OnRoundStart)
                    roundStartBuffs.Add(buff);
            }

            foreach (var buff in roundStartBuffs)
            {
                TriggerBuffEffect(buff, ctx);
            }
        }

        /// <summary>
        /// 回合结束时触发，处理持续时间衰减。
        /// </summary>
        public void OnRoundEnd(BattleContext ctx)
        {
            // 触发回合结束效果
            var roundEndBuffs = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.TriggerTiming == BuffTriggerTiming.OnRoundEnd)
                    roundEndBuffs.Add(buff);
            }

            foreach (var buff in roundEndBuffs)
            {
                TriggerBuffEffect(buff, ctx);
            }

            // 衰减持续时间
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.IsPermanent) continue;

                buff.RemainingRounds--;
                if (buff.RemainingRounds <= 0)
                {
                    RemoveBuffModifiers(buff);
                    OnBuffRemoved?.Invoke(buff);
                    _buffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 受到伤害时触发。
        /// </summary>
        public void OnDamageTaken(BattleContext ctx, int damage, string attackerId)
        {
            var triggeredBuffs = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.TriggerTiming == BuffTriggerTiming.OnDamageTaken)
                    triggeredBuffs.Add(buff);
            }

            foreach (var buff in triggeredBuffs)
            {
                TriggerBuffEffect(buff, ctx, damage, attackerId);
            }
        }

        /// <summary>
        /// 造成伤害时触发。
        /// </summary>
        public void OnDamageDealt(BattleContext ctx, int damage, string targetId)
        {
            var triggeredBuffs = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.TriggerTiming == BuffTriggerTiming.OnDamageDealt)
                    triggeredBuffs.Add(buff);
            }

            foreach (var buff in triggeredBuffs)
            {
                TriggerBuffEffect(buff, ctx, damage, targetId);
            }
        }

        /// <summary>
        /// 濒死时触发。
        /// </summary>
        public void OnNearDeath(BattleContext ctx)
        {
            var triggeredBuffs = new List<BuffInstance>();
            foreach (var buff in _buffs)
            {
                if (buff.TriggerTiming == BuffTriggerTiming.OnNearDeath)
                    triggeredBuffs.Add(buff);
            }

            foreach (var buff in triggeredBuffs)
            {
                TriggerBuffEffect(buff, ctx);
            }
        }

        /// <summary>
        /// 清除所有 Buff。
        /// </summary>
        public void ClearAllBuffs()
        {
            foreach (var buff in _buffs)
            {
                RemoveBuffModifiers(buff);
                OnBuffRemoved?.Invoke(buff);
            }
            _buffs.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // 私有方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 应用 Buff 对玩家属性的修改。
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
                    _owner.Hp += buff.TotalValue; // 同时增加当前 HP
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
        /// 移除 Buff 对玩家属性的修改。
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
                    // 检查是否还有其他无敌 Buff
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
                    // 检查是否还有其他眩晕 Buff
                    if (!HasBuffType(BuffType.Stun))
                        _owner.IsStunned = false;
                    break;
                case BuffType.Silence:
                    // 检查是否还有其他沉默 Buff
                    if (!HasBuffType(BuffType.Silence))
                        _owner.IsSilenced = false;
                    break;
            }
        }

        /// <summary>
        /// 触发 Buff 的效果（如中毒、灼烧等持续伤害）。
        /// </summary>
        private void TriggerBuffEffect(BuffInstance buff, BattleContext ctx, int contextValue = 0, string contextId = null)
        {
            // 检查触发次数限制
            if (buff.MaxTriggerCount > 0 && buff.TriggerCount >= buff.MaxTriggerCount)
                return;

            buff.TriggerCount++;

            switch (buff.BuffType)
            {
                case BuffType.Regeneration:
                    // 回合开始恢复生命
                    _owner.Hp += buff.TotalValue;
                    if (_owner.Hp > _owner.MaxHp)
                        _owner.Hp = _owner.MaxHp;
                    break;

                case BuffType.Poison:
                case BuffType.Burn:
                case BuffType.Bleed:
                    // 回合开始/结束受到伤害
                    _owner.Hp -= buff.TotalValue;
                    _owner.DamageTakenThisRound += buff.TotalValue;
                    if (_owner.Hp <= 0)
                        _owner.IsMarkedForDeath = true;
                    break;

                case BuffType.Thorns:
                    // 受到伤害时反弹
                    if (buff.TriggerTiming == BuffTriggerTiming.OnDamageTaken && !string.IsNullOrEmpty(contextId))
                    {
                        var attacker = ctx.GetPlayerState(contextId);
                        if (attacker != null)
                        {
                            int reflectDamage = buff.TotalValue;
                            attacker.Hp -= reflectDamage;
                            attacker.DamageTakenThisRound += reflectDamage;
                        }
                    }
                    break;

                case BuffType.Lifesteal:
                    // 造成伤害时吸血
                    if (buff.TriggerTiming == BuffTriggerTiming.OnDamageDealt)
                    {
                        int healAmount = (contextValue * buff.TotalValue) / 100;
                        _owner.Hp += healAmount;
                        if (_owner.Hp > _owner.MaxHp)
                            _owner.Hp = _owner.MaxHp;
                    }
                    break;

                case BuffType.Resurrection:
                    // 濒死时复活
                    if (buff.TriggerTiming == BuffTriggerTiming.OnNearDeath)
                    {
                        _owner.Hp = buff.TotalValue;
                        _owner.IsMarkedForDeath = false;
                        RemoveBuff(buff); // 复活后移除
                    }
                    break;
            }
        }
    }
}
