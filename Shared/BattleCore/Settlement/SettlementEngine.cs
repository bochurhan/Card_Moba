using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 结算引擎 —— 负责执行卡牌效果，改变 BattleContext 中的状态。
    /// 
    /// 核心规则：
    /// - 瞬策牌：打出后立即调用 ResolveInstantCard() 结算
    /// - 定策牌：操作期内暗置，回合末调用 ResolvePlanCards() 按优先级统一结算
    /// - 结算优先级（高→低）：反制型 → 防御型 → 功能型 → 伤害型
    /// 
    /// 设计原则：纯函数式思维，所有状态变更都通过 BattleContext，无副作用。
    /// </summary>
    public class SettlementEngine
    {
        // ── 瞬策牌结算 ──

        /// <summary>
        /// 立即结算一张瞬策牌的效果。
        /// 在操作期内，玩家每打出一张瞬策牌就调用此方法。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="action">出牌操作</param>
        public void ResolveInstantCard(BattleContext ctx, CardAction action)
        {
            ApplyCardEffect(ctx, action);
        }

        // ── 定策牌统一结算 ──

        /// <summary>
        /// 按优先级分批结算本回合所有已提交的定策牌。
        /// 
        /// 核心规则："同优先级同时结算"
        /// - 按优先级从高到低分批：反制型 → 防御型 → 功能型 → 伤害型
        /// - 同一批内的所有牌"同时生效"：先收集所有效果变化量，再统一应用
        /// - 这样双方同时打出伤害牌时，不会因为结算顺序导致一方先死、另一方的牌失效
        /// 
        /// 不同优先级之间仍然有先后顺序（反制先于伤害），这是设计意图：
        /// 防御/反制牌应该在伤害牌之前生效，这样"先防后攻"才有策略意义。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        public void ResolvePlanCards(BattleContext ctx)
        {
            if (ctx.PendingPlanActions.Count == 0) return;

            // 按优先级排序
            List<CardAction> sorted = new List<CardAction>(ctx.PendingPlanActions);
            sorted.Sort((a, b) => GetPriority(a.Card.SubType).CompareTo(GetPriority(b.Card.SubType)));

            // 按优先级分批处理
            int batchStart = 0;
            while (batchStart < sorted.Count)
            {
                int currentPriority = GetPriority(sorted[batchStart].Card.SubType);

                // 找到同优先级的所有牌
                int batchEnd = batchStart;
                while (batchEnd < sorted.Count && GetPriority(sorted[batchEnd].Card.SubType) == currentPriority)
                {
                    batchEnd++;
                }

                // 同优先级的牌"同时结算"
                ResolveBatchSimultaneously(ctx, sorted, batchStart, batchEnd);

                batchStart = batchEnd;
            }
        }

        /// <summary>
        /// 同时结算一批同优先级的定策牌。
        /// 
        /// 原理：先用快照记录结算前的状态，所有牌基于快照计算效果，
        /// 将效果累积到变化量中，最后统一应用变化量到实际状态。
        /// 
        /// 例：双方都打出8点伤害牌，双方都会受到8点伤害，不会因为顺序导致一方的牌失效。
        /// </summary>
        private void ResolveBatchSimultaneously(BattleContext ctx, List<CardAction> actions, int start, int end)
        {
            // 第一步：为每个玩家创建效果累积器（记录HP变化、护盾变化）
            Dictionary<int, int> hpChanges = new Dictionary<int, int>();
            Dictionary<int, int> shieldChanges = new Dictionary<int, int>();

            for (int i = 0; i < ctx.Players.Count; i++)
            {
                hpChanges[ctx.Players[i].PlayerId] = 0;
                shieldChanges[ctx.Players[i].PlayerId] = 0;
            }

            // 第二步：逐张计算效果，累积到变化量中（不立即修改实际状态）
            for (int i = start; i < end; i++)
            {
                CardAction action = actions[i];
                PlayerBattleState? source = ctx.GetPlayer(action.SourcePlayerId);

                // 检查出牌者是否在本批次开始前就已经阵亡（被更高优先级的批次击杀）
                if (source == null || !source.IsAlive)
                {
                    ctx.RoundLog.Add($"[Settlement] 玩家{action.SourcePlayerId}已阵亡，定策牌「{action.Card.CardName}」失效");
                    continue;
                }

                CalcCardEffect(ctx, action, hpChanges, shieldChanges);
            }

            // 第三步：统一应用所有变化量到实际状态
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                int pid = p.PlayerId;

                // 先应用护盾变化（护盾只增不减，伤害在HP变化中已计算）
                if (shieldChanges[pid] != 0)
                {
                    p.Shield += shieldChanges[pid];
                    if (p.Shield < 0) p.Shield = 0;
                }

                // 再应用HP变化
                if (hpChanges[pid] != 0)
                {
                    p.Hp += hpChanges[pid];
                    if (p.Hp > p.MaxHp) p.Hp = p.MaxHp;
                    if (p.Hp < 0) p.Hp = 0;
                }
            }
        }

        /// <summary>
        /// 计算单张定策牌的效果变化量（不直接修改状态，而是累积到变化字典中）。
        /// </summary>
        private void CalcCardEffect(BattleContext ctx, CardAction action,
            Dictionary<int, int> hpChanges, Dictionary<int, int> shieldChanges)
        {
            CardConfig card = action.Card;
            PlayerBattleState? source = ctx.GetPlayer(action.SourcePlayerId);
            PlayerBattleState? target = ctx.GetPlayer(action.TargetPlayerId);

            if (source == null) return;

            switch (card.SubType)
            {
                case CardSubType.伤害型:
                    if (target == null || !target.IsAlive) return;
                    // 伤害优先扣护盾（基于当前护盾+已累积的护盾变化）
                    int currentShield = target.Shield + shieldChanges[target.PlayerId];
                    if (currentShield < 0) currentShield = 0;
                    int damage = card.EffectValue;
                    if (currentShield > 0)
                    {
                        int shieldAbsorb = currentShield >= damage ? damage : currentShield;
                        shieldChanges[target.PlayerId] -= shieldAbsorb;
                        damage -= shieldAbsorb;
                        ctx.RoundLog.Add($"[Settlement] 「{card.CardName}」被护盾吸收了{shieldAbsorb}点伤害");
                    }
                    if (damage > 0)
                    {
                        hpChanges[target.PlayerId] -= damage;
                    }
                    ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}对玩家{target.PlayerId}使用「{card.CardName}」，造成{card.EffectValue}点伤害");
                    break;

                case CardSubType.防御型:
                    shieldChanges[source.PlayerId] += card.EffectValue;
                    ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用「{card.CardName}」，获得{card.EffectValue}点护盾");
                    break;

                case CardSubType.功能型:
                    // 原型简化：功能牌 = 回复生命
                    hpChanges[source.PlayerId] += card.EffectValue;
                    ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用「{card.CardName}」，回复{card.EffectValue}点生命");
                    break;

                case CardSubType.反制型:
                    shieldChanges[source.PlayerId] += card.EffectValue;
                    ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用反制牌「{card.CardName}」，获得{card.EffectValue}点护盾");
                    break;
            }
        }

        // ── 卡牌效果执行 ──

        /// <summary>
        /// 执行单张卡牌的效果。根据 SubType 分发到不同的处理逻辑。
        /// 这是所有卡牌效果的统一入口。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="action">出牌操作</param>
        private void ApplyCardEffect(BattleContext ctx, CardAction action)
        {
            CardConfig card = action.Card;
            PlayerBattleState? source = ctx.GetPlayer(action.SourcePlayerId);
            PlayerBattleState? target = ctx.GetPlayer(action.TargetPlayerId);

            if (source == null) return;

            switch (card.SubType)
            {
                case CardSubType.伤害型:
                    ApplyDamage(ctx, source, target, card);
                    break;

                case CardSubType.防御型:
                    ApplyShield(ctx, source, card);
                    break;

                case CardSubType.功能型:
                    ApplyFunction(ctx, source, target, card);
                    break;

                case CardSubType.反制型:
                    ApplyCounter(ctx, source, card);
                    break;
            }
        }

        /// <summary>
        /// 伤害型卡牌：对目标造成伤害（先扣护盾，再扣血）。
        /// </summary>
        private void ApplyDamage(BattleContext ctx, PlayerBattleState source, PlayerBattleState? target, CardConfig card)
        {
            if (target == null || !target.IsAlive) return;

            int damage = card.EffectValue;
            int actualDamage = 0;

            // 先扣护盾
            if (target.Shield > 0)
            {
                int shieldAbsorb = target.Shield >= damage ? damage : target.Shield;
                target.Shield -= shieldAbsorb;
                damage -= shieldAbsorb;
                ctx.RoundLog.Add($"[Settlement] 「{card.CardName}」被护盾吸收了{shieldAbsorb}点伤害");
            }

            // 再扣血
            if (damage > 0)
            {
                target.Hp -= damage;
                actualDamage = damage;
                if (target.Hp < 0) target.Hp = 0;
            }

            ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}对玩家{target.PlayerId}使用「{card.CardName}」，造成{actualDamage}点伤害（剩余HP:{target.Hp}）");
        }

        /// <summary>
        /// 防御型卡牌：给自己添加护盾。
        /// </summary>
        private void ApplyShield(BattleContext ctx, PlayerBattleState source, CardConfig card)
        {
            source.Shield += card.EffectValue;
            ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用「{card.CardName}」，获得{card.EffectValue}点护盾（当前护盾:{source.Shield}）");
        }

        /// <summary>
        /// 功能型卡牌：原型阶段简化为回复生命值。
        /// TODO: 后续扩展为 buff/debuff 系统。
        /// </summary>
        private void ApplyFunction(BattleContext ctx, PlayerBattleState source, PlayerBattleState? target, CardConfig card)
        {
            // 原型简化：功能牌 = 回复生命
            int healAmount = card.EffectValue;
            source.Hp += healAmount;
            if (source.Hp > source.MaxHp) source.Hp = source.MaxHp;

            ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用「{card.CardName}」，回复{healAmount}点生命（当前HP:{source.Hp}）");
        }

        /// <summary>
        /// 反制型卡牌：原型阶段简化为给自己加护盾 + 反弹伤害。
        /// TODO: 后续实现真正的"抵消对手卡牌效果"机制。
        /// </summary>
        private void ApplyCounter(BattleContext ctx, PlayerBattleState source, CardConfig card)
        {
            // 原型简化：反制牌 = 获得护盾（模拟"挡住对手攻击"的感觉）
            source.Shield += card.EffectValue;
            ctx.RoundLog.Add($"[Settlement] 玩家{source.PlayerId}使用反制牌「{card.CardName}」，获得{card.EffectValue}点护盾（当前护盾:{source.Shield}）");
        }

        // ── 优先级 ──

        /// <summary>
        /// 获取卡牌子类型的结算优先级（数字越小越先结算）。
        /// 顺序：反制型(1) → 防御型(2) → 功能型(3) → 伤害型(4)
        /// </summary>
        private int GetPriority(CardSubType subType)
        {
            switch (subType)
            {
                case CardSubType.反制型: return 1;
                case CardSubType.防御型: return 2;
                case CardSubType.功能型: return 3;
                case CardSubType.伤害型: return 4;
                default: return 99;
            }
        }
    }
}
