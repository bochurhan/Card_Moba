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
        /// 堆叠0层：处理反制效果层。
        ///
        /// 新架构说明（R-Counter 重构）：
        ///   反制牌在打出时已通过 PassiveHandler 注册了 BeforePlayCard 触发器，
        ///   触发器在对方卡牌进入 PendingPlanCards 时触发，自动将目标加入 ctx.CounteredCards。
        ///   Layer0 只负责：
        ///     1. 筛除带 Counter 标签的本回合新提交牌（下回合才生效，从 ValidPlanCards 排除）
        ///     2. 筛除已被触发器判定为"被反制"的卡牌（CounteredCards 中的）
        ///
        /// 旧方法 ProcessCounterCard / FindFirstDamageCard / ApplyCounterEffects 已移除（R-Counter 清理）。
        /// Counter 效果的具体逻辑已迁移到 CounterHandler + PassiveHandler 体系。
        /// </summary>
        private void ResolveLayer0_Counter(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer0] ── 反制效果结算层 ──");

            // 筛选出本回合有效的定策牌（未被反制的）
            ctx.ValidPlanCards.Clear();

            foreach (var playedCard in ctx.PendingPlanCards)
            {
                // 跳过带 Counter 标签的本回合新提交反制牌（下回合才生效）
                if (playedCard.Config.HasTag(CardTag.Counter))
                {
                    ctx.RoundLog.Add($"[Layer0] 反制牌「{playedCard.Config.CardName}」本回合锁定，下回合生效");
                    continue;
                }

                // 检查是否已被触发器反制（PassiveHandler 注册的 BeforePlayCard 触发器写入 CounteredCards）
                bool isCountered = false;
                foreach (var counteredCard in ctx.CounteredCards)
                {
                    if (counteredCard.RuntimeId == playedCard.RuntimeId)
                    {
                        isCountered = true;
                        break;
                    }
                }

                if (isCountered)
                {
                    ctx.RoundLog.Add($"[Layer0] 「{playedCard.Config.CardName}」已被反制，跳过结算");
                }
                else
                {
                    ctx.ValidPlanCards.Add(playedCard);
                }
            }

            ctx.RoundLog.Add($"[Layer0] 有效定策牌数量: {ctx.ValidPlanCards.Count}");
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
                        layer1Effects.Add(new EffectToResolve
                        {
                            Card        = card,
                            Effect      = effect,
                            Source      = source,
                            Priority    = effect.Priority,
                            SubPriority = effect.SubPriority,
                        });
                    }
                }
            }

            // 按优先级排序：Priority 小的先执行（增益 100-199 先于削弱 300-399）
            // Priority 相同时，SubPriority 小的先执行
            layer1Effects.Sort((a, b) =>
            {
                int cmp = a.Priority.CompareTo(b.Priority);
                return cmp != 0 ? cmp : a.SubPriority.CompareTo(b.SubPriority);
            });

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
    ///
    /// 结构：
    ///   Step1 —— Damage 类型效果走 A-B-C 三阶段批量结算（保证同回合伤害同步性）
    ///   Step2 —— 其他 Layer2 效果（Pierce 等辅助类）按优先级顺序通过 ExecuteEffect 分发
    ///            （在伤害批量写入后执行，可读到稳定的 HP 状态）
    ///
    /// 注意：触发式效果（吸血/反伤/ArmorOnHit）已通过 BuffManager 注册触发器，
    ///       在 Step1 阶段C的 AfterDealDamage / AfterTakeDamage 中统一触发，无需独立 Step2。
    /// </summary>
    private void ResolveLayer2_Damage(BattleContext ctx)
    {
        ctx.RoundLog.Add("[Layer2] ── 主动伤害与触发式效果层 ──");

        ResolveLayer2_Step1_Damage(ctx);
        ResolveLayer2_Step2_Other(ctx);
    }

        /// <summary>
        /// 堆叠2层-步骤1：所有伤害牌同步、一次性结算。
        ///
        /// 执行流程：
        ///   阶段A（收集）：遍历有效定策牌，计算每张伤害牌的最终伤害值（含攻击方加成），
        ///                  存入 damages 列表。此阶段只计算，不修改任何状态，保证同步性。
        ///   阶段B（批量写入）：对每条伤害记录，依次经过无敌检查、护盾吸收、扣血，
        ///                     写入 hpChanges / shieldChanges 累计变化量。
        ///   阶段C（触发器统一触发）：批量写入完毕后，按伤害记录顺序统一触发
        ///                          AfterDealDamage / AfterTakeDamage / OnNearDeath，
        ///                          确保所有基础伤害先落定再触发二次效果。
        ///
        /// 与 DamageHelper.ApplyDamage 的协作关系：
        ///   - 阶段A/B 替代了 DamageHelper 的内联逻辑（批量语义无法直接复用单次调用）
        ///   - 阶段C 统一补齐 DamageHelper 中单次触发的所有回调，保证 Buff / Trigger 系统等效
        /// </summary>
        private void ResolveLayer2_Step1_Damage(BattleContext ctx)
        {
            ctx.RoundLog.Add("[Layer2-Step1] 主动伤害同步结算");

            // ──────────────────────────────────────────────────────
            // 阶段A：收集所有伤害效果（只读，不修改状态）
            // ──────────────────────────────────────────────────────
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
                        // BeforeDealDamage 触发器（可修改或取消伤害）
                        int outgoing = source.CalculateOutgoingDamage(effect.Value);
                        bool cancelled = false;

                        if (ctx.TriggerManager != null)
                        {
                            var beforeDealCtx = new Trigger.TriggerContext
                            {
                                BattleContext   = ctx,
                                Timing          = Trigger.TriggerTiming.BeforeDealDamage,
                                SourcePlayerId  = source.PlayerId,
                                TargetPlayerId  = targetId,
                                Value           = outgoing,
                                ModifiedValue   = outgoing
                            };
                            ctx.TriggerManager.FireTriggers(ctx, Trigger.TriggerTiming.BeforeDealDamage, beforeDealCtx);

                            if (beforeDealCtx.ShouldCancel)
                            {
                                ctx.RoundLog.Add($"[Layer2-Step1] 「{card.Config.CardName}」伤害被 BeforeDealDamage 触发器取消");
                                cancelled = true;
                            }
                            else
                            {
                                outgoing = beforeDealCtx.ModifiedValue;
                            }
                        }

                        if (!cancelled)
                        {
                            damages.Add(new DamageToApply
                            {
                                SourceId    = source.PlayerId,
                                TargetId    = targetId,
                                BaseDamage  = effect.Value,
                                FinalDamage = outgoing,
                                CardName    = card.Config.CardName,
                            });
                        }
                    }
                }
            }

            // ──────────────────────────────────────────────────────
            // 阶段B：批量写入伤害（护盾 → 扣血，累计 delta）
            // ──────────────────────────────────────────────────────
            // hpChanges / shieldChanges：累计本阶段所有伤害对各玩家的净变化量，
            // 避免同一回合多张伤害牌之间互相影响对方的瞬时状态。
            var hpChanges     = new Dictionary<string, int>();
            var shieldChanges = new Dictionary<string, int>();

            foreach (var player in ctx.Players)
            {
                hpChanges[player.PlayerId]     = 0;
                shieldChanges[player.PlayerId] = 0;
            }

            // 记录每条伤害的"最终实际 HP 伤害"，供阶段C触发器使用
            var actualHpDamages = new List<AppliedDamageRecord>();

            foreach (var dmg in damages)
            {
                var target = ctx.GetPlayer(dmg.TargetId);
                if (target == null || target.IsMarkedForDeath) continue;

                // ── 无敌检查：无敌时跳过全部伤害 ──
                if (target.IsInvincible)
                {
                    ctx.RoundLog.Add($"[Layer2-Step1] 玩家{dmg.TargetId}处于无敌状态，「{dmg.CardName}」伤害被免疫");
                    continue;
                }

                // ── BeforeTakeDamage 触发器（可修改或取消伤害）──
                int actualDamage = target.CalculateIncomingDamage(dmg.FinalDamage);
                if (ctx.TriggerManager != null)
                {
                    var beforeTakeCtx = new Trigger.TriggerContext
                    {
                        BattleContext   = ctx,
                        Timing          = Trigger.TriggerTiming.BeforeTakeDamage,
                        SourcePlayerId  = dmg.SourceId,
                        TargetPlayerId  = dmg.TargetId,
                        Value           = actualDamage,
                        ModifiedValue   = actualDamage
                    };
                    ctx.TriggerManager.FireTriggers(ctx, Trigger.TriggerTiming.BeforeTakeDamage, beforeTakeCtx);

                    if (beforeTakeCtx.ShouldCancel)
                    {
                        ctx.RoundLog.Add($"[Layer2-Step1] 「{dmg.CardName}」伤害被 BeforeTakeDamage 触发器取消");
                        continue;
                    }
                    actualDamage = beforeTakeCtx.ModifiedValue;
                }

                // ── 护盾吸收（使用累计后的当前护盾值）──
                int shieldAbsorbed = 0;
                int currentShield = target.Shield + shieldChanges[target.PlayerId];
                if (currentShield > 0 && actualDamage > 0)
                {
                    shieldAbsorbed = currentShield >= actualDamage ? actualDamage : currentShield;
                    shieldChanges[target.PlayerId] -= shieldAbsorbed;
                    actualDamage -= shieldAbsorbed;

                    ctx.RoundLog.Add($"[Layer2-Step1] 「{dmg.CardName}」被护盾吸收 {shieldAbsorbed} 点伤害");

                    // 护盾破碎检测（护盾累计后归零）
                    if ((target.Shield + shieldChanges[target.PlayerId]) <= 0)
                    {
                        ctx.TriggerManager?.FireTriggers(ctx, Trigger.TriggerTiming.OnShieldBroken, new Trigger.TriggerContext
                        {
                            BattleContext  = ctx,
                            Timing         = Trigger.TriggerTiming.OnShieldBroken,
                            SourcePlayerId = dmg.SourceId,
                            TargetPlayerId = dmg.TargetId,
                            Value          = shieldAbsorbed
                        });
                    }
                }

                // ── 扣血（累计 delta）──
                if (actualDamage > 0)
                {
                    hpChanges[target.PlayerId] -= actualDamage;
                    target.DamageTakenThisRound += actualDamage;

                    var source = ctx.GetPlayer(dmg.SourceId);
                    if (source != null)
                        source.DamageDealtThisRound += actualDamage;

                    actualHpDamages.Add(new AppliedDamageRecord
                    {
                        SourceId = dmg.SourceId,
                        TargetId = dmg.TargetId,
                        HpDamage = actualDamage,
                        CardName = dmg.CardName,
                    });

                    ctx.RoundLog.Add($"[Layer2-Step1] 玩家{dmg.SourceId}的「{dmg.CardName}」" +
                        $"对玩家{dmg.TargetId}造成 {actualDamage} 点伤害");
                }
            }

            // ── 统一写入护盾变化 ──
            foreach (var player in ctx.Players)
            {
                string pid = player.PlayerId;
                if (shieldChanges[pid] != 0)
                {
                    player.Shield += shieldChanges[pid];
                    if (player.Shield < 0) player.Shield = 0;
                }
            }

            // ── 统一写入 HP 变化并标记濒死 ──
            foreach (var player in ctx.Players)
            {
                string pid = player.PlayerId;
                if (hpChanges[pid] == 0) continue;

                player.Hp += hpChanges[pid];
                if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;
                if (player.Hp < 0) player.Hp = 0;
            }

            // ──────────────────────────────────────────────────────
            // 阶段C：批量触发所有伤害后回调
            // 必须在 HP 统一写入之后执行，确保回调看到的是稳定的最终状态
            // ──────────────────────────────────────────────────────
            foreach (var rec in actualHpDamages)
            {
                var source = ctx.GetPlayer(rec.SourceId);
                var target = ctx.GetPlayer(rec.TargetId);
                if (source == null || target == null) continue;

                // AfterDealDamage（攻击方视角，含吸血 Buff 触发）
                ctx.TriggerManager?.FireTriggers(ctx, Trigger.TriggerTiming.AfterDealDamage, new Trigger.TriggerContext
                {
                    BattleContext  = ctx,
                    Timing         = Trigger.TriggerTiming.AfterDealDamage,
                    SourcePlayerId = rec.SourceId,
                    TargetPlayerId = rec.TargetId,
                    Value          = rec.HpDamage,
                    DamageSource   = Trigger.DamageSourceType.CardDamage
                });

                // AfterTakeDamage（防守方视角，含 Thorns/Reflect Buff 触发）
                // SourcePlayerId = 受伤方，TargetPlayerId = 攻击方（与 DamageHelper 保持一致）
                ctx.TriggerManager?.FireTriggers(ctx, Trigger.TriggerTiming.AfterTakeDamage, new Trigger.TriggerContext
                {
                    BattleContext  = ctx,
                    Timing         = Trigger.TriggerTiming.AfterTakeDamage,
                    SourcePlayerId = rec.TargetId,
                    TargetPlayerId = rec.SourceId,
                    Value          = rec.HpDamage,
                    DamageSource   = Trigger.DamageSourceType.CardDamage
                });

                // R-05 修复：吸血效果不再走旧的 PendingTriggerEffects 路径。
                // Lifesteal Buff 已在 BuffManager.RegisterBuffTriggers 中注册了 AfterDealDamage 触发器，
                // 由上方的 AfterDealDamage 触发器调用统一处理，无需在此重复加入。
                // HasLifesteal 字段保留用于日志/调试，逻辑上已无用途。

                // OnNearDeath（濒死检查，含复活 Buff 触发）
                if (target.Hp <= 0)
                {
                    target.IsMarkedForDeath = true;
                    source.HasKilledThisRound = true;
                    ctx.RoundLog.Add($"[Layer2-Step1] 玩家{rec.TargetId}被标记为濒死状态");

                    ctx.TriggerManager?.FireTriggers(ctx, Trigger.TriggerTiming.OnNearDeath, new Trigger.TriggerContext
                    {
                        BattleContext  = ctx,
                        Timing         = Trigger.TriggerTiming.OnNearDeath,
                        SourcePlayerId = rec.SourceId,
                        TargetPlayerId = rec.TargetId,
                        Value          = rec.HpDamage
                    });

                    // 如果复活 Buff 将 HP 恢复为正值，撤销濒死标记
                    if (target.Hp > 0)
                    {
                        target.IsMarkedForDeath = false;
                        ctx.RoundLog.Add($"[Layer2-Step1] 玩家{rec.TargetId}被复活效果救活");
                    }
                }
            }
        }

        /// <summary>
        /// 堆叠2层-步骤2：非 Damage 类型的其他 Layer2 效果。
        ///
        /// 在 Step1 批量伤害写入完毕、HP 已稳定后执行。
        /// 包括：Heal（ValueSource="LastDamageDealt" 的联动回血）、
        ///        Pierce（辅助类，通常已在 DamageHandler 内处理，此处兜底）等。
        ///
        /// 执行顺序：按各效果的 Priority / SubPriority 排序（与 Layer1 一致）。
        /// 同一张牌的效果按配置顺序隐式由 Priority 控制，策划设置
        ///   Damage Priority=500, Heal Priority=510 即可保证 Damage 先于 Heal。
        /// </summary>
        private void ResolveLayer2_Step2_Other(BattleContext ctx)
        {
            var otherEffects = new List<EffectToResolve>();

            foreach (var card in ctx.ValidPlanCards)
            {
                var source = ctx.GetPlayer(card.SourcePlayerId);
                if (source == null || !source.IsAlive) continue;

                foreach (var effect in card.Config.Effects)
                {
                    // 只处理 Layer2 中非 Damage 类型的效果
                    if (effect.GetSettlementLayer() == 2 && effect.EffectType != EffectType.Damage)
                    {
                        otherEffects.Add(new EffectToResolve
                        {
                            Card        = card,
                            Effect      = effect,
                            Source      = source,
                            Priority    = effect.Priority,
                            SubPriority = effect.SubPriority,
                        });
                    }
                }
            }

            if (otherEffects.Count == 0) return;

            otherEffects.Sort((a, b) =>
            {
                int cmp = a.Priority.CompareTo(b.Priority);
                return cmp != 0 ? cmp : a.SubPriority.CompareTo(b.SubPriority);
            });

            ctx.RoundLog.Add($"[Layer2-Step2] 处理 {otherEffects.Count} 个非伤害 Layer2 效果");
            foreach (var item in otherEffects)
                ExecuteEffect(ctx, item.Card, item.Effect, item.Source);
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
        ///
        /// 执行模式路由：
        ///   Immediate   —— 直接执行
        ///   Conditional —— 先通过 EffectConditionChecker 评估条件，满足再执行
        ///   Passive     —— 跳过（Passive 效果在打出时由 PassiveHandler 注册，此处无需处理）
        ///
        /// 目标优先级：
        ///   1. effect.TargetOverride（单个效果的目标覆盖，如护盾牌自身效果覆盖为 Self）
        ///   2. card.ResolvedTargets（整张卡的解析目标）
        /// </summary>
        private void ExecuteEffect(BattleContext ctx, PlayedCard card, CardEffect effect, PlayerBattleState source)
        {
            // ── Passive 模式：打出时已由 PassiveHandler 注册触发器，结算时跳过 ──
            if (effect.ExecutionMode == EffectExecutionMode.Passive)
                return;

            // ── Conditional 模式：评估 EffectConditions，不满足则跳过 ──
            if (effect.ExecutionMode == EffectExecutionMode.Conditional
                && effect.EffectConditions != null
                && effect.EffectConditions.Count > 0)
            {
                string? targetId = card.ResolvedTargets?.Count > 0 ? card.ResolvedTargets[0] : null;
                bool conditionMet = EffectConditionChecker.EvaluateAll(
                    effect.EffectConditions, source.PlayerId, targetId, ctx);

                if (!conditionMet)
                {
                    ctx.RoundLog.Add(
                        $"[Settlement] 「{card.Config.CardName}」效果 {effect.EffectType} 条件不满足，跳过");
                    return;
                }

                ctx.RoundLog.Add(
                    $"[Settlement] 「{card.Config.CardName}」效果 {effect.EffectType} 条件满足，执行");
            }

            var handler = HandlerRegistry.GetHandler(effect.EffectType);
            if (handler == null)
            {
                ctx.RoundLog.Add($"[Warning] 未找到效果处理器: {effect.EffectType}，效果跳过");
                return;
            }

            // ── 优先使用 effect.TargetOverride 覆盖目标 ──
            if (effect.TargetOverride.HasValue)
            {
                PlayerBattleState? overrideTarget = ResolveTargetOverride(
                    effect.TargetOverride.Value, source, card, ctx);
                handler.Execute(card, effect, source, overrideTarget, ctx);
                return;
            }

            // ── 使用卡牌整体的解析目标 ──
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

        /// <summary>
        /// 根据 CardTargetType 解析单个效果的覆盖目标。
        /// </summary>
        private PlayerBattleState? ResolveTargetOverride(
            CardTargetType targetType,
            PlayerBattleState source,
            PlayedCard card,
            BattleContext ctx)
        {
            switch (targetType)
            {
                case CardTargetType.Self:
                    return source;

                case CardTargetType.CurrentEnemy:
                case CardTargetType.AnyEnemy:
                case CardTargetType.AllEnemies:
                    // 从 ResolvedTargets 取第一个，或从所有玩家里找对手
                    if (card.ResolvedTargets?.Count > 0)
                        return ctx.GetPlayer(card.ResolvedTargets[0]);
                    foreach (var p in ctx.Players)
                        if (p.TeamId != source.TeamId && p.IsAlive)
                            return p;
                    return null;

                case CardTargetType.AnyAlly:
                case CardTargetType.AllAllies:
                    return source; // 友方默认为自身

                default:
                    return source;
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

            /// <summary>
            /// 结算优先级（来自 CardEffect.Priority，数值越小越先执行）。
            /// 排序规则：增益效果（100-199）先于削弱效果（300-399），与 TriggerInstance.Priority 约定一致。
            /// </summary>
            public int Priority { get; set; } = 500;

            /// <summary>次级优先级（主优先级相同时的打破平局字段）。</summary>
            public int SubPriority { get; set; } = 0;
        }

        /// <summary>待应用的伤害信息（阶段A收集，阶段B读取）。</summary>
        private class DamageToApply
        {
            public string SourceId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public int BaseDamage { get; set; }
            public int FinalDamage { get; set; }
            public string CardName { get; set; } = string.Empty;
        }

        /// <summary>
        /// 阶段B写入后的实际伤害记录（阶段C触发器读取）。
        /// 只记录最终落地到 HP 的伤害，护盾完全吸收的伤害不在此列。
        /// </summary>
        private class AppliedDamageRecord
        {
            /// <summary>攻击方玩家ID。</summary>
            public string SourceId { get; set; } = string.Empty;
            /// <summary>受伤方玩家ID。</summary>
            public string TargetId { get; set; } = string.Empty;
            /// <summary>实际造成的 HP 伤害（护盾吸收后的余量）。</summary>
            public int HpDamage { get; set; }
            /// <summary>来源卡牌名（用于日志）。</summary>
            public string CardName { get; set; } = string.Empty;
        }
    }
}
