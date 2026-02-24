using System.Collections.Generic;
using CardMoba.ConfigModels.Card;

namespace CardMoba.BattleCore.Context
{
    /// <summary>
    /// 玩家对局状态 —— 一个玩家在一局对战中的所有可变数据。
    /// 
    /// 扩展支持《定策牌结算机制 V4.0》所需的属性：
    /// - 力量/易伤/虚弱等数值修正
    /// - Buff/Debuff 状态追踪
    /// - 本回合伤害统计（用于触发式效果）
    /// </summary>
    public class PlayerBattleState
    {
        /// <summary>玩家ID（用于区分不同玩家）</summary>
        public int PlayerId { get; set; }

        /// <summary>玩家显示名称</summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>所属队伍ID（0 或 1）</summary>
        public int TeamId { get; set; }

        // ── 生存属性 ──

        /// <summary>当前生命值</summary>
        public int Hp { get; set; }

        /// <summary>最大生命值</summary>
        public int MaxHp { get; set; }

        /// <summary>当前护盾值（受到伤害时优先扣护盾）</summary>
        public int Shield { get; set; }

        /// <summary>当前护甲值（减少受到的伤害，固定值减免）</summary>
        public int Armor { get; set; }

        // ── 战斗属性（堆叠1层锁定） ──

        /// <summary>力量值（增加造成的伤害，可为负数表示虚弱）</summary>
        public int Strength { get; set; }

        /// <summary>易伤层数（每层增加受到的伤害 50%）</summary>
        public int VulnerableStacks { get; set; }

        /// <summary>虚弱层数（每层减少造成的伤害 25%）</summary>
        public int WeakStacks { get; set; }

        /// <summary>伤害减免百分比（0-100）</summary>
        public int DamageReductionPercent { get; set; }

        /// <summary>是否无敌（完全免疫伤害）</summary>
        public bool IsInvincible { get; set; }

        // ── 资源属性 ──

        /// <summary>当前能量（每回合恢复，出牌消耗）</summary>
        public int Energy { get; set; }

        /// <summary>每回合能量恢复量</summary>
        public int EnergyPerRound { get; set; }

        // ── 卡牌区域 ──

        /// <summary>手牌（玩家当前持有的卡牌）</summary>
        public List<CardConfig> Hand { get; set; } = new List<CardConfig>();

        /// <summary>牌库（还没摸到的牌，摸牌时从这里抽）</summary>
        public List<CardConfig> Deck { get; set; } = new List<CardConfig>();

        /// <summary>弃牌堆（已打出/已丢弃的牌）</summary>
        public List<CardConfig> DiscardPile { get; set; } = new List<CardConfig>();

        // ── 状态标记 ──

        /// <summary>本回合是否已锁定操作（锁定后不能再出牌）</summary>
        public bool IsLocked { get; set; }

        /// <summary>玩家是否存活</summary>
        public bool IsAlive => Hp > 0;

        /// <summary>是否被沉默（无法使用技能牌）</summary>
        public bool IsSilenced { get; set; }

        /// <summary>沉默剩余回合数</summary>
        public int SilencedRounds { get; set; }

        /// <summary>是否被眩晕（跳过操作回合）</summary>
        public bool IsStunned { get; set; }

        /// <summary>眩晕剩余回合数</summary>
        public int StunnedRounds { get; set; }

        // ── 本回合统计（用于触发式效果） ──

        /// <summary>本回合已造成的总伤害（用于吸血计算）</summary>
        public int DamageDealtThisRound { get; set; }

        /// <summary>本回合已受到的总伤害（用于反伤、受击效果）</summary>
        public int DamageTakenThisRound { get; set; }

        /// <summary>本回合是否击杀了敌人</summary>
        public bool HasKilledThisRound { get; set; }

        /// <summary>本回合是否被标记为濒死（HP<=0 但还未正式判定死亡）</summary>
        public bool IsMarkedForDeath { get; set; }

        // ── Buff/Debuff 列表 ──

        /// <summary>当前生效的 Buff 列表</summary>
        public List<BuffInstance> ActiveBuffs { get; set; } = new List<BuffInstance>();

        // ── 便捷方法 ──

