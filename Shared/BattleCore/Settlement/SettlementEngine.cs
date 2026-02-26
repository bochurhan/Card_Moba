using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Settlement.Handlers;
using CardMoba.BattleCore.Trigger;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 结算引擎 —— 基于模块化 Handler 的 4 堆叠层结算系统。
    ///
    /// 结算顺序（永久不可颠倒）：
    ///   堆叠0层：反制效果结算层（上回合反制牌触发校验）
    ///   堆叠1层：防御与数值修正层（护甲、力量、易伤等）
    ///   堆叠2层：主动伤害与触发式效果闭环层
    ///     - 步骤1：所有伤害牌同步结算
    ///     - 步骤2：触发式效果同步闭环结算
    ///   堆叠3层：全局非依赖效果收尾层
    ///     - 子阶段1：控制/资源/支援类效果
    ///     - 子阶段2：传说特殊牌专属结算
    ///
    /// 所有效果均通过 HandlerRegistry 分发，不存在备用分支。
    /// </summary>
    public class SettlementEngine
    {
        private readonly TargetResolver _targetResolver = new();

        /// <summary>
        /// 初始化结算引擎，注册所有效果处理器。
        /// 应在对战开始前调用一次。
        /// </summary>
        public void Initialize()
        {
            HandlerRegistry.Initialize();
        }

        // ══════════════════════════════════════════════════════════
        // 瞬策牌结算（操作期内立即执行）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 立即结算一张瞬策牌的效果。
        ///
        /// 完整触发链：
        ///   1. BeforePlayCard 触发（可被反制牌取消）
        ///   2. 如果未被取消，通过 Handler 执行卡牌效果
        ///   3. AfterPlayCard 触发（出牌后效果）
        ///   4. 如果被取消，触发 OnCardCountered
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="card">待结算的瞬策牌</param>
        /// <returns>卡牌是否成功生效（未被反制）</returns>
        public bool ResolveInstantCard(BattleContext ctx, PlayedCard card)
        {
            ctx.RoundLog.Add($"[瞬策牌] 玩家{card.SourcePlayerId}打出「{card.Config.CardName}」");

            var source = ctx.GetPlayer(card.SourcePlayerId);
            if (source == null || !source.IsAlive)
            {
                ctx.RoundLog.Add($"[瞬策牌] 来源玩家无效或已死亡，跳过");
                return false;
            }

            // 解析目标
            card.ResolvedTargets = _targetResolver.Resolve(card, ctx);

            // ─────────────────────────────────────────────────────
            // 1. 触发 BeforePlayCard（反制牌在此时机检查）
            // ─────────────────────────────────────────────────────
            bool cancelled = false;
            if (ctx.TriggerManager != null)
            {
                ctx.TriggerManager.FireTriggers(
                    ctx,
                    TriggerTiming.BeforePlayCard,
                    sourcePlayerId: card.SourcePlayerId,
                    targetPlayerId: card.ResolvedTargets?.Count > 0 ? card.ResolvedTargets[0] : null,
                    value: 0,
                    relatedCard: card,
                    out cancelled
                );
            }

            // ─────────────────────────────────────────────────────
            // 2. 检查是否被反制
            // ─────────────────────────────────────────────────────
            if (cancelled)
            {
                ctx.RoundLog.Add($"[瞬策牌] 「{card.Config.CardName}」被反制，效果取消！");

                ctx.TriggerManager?.FireTriggers(
                    ctx,
                    TriggerTiming.OnCardCountered,
                    sourcePlayerId: card.SourcePlayerId,
                    relatedCard: card
                );

                ctx.EventRecorder?.RecordEvent(new Event.BattleEvent
                {
                    EventType = Event.BattleEventType.CardCountered,
                    Round = ctx.CurrentRound,
                    SourcePlayerId = card.SourcePlayerId,
                    Description = $"「{card.Config.CardName}」被反制"
                });

                return false;
            }

            // ─────────────────────────────────────────────────────
            // 3. 通过 Handler 执行卡牌效果
            // ─────────────────────────────────────────────────────
            foreach (var effect in card.Config.Effects)
            {
                ExecuteEffect(ctx, card, effect, source);
            }

            // ─────────────────────────────────────────────────────
            // 4. 触发 AfterPlayCard（出牌后效果）
            // ─────────────────────────────────────────────────────
            ctx.TriggerManager?.FireTriggers(
                ctx,
                TriggerTiming.AfterPlayCard,
                sourcePlayerId: card.SourcePlayerId,
                targetPlayerId: card.ResolvedTargets?.Count > 0 ? card.ResolvedTargets[0] : null,
                relatedCard: card
            );

            ctx.EventRecorder?.RecordEvent(new Event.BattleEvent
            {
                EventType = Event.BattleEventType.CardPlayed,
                Round = ctx.CurrentRound,
                SourcePlayerId = card.SourcePlayerId,
                Description = $"打出瞬策牌「{card.Config.CardName}」"
            });

            return true;
        }

        // ══════════════════════════════════════════════════════════
        // 定策牌统一结算入口
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 按 4 堆叠层顺序结算本回合所有已提交的定策牌。
        /// </summary>
        public void ResolvePlanCards(BattleContext ctx)
        {
            if (ctx.PendingPlanCards.Count == 0)
                return;

            ctx.RoundLog.Add($"[Settlement] ═══ 回合{ctx.CurrentRound} 定策牌结算开始 ═══");

            // 预处理：为所有待结算卡牌解析目标
            PreResolveTargets(ctx);

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

        /// <summary>
        /// 预处理：为所有待结算卡牌解析目标。
        /// </summary>
        private void PreResolveTargets(BattleContext ctx)
        {
            foreach (var card in ctx.PendingPlanCards)
            {
                if (ctx.GetPlayer(card.SourcePlayerId) == null) continue;
                card.ResolvedTargets = _targetResolver.Resolve(card, ctx);
            }
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

            foreach (var counterAction in ctx.PendingCounterCards)
            {
                ProcessCounterCard(ctx, counterAction);
            }

            // 筛选出本回合有效的定策牌（未被反制的）
            ctx.ValidPlanCards.Clear();

            foreach (var playedCard in ctx.PendingPlanCards)
            {
                // 跳过本回合新提交的反制牌（下回合才生效）
                if (playedCard.Config.HasTag(CardTag.Counter))
                {
                    ctx.RoundLog.Add($"[Layer0] 反制牌「{playedCard.Config.CardName}」已锁定，下回合生效");
                    continue;
                }

                // 检查是否被反制
                bool isCountered = false;
                foreach (var counteredCard in ctx.CounteredCards)
                {
                    if (counteredCard.RuntimeId == playedCard.RuntimeId)
                    {
                        isCountered = true;
                        break;
                    }
                }

                if (!isCountered)
                    ctx.ValidPlanCards.Add(playedCard);
            }

            ctx.RoundLog.Add($"[Layer0] 有效定策牌数量: {ctx.ValidPlanCards.Count}");
        }

        /// <summary>
        /// 处理单张反制牌的效果。
        /// </summary>
        private void ProcessCounterCard(BattleContext ctx, PlayedCard counterCard)
        {
            var source = ctx.GetPlayer(counterCard.SourcePlayerId);
            if (source == null || !source.IsAlive) return;

            string? targetPlayerId = counterCard.ResolvedTargets?.Count > 0
                ? counterCard.ResolvedTargets[0]
                : (counterCard.RawTargetGroup?.Count > 0 ? counterCard.RawTargetGroup[0] : null);

            if (string.IsNullOrEmpty(targetPlayerId)) return;

            var target = ctx.GetPlayer(targetPlayerId);
            if (target == null) return;

            PlayedCard? targetDamageCard = FindFirstDamageCard(ctx, target.PlayerId);

            if (targetDamageCard != null)
            {
                ctx.CounteredCards.Add(targetDamageCard);
                ctx.RoundLog.Add($"[Layer0] 玩家{source.PlayerId}的反制牌「{counterCard.Config.CardName}」" +
                    $"成功反制了玩家{target.PlayerId}的「{targetDamageCard.Config.CardName}」！");

                ApplyCounterEffects(ctx, counterCard, targetDamageCard);
            }
            else
            {
                ctx.RoundLog.Add($"[Layer0] 玩家{source.PlayerId}的反制牌「{counterCard.Config.CardName}」" +
                    $"未找到可反制的目标，效果落空");
            }
        }

        /// <summary>
        /// 查找指定玩家本回合提交的首张伤害牌。
        /// </summary>
        private PlayedCard? FindFirstDamageCard(BattleContext ctx, string playerId)
        {
            foreach (var card in ctx.PendingPlanCards)
            {
                if (card.SourcePlayerId == playerId && card.Config.HasTag(CardTag.Damage))
                    return card;
            }
            return null;
        }

        /// <summary>
        /// 应用反制牌的额外效果（如反弹伤害）。
        /// </summary>
        private void ApplyCounterEffects(BattleContext ctx, PlayedCard counterCard, PlayedCard targetCard)
        {
            foreach (var effect in counterCard.Config.Effects)
            {
                if (effect.EffectType == EffectType.Counter && counterCard.Config.HasTag(CardTag.Reflect))
                {
                    var attacker = ctx.GetPlayer(targetCard.SourcePlayerId);
                    if (attacker != null && attacker.IsAlive)
                    {
                        int reflectDamage = targetCard.Config.EffectValue;
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

            var layer1Effects = new List<EffectToResolve>();

            foreach (var card in ctx.ValidPlanCards)
            {
                var source = ctx.GetPlayer(card.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                foreach (var effect in card.Config.Effects)
                {
                    if (effect.GetSettlementLayer() == 1)
                    {
                        layer1Effects.Add(new EffectToResolve { Card = card, Effect = effect, Source = source });
                    }
                }
            }

            foreach (var item in layer1Effects)
            {
                ExecuteEffect(ctx, item.Card, item.Effect, item.Source);
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

            ResolveLayer2_Step1_Damage(ctx);
            ResolveLayer2_Step2_Triggers(ctx);
        }

        /// <summary>
        /// 堆叠2层-步骤1：所有伤害牌同步、一次性结算。
        /// </summary>
        private void ResolveLayer2_Step1_Damage(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer2-Step1] 主动伤害同步结算");

            // 收集所有伤害效果
            var damages = new List<DamageToApply>();

            foreach (var card in ctx.ValidPlanCards)
            {
                var source = ctx.GetPlayer(card.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                string? targetId = card.ResolvedTargets?.Count > 0
                    ? card.ResolvedTargets[0]
                    : (card.RawTargetGroup?.Count > 0 ? card.RawTargetGroup[0] : null);

                if (string.IsNullOrEmpty(targetId)) continue;
                if (ctx.GetPlayer(targetId) == null) continue;

                foreach (var effect in card.Config.Effects)
                {
                    if (effect.EffectType == EffectType.Damage)
                    {
                        int finalDamage = source.CalculateOutgoingDamage(effect.Value);
                        damages.Add(new DamageToApply
                        {
                            SourceId  = source.PlayerId,
                            TargetId  = targetId,
                            BaseDamage = effect.Value,
                            FinalDamage = finalDamage,
                            CardName  = card.Config.CardName,
                            HasLifesteal = HasLifestealEffect(card.Config)
                        });
                    }
                }
            }

            // 同步应用所有伤害（先计算，后统一写入）
            var hpChanges     = new Dictionary<string, int>();
            var shieldChanges = new Dictionary<string, int>();

            foreach (var player in ctx.Players)
            {
                hpChanges[player.PlayerId]     = 0;
                shieldChanges[player.PlayerId] = 0;
            }

            foreach (var dmg in damages)
            {
                var target = ctx.GetPlayer(dmg.TargetId);
                if (target == null || target.IsMarkedForDeath) continue;

                int actualDamage = target.CalculateIncomingDamage(dmg.FinalDamage);

                // 先扣护盾
                int currentShield = target.Shield + shieldChanges[target.PlayerId];
                if (currentShield > 0)
                {
                    int shieldAbsorb = currentShield >= actualDamage ? actualDamage : currentShield;
                    shieldChanges[target.PlayerId] -= shieldAbsorb;
                    actualDamage -= shieldAbsorb;
                    if (shieldAbsorb > 0)
                        ctx.RoundLog.Add($"[Layer2-Step1] 「{dmg.CardName}」被护盾吸收{shieldAbsorb}点伤害");
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

                        if (dmg.HasLifesteal)
                        {
                            ctx.PendingTriggerEffects.Add(new PendingTriggerEffect
                            {
                                SourcePlayerId = dmg.SourceId,
                                TargetPlayerId = dmg.SourceId,
                                EffectType     = EffectType.Lifesteal,
                                Value          = actualDamage,
                                TriggerReason  = $"「{dmg.CardName}」吸血"
                            });
                        }
                    }
                }

                ctx.RoundLog.Add($"[Layer2-Step1] 玩家{dmg.SourceId}的「{dmg.CardName}」" +
                    $"对玩家{dmg.TargetId}造成{actualDamage}点伤害");
            }

            // 统一应用所有变化，并通知 BuffManager 触发响应型 Buff（反伤/吸血等）
            foreach (var player in ctx.Players)
            {
                string pid = player.PlayerId;

                if (shieldChanges[pid] != 0)
                {
                    player.Shield += shieldChanges[pid];
                    if (player.Shield < 0) player.Shield = 0;
                }

                if (hpChanges[pid] != 0)
                {
                    int hpDelta = hpChanges[pid];   // 负值 = 受到伤害
                    player.Hp += hpDelta;
                    if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;

                    if (player.Hp <= 0)
                    {
                        player.Hp = 0;
                        player.IsMarkedForDeath = true;
                        ctx.RoundLog.Add($"[Layer2-Step1] 玩家{pid}被标记为濒死状态");
                    }

                    // 通知受伤玩家的 BuffManager（触发反伤、受伤获甲等）
                    int damageTaken = -hpDelta;     // 转为正数
                    if (damageTaken > 0)
                    {
                        // 找出是谁打了这个玩家（可能有多个来源，逐一通知）
                        foreach (var dmg in damages)
                        {
                            if (dmg.TargetId == pid)
                            {
                                ctx.GetBuffManager(pid)
                                   ?.OnDamageTaken(ctx, damageTaken, dmg.SourceId);

                                // 同时通知攻击方 BuffManager（吸血触发）
                                ctx.GetBuffManager(dmg.SourceId)
                                   ?.OnDamageDealt(ctx, damageTaken, pid);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 检查卡牌是否含有吸血效果。
        /// </summary>
        private bool HasLifestealEffect(CardConfig card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.EffectType == EffectType.Lifesteal)
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

            foreach (var trigger in ctx.PendingTriggerEffects)
            {
                var source = ctx.GetPlayer(trigger.SourcePlayerId);
                var target = ctx.GetPlayer(trigger.TargetPlayerId);
                if (source == null || target == null) continue;

                switch (trigger.EffectType)
                {
                    case EffectType.Lifesteal:
                        target.Hp += trigger.Value;
                        if (target.Hp > target.MaxHp) target.Hp = target.MaxHp;
                        ctx.RoundLog.Add($"[Layer2-Step2] {trigger.TriggerReason}，玩家{target.PlayerId}回复{trigger.Value}点生命");
                        break;

                    case EffectType.Thorns:
                        target.Hp -= trigger.Value;
                        if (target.Hp < 0) target.Hp = 0;
                        ctx.RoundLog.Add($"[Layer2-Step2] {trigger.TriggerReason}，玩家{target.PlayerId}受到{trigger.Value}点反伤");
                        break;

                    case EffectType.ArmorOnHit:
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

            ResolveLayer3_SubPhase1_Normal(ctx);
            ResolveLayer3_SubPhase2_Legendary(ctx);
        }

        /// <summary>
        /// 堆叠3层-子阶段1：普通效果结算（控制、资源、支援）。
        /// </summary>
        private void ResolveLayer3_SubPhase1_Normal(BattleContext ctx)
        {
            foreach (var card in ctx.ValidPlanCards)
            {
                if (card.Config.IsLegendary) continue;

                var source = ctx.GetPlayer(card.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                foreach (var effect in card.Config.Effects)
                {
                    if (effect.GetSettlementLayer() == 3)
                        ExecuteEffect(ctx, card, effect, source);
                }
            }
        }

        /// <summary>
        /// 堆叠3层-子阶段2：传说特殊牌专属结算。
        /// </summary>
        private void ResolveLayer3_SubPhase2_Legendary(BattleContext ctx)
        {
            foreach (var card in ctx.ValidPlanCards)
            {
                if (!card.Config.IsLegendary) continue;

                var source = ctx.GetPlayer(card.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                ctx.RoundLog.Add($"[Layer3-Legendary] 传说牌「{card.Config.CardName}」专属结算");

                foreach (var effect in card.Config.Effects)
                {
                    ExecuteEffect(ctx, card, effect, source);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 核心效果执行 —— 通过 Handler 注册中心分发
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 执行单个效果 —— 通过 HandlerRegistry 找到对应 Handler 并执行。
        /// 若无对应 Handler，则记录警告日志并跳过（不抛异常）。
        /// </summary>
        private void ExecuteEffect(BattleContext ctx, PlayedCard card, CardEffect effect, PlayerBattleState source)
        {
            var handler = HandlerRegistry.GetHandler(effect.EffectType);
            if (handler == null)
            {
                ctx.RoundLog.Add($"[Warning] 未找到效果处理器: {effect.EffectType}，效果跳过");
                return;
            }

            var targets = card.ResolvedTargets;
            if (targets == null || targets.Count == 0)
            {
                handler.Execute(card, effect, source, null, ctx);
            }
            else
            {
                foreach (var targetId in targets)
                {
                    var target = ctx.GetPlayer(targetId);
                    handler.Execute(card, effect, source, target, ctx);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // 辅助数据结构
        // ══════════════════════════════════════════════════════════

        /// <summary>待结算的效果信息。</summary>
        private class EffectToResolve
        {
            public PlayedCard Card { get; set; } = null!;
            public CardEffect Effect { get; set; } = null!;
            public PlayerBattleState Source { get; set; } = null!;
        }

        /// <summary>待应用的伤害信息。</summary>
        private class DamageToApply
        {
            public string SourceId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public int BaseDamage { get; set; }
            public int FinalDamage { get; set; }
            public string CardName { get; set; } = string.Empty;
            public bool HasLifesteal { get; set; }
        }
    }
}