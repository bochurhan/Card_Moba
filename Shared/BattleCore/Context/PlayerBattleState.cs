using System.Collections.Generic;
using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Random;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

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
        // ══════════════════════════════════════════════════════════
        // 常量
        // ══════════════════════════════════════════════════════════

        /// <summary>最大手牌数量</summary>
        public const int MaxHandSize = 10;

        /// <summary>默认能量上限（回合开始时恢复到此值）</summary>
        public const int DefaultMaxEnergy = 3;

        // ══════════════════════════════════════════════════════════
        // 玩家基础信息
        // ══════════════════════════════════════════════════════════

        /// <summary>玩家ID（字符串，用于区分不同玩家）</summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>玩家显示名称</summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>所属队伍ID（0 或 1）</summary>
        public int TeamId { get; set; }

        /// <summary>玩家所在分路索引（0=上路, 1=中路, 2=下路, -1=决战期）</summary>
        public int LaneIndex { get; set; } = -1;

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

        /// <summary>无敌剩余回合数</summary>
        public int InvincibleRounds { get; set; }

        /// <summary>是否处于易伤状态（与 VulnerableStacks 不同，这是布尔状态）</summary>
        public bool IsVulnerable { get; set; }

        /// <summary>易伤状态剩余回合数</summary>
        public int VulnerableRounds { get; set; }

        /// <summary>是否处于虚弱状态</summary>
        public bool IsWeak { get; set; }

        /// <summary>虚弱状态剩余回合数</summary>
        public int WeakRounds { get; set; }

        /// <summary>伤害减免数值</summary>
        public int DamageReduction { get; set; }

        /// <summary>伤害减免剩余回合数</summary>
        public int DamageReductionRounds { get; set; }

        /// <summary>吸血百分比</summary>
        public int LifestealPercent { get; set; }

        /// <summary>吸血剩余回合数</summary>
        public int LifestealRounds { get; set; }

        /// <summary>反伤数值</summary>
        public int ThornsValue { get; set; }

        /// <summary>反伤剩余回合数</summary>
        public int ThornsRounds { get; set; }

        /// <summary>受击获甲数值（受到伤害时获得的护甲量）</summary>
        public int ArmorOnHitValue { get; set; }

        /// <summary>受击获甲剩余回合数</summary>
        public int ArmorOnHitRounds { get; set; }

        /// <summary>是否处于减速状态</summary>
        public bool IsSlowed { get; set; }

        /// <summary>减速剩余回合数</summary>
        public int SlowedRounds { get; set; }

        // ── 资源属性 ──

        /// <summary>当前能量（每回合恢复，出牌消耗）</summary>
        public int Energy { get; set; }

        /// <summary>
        /// 每回合能量恢复量（已废弃，使用 MaxEnergy 替代）。
        /// </summary>
        [System.Obsolete("使用 MaxEnergy 替代")]
        public int EnergyPerRound { get; set; }

        /// <summary>能量上限（回合开始时恢复到此值）</summary>
        public int MaxEnergy { get; set; } = DefaultMaxEnergy;

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
        /// 处理回合开始时的状态更新（能量恢复、Buff 持续时间、控制效果等）。
        /// </summary>
        public void OnRoundStart()
        {
            ResetRoundStats();

            // 恢复能量到上限
            Energy = MaxEnergy;

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
            // 由 BuffManager 统一管理，此处为兼容性保留
        }

        private void RemoveBuffEffect(BuffInstance buff)
        {
            // 由 BuffManager 统一管理，此处为兼容性保留
        }

        // ══════════════════════════════════════════════════════════
        // 牌组操作方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 从牌库抽牌（杀戮尖塔风格）。
        /// 如果牌库不足，自动将弃牌堆洗入牌库继续抽。
        /// 如果手牌已满，停止抽牌。
        /// </summary>
        /// <param name="count">要抽的牌数</param>
        /// <param name="random">确定性随机数生成器</param>
        /// <returns>实际抽到的牌数</returns>
        public int DrawCards(int count, SeededRandom random)
        {
            if (count <= 0) return 0;

            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                // 手牌已满，停止抽牌
                if (Hand.Count >= MaxHandSize)
                    break;

                // 牌库为空，尝试洗入弃牌堆
                if (Deck.Count == 0)
                {
                    ShuffleDiscardIntoDeck(random);
                    // 如果洗入后仍然为空，停止抽牌
                    if (Deck.Count == 0)
                        break;
                }

                // 从牌库顶抽一张牌
                var card = Deck[0];
                Deck.RemoveAt(0);
                Hand.Add(card);
                drawn++;
            }

            return drawn;
        }

        /// <summary>
        /// 将弃牌堆洗入牌库。
        /// 使用 Fisher-Yates 确定性洗牌算法。
        /// </summary>
        /// <param name="random">确定性随机数生成器</param>
        public void ShuffleDiscardIntoDeck(SeededRandom random)
        {
            if (DiscardPile.Count == 0) return;

            // 将弃牌堆所有牌加入牌库
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();

            // 使用 SeededRandom 的 Shuffle 方法确保确定性
            random.Shuffle(Deck);
        }

        /// <summary>
        /// 将一张卡牌丢弃到弃牌堆。
        /// </summary>
        /// <param name="card">要丢弃的卡牌</param>
        /// <returns>是否成功丢弃</returns>
        public bool DiscardCard(CardConfig card)
        {
            if (card == null) return false;

            // 从手牌中移除
            if (Hand.Remove(card))
            {
                DiscardPile.Add(card);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将一张卡牌从手牌打出到弃牌堆（出牌时调用）。
        /// </summary>
        /// <param name="card">打出的卡牌</param>
        /// <returns>是否成功打出</returns>
        public bool PlayCard(CardConfig card)
        {
            // 打出的牌也进入弃牌堆（与丢弃相同）
            return DiscardCard(card);
        }

        /// <summary>
        /// 随机丢弃指定数量的手牌。
        /// </summary>
        /// <param name="count">要丢弃的数量</param>
        /// <param name="random">确定性随机数生成器</param>
        /// <returns>实际丢弃的牌数</returns>
        public int DiscardRandomCards(int count, SeededRandom random)
        {
            if (count <= 0 || Hand.Count == 0) return 0;

            int toDiscard = System.Math.Min(count, Hand.Count);
            int discarded = 0;

            for (int i = 0; i < toDiscard; i++)
            {
                if (Hand.Count == 0) break;

                // 随机选择一张手牌
                int index = random.Next(Hand.Count);
                var card = Hand[index];
                Hand.RemoveAt(index);
                DiscardPile.Add(card);
                discarded++;
            }

            return discarded;
        }

        /// <summary>
        /// 获取当前手牌数量。
        /// </summary>
        public int HandCount => Hand.Count;

        /// <summary>
        /// 获取当前牌库数量。
        /// </summary>
        public int DeckCount => Deck.Count;

        /// <summary>
        /// 获取弃牌堆数量。
        /// </summary>
        public int DiscardCount => DiscardPile.Count;
    }

    // 注意：BuffInstance 已迁移到 CardMoba.BattleCore.Buff.BuffInstance
    // 此处使用 using CardMoba.BattleCore.Buff; 引用
}