        /// <summary>
        /// 计算实际造成的伤害（考虑力量和虚弱）。
        /// </summary>
        /// <param name="baseDamage">基础伤害值</param>
        /// <returns>最终伤害值</returns>
        public int CalculateOutgoingDamage(int baseDamage)
        {
            // 加上力量
            int damage = baseDamage + Strength;

            // 应用虚弱（每层减少 25%）
            if (WeakStacks > 0)
            {
                float weakMultiplier = 1f - (WeakStacks * 0.25f);
                if (weakMultiplier < 0) weakMultiplier = 0;
                damage = (int)(damage * weakMultiplier);
            }

            return damage > 0 ? damage : 0;
        }

        /// <summary>
        /// 计算实际受到的伤害（考虑护甲、易伤、伤害减免）。
        /// </summary>
        /// <param name="incomingDamage">传入伤害值</param>
        /// <param name="ignoreArmor">是否无视护甲（穿透）</param>
        /// <returns>最终受到的伤害值</returns>
        public int CalculateIncomingDamage(int incomingDamage, bool ignoreArmor = false)
        {
            if (IsInvincible) return 0;

            int damage = incomingDamage;

            // 应用易伤（每层增加 50%）
            if (VulnerableStacks > 0)
            {
                float vulnMultiplier = 1f + (VulnerableStacks * 0.5f);
                damage = (int)(damage * vulnMultiplier);
            }

            // 应用护甲（固定值减免）
            if (!ignoreArmor && Armor > 0)
            {
                damage -= Armor;
            }

            // 应用伤害减免百分比
            if (DamageReductionPercent > 0)
            {
                float reductionMultiplier = 1f - (DamageReductionPercent / 100f);
                damage = (int)(damage * reductionMultiplier);
            }

            return damage > 0 ? damage : 0;
        }

        /// <summary>
        /// 重置回合统计数据（在回合开始时调用）。
        /// </summary>
        public void ResetRoundStats()
        {
            DamageDealtThisRound = 0;
            DamageTakenThisRound = 0;
            HasKilledThisRound = false;
            IsMarkedForDeath = false;
        }

        /// <summary>
        /// 处理回合开始时的状态更新（Buff 持续时间、控制效果等）。
        /// </summary>
        public void OnRoundStart()
        {
            ResetRoundStats();

            // 处理沉默
            if (IsSilenced && SilencedRounds > 0)
            {
                SilencedRounds--;
                if (SilencedRounds <= 0)
                    IsSilenced = false;
            }

            // 处理眩晕
            if (IsStunned && StunnedRounds > 0)
            {
                StunnedRounds--;
                if (StunnedRounds <= 0)
                    IsStunned = false;
            }

            // 处理 Buff 列表
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuffs[i].RemainingRounds--;
                if (ActiveBuffs[i].RemainingRounds <= 0)
                {
                    // 移除效果并从列表中删除
                    RemoveBuffEffect(ActiveBuffs[i]);
                    ActiveBuffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 添加一个 Buff。
        /// </summary>
        public void AddBuff(BuffInstance buff)
        {
            // 检查是否已有同名 Buff
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].BuffId == buff.BuffId)
                {
                    // 同名 Buff：取最高值（或叠加，根据 Buff 类型决定）
                    if (buff.Value > ActiveBuffs[i].Value)
                        ActiveBuffs[i].Value = buff.Value;
                    if (buff.RemainingRounds > ActiveBuffs[i].RemainingRounds)
                        ActiveBuffs[i].RemainingRounds = buff.RemainingRounds;
                    return;
                }
            }

            // 新增 Buff
            ActiveBuffs.Add(buff);
            ApplyBuffEffect(buff);
        }

        private void ApplyBuffEffect(BuffInstance buff)
        {
            // 根据 Buff 类型应用效果
            // 这里可以扩展更多类型
        }

        private void RemoveBuffEffect(BuffInstance buff)
        {
            // 根据 Buff 类型移除效果
            // 这里可以扩展更多类型
        }
    }

    /// <summary>
    /// Buff 实例 —— 表示一个正在生效的增益/减益效果。
    /// </summary>
    public class BuffInstance
    {
        /// <summary>Buff 唯一标识（用于判断同名叠加）</summary>
        public string BuffId { get; set; } = string.Empty;

        /// <summary>Buff 显示名称</summary>
        public string BuffName { get; set; } = string.Empty;

        /// <summary>效果数值</summary>
        public int Value { get; set; }

        /// <summary>剩余回合数（0 表示永久）</summary>
        public int RemainingRounds { get; set; }

        /// <summary>来源玩家ID</summary>
        public int SourcePlayerId { get; set; }

        /// <summary>是否为增益（false 表示减益）</summary>
        public bool IsBuff { get; set; } = true;
    }
}