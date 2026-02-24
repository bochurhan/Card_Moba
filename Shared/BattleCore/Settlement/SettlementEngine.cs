using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 结算引擎 V2.0 —— 完全符合《定策牌结算机制 V4.0》的 4 堆叠层结算系统。
    /// 
    /// 结算顺序（永久不可颠倒）：
    /// - 堆叠0层：反制效果结算层（上回合反制牌触发校验）
    /// - 堆叠1层：防御与数值修正层（护甲、力量、易伤等）
    /// - 堆叠2层：主动伤害与触发式效果闭环层
    ///   - 步骤1：所有伤害牌同步结算
    ///   - 步骤2：触发式效果同步闭环结算
    /// - 堆叠3层：全局非依赖效果收尾层
    ///   - 子阶段1：控制/资源/支援类效果
    ///   - 子阶段2：传说特殊牌专属结算
    /// 
    /// 核心铁律：
    /// - 公平性：跨阵营效果同步结算，无先后手
    /// - 连锁封顶：触发效果不再触发新的连锁
    /// - 结算与表现解耦：先完成全量预结算，再播放动画
    /// </summary>
    public class SettlementEngine
    {
        // ── 瞬策牌结算（操作期内立即执行） ──

        /// <summary>
        /// 立即结算一张瞬策牌的效果。
        /// </summary>
        public void ResolveInstantCard(BattleContext ctx, CardAction action)
        {
            ctx.RoundLog.Add($"[瞬策牌] 玩家{action.SourcePlayerId}打出「{action.Card.CardName}」");
            ApplyCardEffects(ctx, action, isInstant: true);
        }

        // ── 定策牌统一结算入口 ──

        /// <summary>
        /// 按 4 堆叠层顺序结算本回合所有已提交的定策牌。
        /// </summary>
        public void ResolvePlanCards(BattleContext ctx)
        {
            if (ctx.PendingPlanActions.Count == 0 && ctx.PendingCounterCards.Count == 0)
                return;

            ctx.RoundLog.Add($"[Settlement] ═══ 回合{ctx.CurrentRound} 定策牌结算开始 ═══");

            // 堆叠0层：反制效果结算
            ResolveLayer0_Counter(ctx);

            // 堆叠1层：防御与数值修正
            ResolveLayer1_Defense(ctx);

            // 堆叠2层：主动伤害与触发式效果
            ResolveLayer2_Damage(ctx);

            // 堆叠3层：全局非依赖效果收尾
            ResolveLayer3_Utility(ctx);

            ctx.RoundLog.Add($"[Settlement] ═══ 回合{ctx.CurrentRound} 定策牌结算结束 ═══");
        }

        // ══════════════════════════════════════════════════════════
        // 堆叠0层：反制效果结算层
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 堆叠0层：结算上回合提交的反制牌，校验触发条件，执行无效化效果。
        /// </summary>
        private void ResolveLayer0_Counter(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer0] ── 反制效果结算层 ──");

            // 处理上回合的反制牌
            foreach (var counterAction in ctx.PendingCounterCards)
            {
                ProcessCounterCard(ctx, counterAction);
            }

            // 筛选出本回合有效的定策牌（未被反制的）
            ctx.ValidPlanActions.Clear();
            foreach (var action in ctx.PendingPlanActions)
            {
                // 跳过本回合新提交的反制牌（下回合才生效）
                if (action.Card.SubType == CardSubType.反制)
                {
                    ctx.RoundLog.Add($"[Layer0] 反制牌「{action.Card.CardName}」已锁定，下回合生效");
                    continue;
                }

                // 检查是否被反制
                bool isCountered = false;
                foreach (var countered in ctx.CounteredActions)
                {
                    if (countered == action)
                    {
                        isCountered = true;
                        break;
                    }
                }

                if (!isCountered)
                {
                    ctx.ValidPlanActions.Add(action);
                }
            }

            ctx.RoundLog.Add($"[Layer0] 有效定策牌数量: {ctx.ValidPlanActions.Count}");
        }

        /// <summary>
        /// 处理单张反制牌的效果。
        /// </summary>
        private void ProcessCounterCard(BattleContext ctx, CardAction counterAction)
        {
            var source = ctx.GetPlayer(counterAction.SourcePlayerId);
            if (source == null || !source.IsAlive) return;

            // 获取目标玩家（反制牌作用于敌方）
            var target = ctx.GetPlayer(counterAction.TargetPlayerId);
            if (target == null) return;

            // 查找目标玩家本回合提交的首张伤害牌
            CardAction? targetCard = FindFirstDamageCard(ctx, target.PlayerId);

            if (targetCard != null)
            {
                // 反制成功：将目标卡牌标记为作废
                ctx.CounteredActions.Add(targetCard);
                ctx.RoundLog.Add($"[Layer0] 玩家{source.PlayerId}的反制牌「{counterAction.Card.CardName}」" +
                    $"成功反制了玩家{target.PlayerId}的「{targetCard.Card.CardName}」！");

                // 执行反制牌的额外效果（如反伤）
                ApplyCounterEffects(ctx, counterAction, targetCard);
            }
            else
            {
                ctx.RoundLog.Add($"[Layer0] 玩家{source.PlayerId}的反制牌「{counterAction.Card.CardName}」" +
                    $"未找到可反制的目标，效果落空");
            }
        }

        /// <summary>
        /// 查找指定玩家本回合提交的首张伤害牌。
        /// </summary>
        private CardAction? FindFirstDamageCard(BattleContext ctx, int playerId)
        {
            foreach (var action in ctx.PendingPlanActions)
            {
                if (action.SourcePlayerId == playerId && action.Card.SubType == CardSubType.伤害)
                {
                    return action;
                }
            }
            return null;
        }

        /// <summary>
        /// 应用反制牌的额外效果（如反伤）。
        /// </summary>
        private void ApplyCounterEffects(BattleContext ctx, CardAction counterAction, CardAction targetCard)
        {
            foreach (var effect in counterAction.Card.Effects)
            {
                if (effect.EffectType == EffectType.反制并反弹)
                {
                    // 反弹伤害给原攻击者
                    var attacker = ctx.GetPlayer(targetCard.SourcePlayerId);
                    if (attacker != null && attacker.IsAlive)
                    {
                        int reflectDamage = targetCard.Card.EffectValue;
                        attacker.Hp -= reflectDamage;
                        ctx.RoundLog.Add($"[Layer0] 反弹{reflectDamage}点伤害给玩家{attacker.PlayerId}");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 堆叠1层：防御与数值修正层
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 堆叠1层：结算所有不依赖伤害结果的前置效果。
        /// 包括：护甲、护盾、伤害减免、力量增减、易伤、虚弱等。
        /// </summary>
        private void ResolveLayer1_Defense(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer1] ── 防御与数值修正层 ──");

            // 收集所有属于堆叠1层的效果
            List<EffectToResolve> layer1Effects = new List<EffectToResolve>();

            foreach (var action in ctx.ValidPlanActions)
            {
                var source = ctx.GetPlayer(action.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                foreach (var effect in action.Card.Effects)
                {
                    if (effect.GetSettlementLayer() == SettlementLayer.防御数值层)
                    {
                        layer1Effects.Add(new EffectToResolve
                        {
                            Action = action,
                            Effect = effect,
                            Source = source
                        });
                    }
                }
            }

            // 同步应用所有堆叠1层效果
            foreach (var item in layer1Effects)
            {
                ApplySingleEffect(ctx, item.Action, item.Effect, item.Source);
            }

            ctx.RoundLog.Add($"[Layer1] 处理了 {layer1Effects.Count} 个防御/数值修正效果");
        }

        // ══════════════════════════════════════════════════════════
        // 堆叠2层：主动伤害与触发式效果闭环层
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 堆叠2层：主动伤害与触发式效果闭环结算。
        /// </summary>
        private void ResolveLayer2_Damage(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer2] ── 主动伤害与触发式效果层 ──");

            // 步骤1：收集并同步结算所有主动伤害
            ResolveLayer2_Step1_Damage(ctx);

            // 步骤2：同步结算所有触发式效果
            ResolveLayer2_Step2_Triggers(ctx);
        }

        /// <summary>
        /// 堆叠2层-步骤1：所有伤害牌同步、一次性结算。
        /// </summary>
        private void ResolveLayer2_Step1_Damage(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer2-Step1] 主动伤害同步结算");

            // 收集所有伤害效果
            List<DamageToApply> damages = new List<DamageToApply>();

            foreach (var action in ctx.ValidPlanActions)
            {
                var source = ctx.GetPlayer(action.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                var target = ctx.GetPlayer(action.TargetPlayerId);
                if (target == null) continue;

                foreach (var effect in action.Card.Effects)
                {
                    if (effect.EffectType == EffectType.造成伤害)
                    {
                        // 计算最终伤害值（考虑力量、虚弱）
                        int baseDamage = effect.Value;
                        int finalDamage = source.CalculateOutgoingDamage(baseDamage);

                        damages.Add(new DamageToApply
                        {
                            SourceId = source.PlayerId,
                            TargetId = target.PlayerId,
                            BaseDamage = baseDamage,
                            FinalDamage = finalDamage,
                            CardName = action.Card.CardName,
                            HasLifesteal = HasLifestealEffect(action.Card)
                        });
                    }
                }
            }

            // 同步应用所有伤害（先计算，后统一应用）
            Dictionary<int, int> hpChanges = new Dictionary<int, int>();
            Dictionary<int, int> shieldChanges = new Dictionary<int, int>();

            foreach (var player in ctx.Players)
            {
                hpChanges[player.PlayerId] = 0;
                shieldChanges[player.PlayerId] = 0;
            }

            foreach (var dmg in damages)
            {
                var target = ctx.GetPlayer(dmg.TargetId);
                if (target == null || target.IsMarkedForDeath) continue;

                // 计算目标实际受到的伤害（考虑护甲、易伤、伤害减免）
                int actualDamage = target.CalculateIncomingDamage(dmg.FinalDamage);

                // 先扣护盾
                int currentShield = target.Shield + shieldChanges[target.PlayerId];
                if (currentShield > 0)
                {
                    int shieldAbsorb = currentShield >= actualDamage ? actualDamage : currentShield;
                    shieldChanges[target.PlayerId] -= shieldAbsorb;
                    actualDamage -= shieldAbsorb;
                    if (shieldAbsorb > 0)
                    {
                        ctx.RoundLog.Add($"[Layer2-Step1] 「{dmg.CardName}」被护盾吸收{shieldAbsorb}点伤害");
                    }
                }

                // 再扣血
                if (actualDamage > 0)
                {
                    hpChanges[target.PlayerId] -= actualDamage;
                    target.DamageTakenThisRound += actualDamage;

                    var source = ctx.GetPlayer(dmg.SourceId);
                    if (source != null)
                    {
                        source.DamageDealtThisRound += actualDamage;

                        // 如果有吸血效果，添加到待触发列表
                        if (dmg.HasLifesteal)
                        {
                            ctx.PendingTriggerEffects.Add(new PendingTriggerEffect
                            {
                                SourcePlayerId = dmg.SourceId,
                                TargetPlayerId = dmg.SourceId, // 吸血回复自己
                                EffectType = EffectType.吸血,
                                Value = actualDamage,
                                TriggerReason = $"「{dmg.CardName}」吸血"
                            });
                        }
                    }
                }

                ctx.RoundLog.Add($"[Layer2-Step1] 玩家{dmg.SourceId}的「{dmg.CardName}」对玩家{dmg.TargetId}造成{actualDamage}点伤害");
            }

            // 统一应用所有变化
            foreach (var player in ctx.Players)
            {
                int pid = player.PlayerId;

                if (shieldChanges[pid] != 0)
                {
                    player.Shield += shieldChanges[pid];
                    if (player.Shield < 0) player.Shield = 0;
                }

                if (hpChanges[pid] != 0)
                {
                    player.Hp += hpChanges[pid];
                    if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;

                    // 标记濒死状态（暂不执行死亡判定）
                    if (player.Hp <= 0)
                    {
                        player.Hp = 0;
                        player.IsMarkedForDeath = true;
                        ctx.RoundLog.Add($"[Layer2-Step1] 玩家{pid}被标记为濒死状态");
                    }
                }
            }
        }

        /// <summary>
        /// 检查卡牌是否有吸血效果。
        /// </summary>
        private bool HasLifestealEffect(CardConfig card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.EffectType == EffectType.吸血)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 堆叠2层-步骤2：触发式效果同步闭环结算。
        /// </summary>
        private void ResolveLayer2_Step2_Triggers(BattleContext ctx)
        {
            if (ctx.PendingTriggerEffects.Count == 0)
                return;

            ctx.RoundLog.Add("[Layer2-Step2] 触发式效果同步结算");

            // 连锁封顶检查
            if (ctx.HasChainTriggeredThisRound)
            {
                ctx.RoundLog.Add("[Layer2-Step2] 连锁已触发过，跳过后续连锁");
                ctx.PendingTriggerEffects.Clear();
                return;
            }

            ctx.HasChainTriggeredThisRound = true;

            // 同步处理所有触发效果
            foreach (var trigger in ctx.PendingTriggerEffects)
            {
                var source = ctx.GetPlayer(trigger.SourcePlayerId);
                var target = ctx.GetPlayer(trigger.TargetPlayerId);

                if (source == null || target == null) continue;

                switch (trigger.EffectType)
                {
                    case EffectType.吸血:
                        int healAmount = trigger.Value;
                        target.Hp += healAmount;
                        if (target.Hp > target.MaxHp) target.Hp = target.MaxHp;
                        ctx.RoundLog.Add($"[Layer2-Step2] {trigger.TriggerReason}，玩家{target.PlayerId}回复{healAmount}点生命");
                        break;

                    case EffectType.反伤:
                        int reflectDamage = trigger.Value;
                        target.Hp -= reflectDamage;
                        if (target.Hp < 0) target.Hp = 0;
                        ctx.RoundLog.Add($"[Layer2-Step2] {trigger.TriggerReason}，玩家{target.PlayerId}受到{reflectDamage}点反伤");
                        break;

                    case EffectType.受击获得护甲:
                        target.Armor += trigger.Value;
                        ctx.RoundLog.Add($"[Layer2-Step2] {trigger.TriggerReason}，玩家{target.PlayerId}获得{trigger.Value}点护甲");
                        break;
                }
            }

            ctx.PendingTriggerEffects.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // 堆叠3层：全局非依赖效果收尾层
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 堆叠3层：结算所有不依赖本回合结算结果的后置效果。
        /// </summary>
        private void ResolveLayer3_Utility(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer3] ── 全局非依赖效果收尾层 ──");

            // 子阶段1：普通效果（控制、资源、支援）
            ResolveLayer3_SubPhase1_Normal(ctx);

            // 子阶段2：传说特殊牌
            ResolveLayer3_SubPhase2_Legendary(ctx);
        }

        /// <summary>
        /// 堆叠3层-子阶段1：普通效果结算。
        /// </summary>
        private void ResolveLayer3_SubPhase1_Normal(BattleContext ctx)
        {
            foreach (var action in ctx.ValidPlanActions)
            {
                if (action.Card.IsLegendary) continue; // 传说牌在子阶段2处理

                var source = ctx.GetPlayer(action.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                foreach (var effect in action.Card.Effects)
                {
                    if (effect.GetSettlementLayer() == SettlementLayer.收尾效果层)
                    {
                        ApplySingleEffect(ctx, action, effect, source);
                    }
                }
            }
        }

        /// <summary>
        /// 堆叠3层-子阶段2：传说特殊牌专属结算。
        /// </summary>
        private void ResolveLayer3_SubPhase2_Legendary(BattleContext ctx)
        {
            foreach (var action in ctx.ValidPlanActions)
            {
                if (!action.Card.IsLegendary) continue;

                var source = ctx.GetPlayer(action.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                ctx.RoundLog.Add($"[Layer3-Legendary] 传说牌「{action.Card.CardName}」专属结算");

                foreach (var effect in action.Card.Effects)
                {
                    ApplySingleEffect(ctx, action, effect, source);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 通用效果应用方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 应用卡牌的所有效果（用于瞬策牌）。
        /// </summary>
        private void ApplyCardEffects(BattleContext ctx, CardAction action, bool isInstant)
        {
            var source = ctx.GetPlayer(action.SourcePlayerId);
            if (source == null) return;

            foreach (var effect in action.Card.Effects)
            {
                ApplySingleEffect(ctx, action, effect, source);
            }
        }

        /// <summary>
        /// 应用单个效果。
        /// </summary>
        private void ApplySingleEffect(BattleContext ctx, CardAction action, CardEffect effect, PlayerBattleState source)
        {
            var target = ctx.GetPlayer(action.TargetPlayerId);

            switch (effect.EffectType)
            {
                // ── 防御与数值修正（堆叠1层） ──
                case EffectType.获得护甲:
                    source.Armor += effect.Value;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}获得{effect.Value}点护甲");
                    break;

                case EffectType.获得护盾:
                    source.Shield += effect.Value;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}获得{effect.Value}点护盾");
                    break;

                case EffectType.增加力量:
                    source.Strength += effect.Value;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}力量+{effect.Value}");
                    break;

                case EffectType.降低力量:
                    if (target != null)
                    {
                        target.Strength -= effect.Value;
                        ctx.RoundLog.Add($"[Effect] 玩家{target.PlayerId}力量-{effect.Value}");
                    }
                    break;

                case EffectType.易伤:
                    if (target != null)
                    {
                        target.VulnerableStacks += effect.Value;
                        ctx.RoundLog.Add($"[Effect] 玩家{target.PlayerId}获得{effect.Value}层易伤");
                    }
                    break;

                case EffectType.虚弱:
                    if (target != null)
                    {
                        target.WeakStacks += effect.Value;
                        ctx.RoundLog.Add($"[Effect] 玩家{target.PlayerId}获得{effect.Value}层虚弱");
                    }
                    break;

                case EffectType.伤害减免:
                    source.DamageReductionPercent += effect.Value;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}获得{effect.Value}%伤害减免");
                    break;

                case EffectType.无敌:
                    source.IsInvincible = true;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}获得无敌状态");
                    break;

                // ── 伤害（堆叠2层，通常在Step1处理，这里作为备用） ──
                case EffectType.造成伤害:
                    if (target != null && target.IsAlive)
                    {
                        int damage = source.CalculateOutgoingDamage(effect.Value);
                        int actualDamage = target.CalculateIncomingDamage(damage);
                        target.Hp -= actualDamage;
                        if (target.Hp < 0) target.Hp = 0;
                        ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}对玩家{target.PlayerId}造成{actualDamage}点伤害");
                    }
                    break;

                // ── 控制效果（堆叠3层） ──
                case EffectType.沉默:
                    if (target != null)
                    {
                        target.IsSilenced = true;
                        target.SilencedRounds = effect.Duration > 0 ? effect.Duration : 1;
                        ctx.RoundLog.Add($"[Effect] 玩家{target.PlayerId}被沉默{target.SilencedRounds}回合");
                    }
                    break;

                case EffectType.眩晕:
                    if (target != null)
                    {
                        target.IsStunned = true;
                        target.StunnedRounds = effect.Duration > 0 ? effect.Duration : 1;
                        ctx.RoundLog.Add($"[Effect] 玩家{target.PlayerId}被眩晕{target.StunnedRounds}回合");
                    }
                    break;

                // ── 资源效果（堆叠3层） ──
                case EffectType.抽牌:
                    // TODO: 实现抽牌逻辑
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}抽{effect.Value}张牌");
                    break;

                case EffectType.回复能量:
                    source.Energy += effect.Value;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}回复{effect.Value}点能量");
                    break;

                case EffectType.回复生命:
                    source.Hp += effect.Value;
                    if (source.Hp > source.MaxHp) source.Hp = source.MaxHp;
                    ctx.RoundLog.Add($"[Effect] 玩家{source.PlayerId}回复{effect.Value}点生命");
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 辅助数据结构
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 待结算的效果信息。
        /// </summary>
        private class EffectToResolve
        {
            public CardAction Action { get; set; } = null!;
            public CardEffect Effect { get; set; } = null!;
            public PlayerBattleState Source { get; set; } = null!;
        }

        /// <summary>
        /// 待应用的伤害信息。
        /// </summary>
        private class DamageToApply
        {
            public int SourceId { get; set; }
            public int TargetId { get; set; }
            public int BaseDamage { get; set; }
            public int FinalDamage { get; set; }
            public string CardName { get; set; } = string.Empty;
            public bool HasLifesteal { get; set; }
        }
    }
}
