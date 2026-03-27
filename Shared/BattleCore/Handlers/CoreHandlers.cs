
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Managers;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Resolvers;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Handlers
{
    // ══════════════════════════════════════════════════════════════
    // DamageHandler：处理 Damage / Pierce 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 伤害 Handler：处理 EffectType.Damage 和 EffectType.Pierce。
    ///
    /// 执行流程（A→B→C 三阶段，每次对单一目标）：
    ///   Phase A（只读）：计算修正后伤害值，检查无敌。
    ///   Phase B（写入）：护盾吸收、护甲减免、HP 扣减，记录实际伤害量。
    ///   Phase C（触发）：Fire(AfterDealDamage) + Fire(AfterTakeDamage)，推进 PendingQueue。
    ///
    /// ⚠️ 定策牌 Layer 2 结算时，Handler 会按出牌顺序逐张调用。
    ///    每张牌完整走完 A-B-C，再处理下一张（保留己方顺序依赖语义）。
    /// </summary>
    public class DamageHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };
            bool isPierce = effect.Type == EffectType.Pierce;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);
            bool isDot = effect.Params.TryGetValue("isDot", out var isDotValue)
                && bool.TryParse(isDotValue, out var parsedIsDot)
                && parsedIsDot;
            bool isThorns = effect.Params.TryGetValue("isThorns", out var isThornsValue)
                && bool.TryParse(isThornsValue, out var parsedIsThorns)
                && parsedIsThorns;

            // 基础伤害值由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int baseDamage = effect.ResolvedValue;

            // ── 施害方出伤修正（力量 Add / 虚弱 Mul）────────────────
            // 传 source.OwnerPlayerId，与 BuffManager 注册修正器时使用的 ownerPlayerId 一致。
            int modifiedDamage = ctx.ValueModifierManager.Apply(
                effect.Type, source.OwnerPlayerId, ModifierScope.OutgoingDamage, baseDamage);

            foreach (var target in targets)
            {
                // ── Phase A：只读校验 ─────────────────────────────────
                if (!target.IsAlive) continue;

                // 无敌检查
                if (target.IsInvincible)
                {
                    ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} is invincible; damage ignored.");
                    continue;
                }

                // ── Phase B：写入 ───────────────────────────────────
                // 受击方入伤修正（护甲 Add 负值 / 易伤 Mul）
                // 单独以 target.OwnerPlayerId 查受击方修正，与施害方修正分开路由。
                int incomingDamage = ctx.ValueModifierManager.Apply(
                    effect.Type, target.OwnerPlayerId, ModifierScope.IncomingDamage, modifiedDamage);

                int remaining = incomingDamage;
                int shieldAbsorbed = 0;
                int armorReduced = 0;

                // ── 防御快照隔离：Layer 2 定策结算时读快照，其他场景（瞬策等）读实时值 ──
                // 快照在 Pre-Layer 2 拍摄，代表“本轮定策开始前”的防御状态，
                // 使双方各自的受伤计算不受对方出牌顺序影响。
                // 此前直接读 target.Shield/Armor（实时值），会导致快照机制形同虚设。
                var targetPlayer = ctx.GetPlayer(target.OwnerPlayerId);
                var snapshot     = targetPlayer?.CurrentDefenseSnapshot;

                // 快照防御值：Layer 2 定策时读快照（代表本次 Layer 2 开始前的状态）。
                // 无快照时（瞬策等场景）读实时值。
                // ⚠️ 快照值必须随每次命中递减，否则多张伤害牌会重复消费同一份护盾。
                //    快照和实时值同步扣减，保持语义一致。
                //
                // 设计约定（见 SettlementRules.md §Layer2 快照隔离）：
                //   Layer 2 期间 AfterTakeDamage 等触发器动态生成的护盾会写入实时 target.Shield，
                //   但快照不更新，因此这部分护盾本回合不生效，下回合才参与防御。
                //   这是有意为之的设计：受伤后获得的护盾代表“战斗经验积累”，下回合才转化为防御力。
                int snapshotShield = snapshot != null ? snapshot.Shield : target.Shield;
                int snapshotArmor  = snapshot != null ? snapshot.Armor  : target.Armor;

                // 护盾破裂标记：提前声明，供 Phase C 统一广播使用。
                bool shieldBroken = false;

                // 护盾吸收（穿透伤害不跳过护盾，只跳过护甲）
                if (snapshotShield > 0)
                {
                    shieldAbsorbed = remaining < snapshotShield ? remaining : snapshotShield;
                    // 同步递减快照和实时值，防止后续命中重复消费
                    if (snapshot != null) snapshot.Shield -= shieldAbsorbed;
                    target.Shield -= shieldAbsorbed;
                    if (target.Shield < 0) target.Shield = 0;
                    remaining -= shieldAbsorbed;

                    // 护盾破裂判断：本次命中前快照有盾，且本次吸收量耗尽了全部快照盾。
                    // snapshotShield 是递减前的值，shieldAbsorbed == snapshotShield 说明盾被打光。
                    shieldBroken = shieldAbsorbed > 0 && shieldAbsorbed == snapshotShield;
                    if (shieldBroken)
                    {
                        ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} shield broken.");
                        ctx.TriggerManager.Fire(ctx, TriggerTiming.OnShieldBroken, new TriggerContext
                        {
                            SourceEntityId = source.EntityId,
                            TargetEntityId = target.EntityId,
                            Value          = shieldAbsorbed,
                        });
                        // 注意：不在此处广播 DamageDealtEvent，统一在 Phase C 末尾合并广播（避免重复）。
                    }
                }

                // 护甲减伤（穿透伤害跳过护甲）
                if (!isPierce && snapshotArmor > 0 && remaining > 0)
                {
                    armorReduced = remaining < snapshotArmor ? remaining : snapshotArmor;
                    // 同步递减快照和实时值
                    if (snapshot != null) snapshot.Armor -= armorReduced;
                    target.Armor -= armorReduced;
                    if (target.Armor < 0) target.Armor = 0;
                    remaining -= armorReduced;
                }

                // HP 扣减
                int realHpDamage = remaining > 0 ? remaining : 0;
                if (realHpDamage > 0)
                {
                    target.Hp -= realHpDamage;
                    result.TotalRealHpDamage += realHpDamage;
                    result.PerTargetValues[target.EntityId] = realHpDamage;
                }

                ctx.RoundLog.Add($"[DamageHandler] {source.EntityId} -> {target.EntityId}: base={baseDamage}, modified={modifiedDamage}, shieldAbsorbed={shieldAbsorbed}, armorReduced={armorReduced}, realHpDamage={realHpDamage}, hp={target.Hp}");

                // Phase C 统一广播（含 ShieldBroken 标记，避免护盾破裂时重复发两条事件）
                ctx.EventBus.Publish(new DamageDealtEvent
                {
                    SourceEntityId   = source.EntityId,
                    TargetEntityId   = target.EntityId,
                    BaseDamage       = modifiedDamage,
                    RealHpDamage     = realHpDamage,
                    ShieldAbsorbed   = shieldAbsorbed,
                    ArmorReduced     = armorReduced,
                    ShieldBroken     = shieldBroken,
                    IsDot            = isDot,
                    IsThorns         = isThorns,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                // ── Phase C：触发器 ──────────────────────────────────
                // AfterDealDamage（施害方视角）
                ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterDealDamage, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = realHpDamage,
                });

                // AfterTakeDamage（受害方视角，注意 Source/Target 方向约定）
                // SourceEntityId = 受害方，TargetEntityId = 施害方（详见文档 §4.5 约定）
                ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterTakeDamage, new TriggerContext
                {
                    SourceEntityId = target.EntityId,
                    TargetEntityId = source.EntityId,
                    Value          = realHpDamage,
                });

                // 濒死标记：HP <= 0 时仅标记，不在此处触发 OnNearDeath/OnDeath。
                // 死亡链路统一由 RoundManager.CheckDeathAndBattleOver 处理，
                // 避免同一次击杀重复触发濒死/复活/OnDeath 及战斗结束判定。
                if (!target.IsAlive && !target.DeathEventFired)
                {
                    ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} HP <= 0, waiting for RoundManager death resolution.");
                }
            }

            return result;
        }

    }

    // ══════════════════════════════════════════════════════════════
    // HealHandler：处理 Heal 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 治疗 Handler：处理 EffectType.Heal。
    /// 治疗量不得超过目标 MaxHp，不触发伤害相关的触发器。
    /// </summary>
    public class HealHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 治疗量由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int baseHeal = effect.ResolvedValue;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;

                int canHeal = target.MaxHp - target.Hp;
                int realHeal = baseHeal < canHeal ? baseHeal : canHeal;
                if (realHeal <= 0) continue;

                target.Hp += realHeal;
                result.TotalRealHeal += realHeal;
                result.PerTargetValues[target.EntityId] = realHeal;

                ctx.RoundLog.Add($"[HealHandler] {source.EntityId} -> {target.EntityId}: heal={realHeal}, hp={target.Hp}");

                ctx.EventBus.Publish(new HealEvent
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    RealHealAmount = realHeal,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnHealed, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = realHeal,
                });
            }

            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ShieldHandler：处理 Shield 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 护盾 Handler：处理 EffectType.Shield。
    /// 护盾叠加到当前护盾值（不设上限），回合结束时清零。
    /// </summary>
    public class ShieldHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 护盾量由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int shieldAmount = effect.ResolvedValue;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;

                target.Shield += shieldAmount;
                result.TotalRealShield += shieldAmount;
                result.PerTargetValues[target.EntityId] = shieldAmount;

                ctx.RoundLog.Add($"[ShieldHandler] {source.EntityId} -> {target.EntityId}: shield+={shieldAmount}, shield={target.Shield}");

                ctx.EventBus.Publish(new ShieldGainedEvent
                {
                    TargetEntityId = target.EntityId,
                    ShieldAmount   = shieldAmount,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnGainShield, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = shieldAmount,
                });
            }

            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // AddBuffHandler：处理 AddBuff 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 附加 Buff Handler：处理 EffectType.AddBuff。
    /// 从 effect.Params["buffConfigId"] 读取配置 ID，调用 BuffManager.AddBuff。
    /// </summary>
    public class AddBuffHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue("buffConfigId", out var buffConfigId))
            {
                ctx.RoundLog.Add("[AddBuffHandler] missing buffConfigId.");
                result.Success = false;
                return result;
            }

            // Buff 层数由 HandlerPool 预解析（支持动态表达式），duration 仍从 Params 读取静态值。
            int value = effect.ResolvedValue;
            effect.Params.TryGetValue("duration", out var durationStr);
            int.TryParse(durationStr, out int duration);
            if (duration == 0) duration = -1; // 默认永久

            int appliedCount = 0;
            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                ctx.BuffManager.AddBuff(ctx, target.EntityId, buffConfigId, source.EntityId, value, duration);
                appliedCount++;
            }

            result.Extra["buffConfigId"] = buffConfigId;
            result.Extra["buffValue"] = value;
            result.Extra["buffDuration"] = duration;
            result.Extra["appliedCount"] = appliedCount;
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DrawCardHandler：处理 Draw 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 抽牌 Handler：处理 EffectType.Draw。
    /// </summary>
    public class DrawCardHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            // 抽牌数量由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int drawCount = effect.ResolvedValue;
            if (drawCount <= 0)
            {
                ctx.RoundLog.Add($"[DrawCardHandler] invalid drawCount={drawCount}, skipped.");
                result.Success = false;
                return result;
            }

            var drawn = ctx.CardManager.DrawCards(ctx, source.OwnerPlayerId, drawCount);
            result.Extra["drawnCount"] = drawn.Count;
            ctx.RoundLog.Add($"[DrawCardHandler] {source.OwnerPlayerId} drew {drawn.Count} card(s).");
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // GenerateCardHandler：处理 GenerateCard 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 生成卡牌 Handler：处理 EffectType.GenerateCard。
    /// </summary>
    public class GainEnergyHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            int gainAmount = effect.ResolvedValue;
            if (gainAmount <= 0)
            {
                ctx.RoundLog.Add($"[GainEnergyHandler] invalid gainAmount={gainAmount}, skipped.");
                result.Success = false;
                return result;
            }

            int totalGained = 0;
            foreach (var target in targets)
            {
                var player = ctx.GetPlayer(target.OwnerPlayerId);
                if (player == null)
                    continue;

                player.Energy += gainAmount;
                totalGained += gainAmount;
                result.PerTargetValues[target.EntityId] = gainAmount;
                ctx.RoundLog.Add($"[GainEnergyHandler] {player.PlayerId} energy+={gainAmount}, energy={player.Energy}/{player.MaxEnergy}");
            }

            result.Extra["gainedEnergy"] = totalGained;
            return result;
        }

    }

    public class GenerateCardHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue("configId", out var configId))
            {
                ctx.RoundLog.Add("[GenerateCardHandler] missing configId.");
                result.Success = false;
                return result;
            }

            effect.Params.TryGetValue("targetZone", out var zoneStr);
            var zone = string.IsNullOrWhiteSpace(zoneStr)
                ? CardZone.Hand
                : zoneStr.Trim().ToLowerInvariant() switch
                {
                    "deck" => CardZone.Deck,
                    "discard" => CardZone.Discard,
                    "consume" => CardZone.Consume,
                    _ => CardZone.Hand,
                };
            effect.Params.TryGetValue("count", out var countStr);
            int count = int.TryParse(countStr, out var parsedCount) && parsedCount > 0 ? parsedCount : 1;
            bool tempCard = effect.Params.TryGetValue("tempCard", out var tempCardStr)
                && bool.TryParse(tempCardStr, out var parsedTempCard)
                && parsedTempCard;

            if (!string.IsNullOrWhiteSpace(effect.ValueExpression) && effect.ResolvedValue <= 0)
            {
                ctx.RoundLog.Add($"[GenerateCardHandler] {effect.EffectId} gate resolved to {effect.ResolvedValue}, skip generate {configId}.");
                result.Success = false;
                return result;
            }

            var effectiveTargets = targets.Count > 0 ? targets : new List<Entity> { source };
            var generatedIds = new List<string>();
            foreach (var target in effectiveTargets)
            {
                for (int i = 0; i < count; i++)
                {
                    var generatedCard = ctx.CardManager.GenerateCard(ctx, target.OwnerPlayerId, configId, zone, tempCard);
                    generatedIds.Add(generatedCard.InstanceId);
                }
            }

            result.Extra["generatedCount"] = generatedIds.Count;
            if (generatedIds.Count > 0)
            {
                result.Extra["generatedInstanceId"] = generatedIds[0];
                result.Extra["generatedInstanceIds"] = string.Join(",", generatedIds);
            }
            result.Extra["generatedConfigId"] = configId;
            result.Extra["generatedZone"] = zone.ToString();
            result.Extra["generatedTempCard"] = tempCard;
            ctx.RoundLog.Add($"[GenerateCardHandler] generated {generatedIds.Count} x {configId} to {zone}, temp={tempCard}.");
            return result;
        }
    }

    public class MoveSelectedCardToDeckTopHandler : IEffectHandler
    {
        public const string SelectedCardInstanceIdKey = "selectedCardInstanceId";

        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue(SelectedCardInstanceIdKey, out var selectedCardInstanceId)
                || string.IsNullOrWhiteSpace(selectedCardInstanceId))
            {
                ctx.RoundLog.Add("[MoveSelectedCardToDeckTopHandler] missing selectedCardInstanceId.");
                result.Success = false;
                return result;
            }

            var selectedCard = ctx.CardManager.GetCard(ctx, selectedCardInstanceId);
            if (selectedCard == null)
            {
                ctx.RoundLog.Add($"[MoveSelectedCardToDeckTopHandler] selected card {selectedCardInstanceId} not found.");
                result.Success = false;
                return result;
            }

            if (!selectedCard.OwnerId.Equals(source.OwnerPlayerId, System.StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[MoveSelectedCardToDeckTopHandler] selected card {selectedCardInstanceId} does not belong to {source.OwnerPlayerId}.");
                result.Success = false;
                return result;
            }

            if (selectedCard.Zone != CardZone.Discard)
            {
                ctx.RoundLog.Add($"[MoveSelectedCardToDeckTopHandler] selected card {selectedCardInstanceId} is not in discard (zone={selectedCard.Zone}).");
                result.Success = false;
                return result;
            }

            if (!ctx.CardManager.MoveCardToTopOfDeck(ctx, selectedCard))
            {
                result.Success = false;
                return result;
            }

            result.Extra["selectedCardInstanceId"] = selectedCard.InstanceId;
            result.Extra["selectedCardConfigId"] = selectedCard.GetEffectiveConfigId();
            ctx.RoundLog.Add($"[MoveSelectedCardToDeckTopHandler] moved {selectedCard.InstanceId} to deck top.");
            return result;
        }
    }

    public class UpgradeCardsInHandHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            var player = ctx.GetPlayer(source.OwnerPlayerId);
            if (player == null)
            {
                ctx.RoundLog.Add($"[UpgradeCardsInHandHandler] owner player {source.OwnerPlayerId} not found.");
                result.Success = false;
                return result;
            }

            string lifetimeText = effect.Params.TryGetValue("projectionLifetime", out var rawLifetime)
                && !string.IsNullOrWhiteSpace(rawLifetime)
                ? rawLifetime
                : "EndOfTurn";

            CardProjectionLifetime requestedLifetime = lifetimeText.Equals(nameof(CardProjectionLifetime.EndOfBattle), System.StringComparison.OrdinalIgnoreCase)
                ? CardProjectionLifetime.EndOfBattle
                : CardProjectionLifetime.EndOfTurn;

            int upgradedCount = 0;
            foreach (var card in player.GetCardsInZone(CardZone.Hand))
            {
                string projectionTarget = card.HasProjection
                    ? card.ProjectedConfigId
                    : ctx.GetCardDefinition(card.ConfigId)?.UpgradedConfigId ?? string.Empty;

                if (string.IsNullOrWhiteSpace(projectionTarget))
                    continue;

                if (!card.HasProjection)
                {
                    card.ProjectedConfigId = projectionTarget;
                    card.ProjectionLifetime = requestedLifetime;
                    upgradedCount++;
                    ctx.RoundLog.Add($"[UpgradeCardsInHandHandler] projected {card.InstanceId} -> {projectionTarget} ({requestedLifetime}).");
                    continue;
                }

                if (card.ProjectionLifetime == CardProjectionLifetime.EndOfBattle)
                    continue;

                if (requestedLifetime == CardProjectionLifetime.EndOfBattle)
                {
                    card.ProjectionLifetime = CardProjectionLifetime.EndOfBattle;
                    upgradedCount++;
                    ctx.RoundLog.Add($"[UpgradeCardsInHandHandler] extended {card.InstanceId} projection to EndOfBattle.");
                }
            }

            result.Extra["upgradedCount"] = upgradedCount;
            return result;
        }
    }

    /// <summary>
    /// 标记来源卡牌：本回合结束时若该卡位于弃牌堆，则返回手牌。
    /// </summary>
    public class ReturnSourceCardToHandAtRoundEndHandler : IEffectHandler
    {
        public const string ReturnToHandAtRoundEndKey = "returnToHandAtRoundEnd";
        public const string ReturnToHandMarkedRoundKey = "returnToHandMarkedRound";

        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId)
                || string.IsNullOrWhiteSpace(sourceCardInstanceId))
            {
                ctx.RoundLog.Add("[ReturnSourceCardToHandAtRoundEndHandler] missing sourceCardInstanceId.");
                result.Success = false;
                return result;
            }

            var sourceCard = ctx.CardManager.GetCard(ctx, sourceCardInstanceId);
            if (sourceCard == null)
            {
                ctx.RoundLog.Add($"[ReturnSourceCardToHandAtRoundEndHandler] source card {sourceCardInstanceId} not found.");
                result.Success = false;
                return result;
            }

            sourceCard.ExtraData[ReturnToHandAtRoundEndKey] = true;
            sourceCard.ExtraData[ReturnToHandMarkedRoundKey] = ctx.CurrentRound;
            ctx.RoundLog.Add($"[ReturnSourceCardToHandAtRoundEndHandler] marked {sourceCard.ConfigId} ({sourceCard.InstanceId}) for end-of-round return.");
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LifestealHandler：处理 Lifesteal 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 吸血 Handler：处理 EffectType.Lifesteal。
    ///
    /// 读取同一张牌前置 Damage / Pierce 效果的实际 HP 伤害总量（priorResults），
    /// 按配置百分比（effect.ResolvedValue）为施法者回复生命值。
    ///
    /// 设计约定：
    ///   - Value=100 表示 100% 吸血（即“未被护盾格挡的伤害全部回血”）；
    ///   - 回血量上限为施法者当前缺失生命，不会超过 MaxHp；
    ///   - 仅累计前置效果中类型为 Damage / Pierce 的 TotalRealHpDamage；
    ///     护盾吸收部分不计入（体现“未被护盾格挡”语义）。
    /// </summary>
    public class LifestealHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // ── 累计前置 Damage / Pierce 效果造成的实际 HP 伤害 ──────
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);
            int totalHpDamage = 0;
            foreach (var prior in priorResults)
            {
                if (prior.Success &&
                    (prior.Type == EffectType.Damage || prior.Type == EffectType.Pierce))
                {
                    totalHpDamage += prior.TotalRealHpDamage;
                }
            }

            if (totalHpDamage <= 0)
            {
                ctx.RoundLog.Add("[LifestealHandler] no prior HP damage; skipped.");
                result.Success = false;
                return result;
            }

            // ── 按百分比计算回血量，不超过缺失生命 ─────────────────
            int healAmount = totalHpDamage * effect.ResolvedValue / 100;
            int missing    = source.MaxHp - source.Hp;
            healAmount     = healAmount < missing ? healAmount : missing;

            if (healAmount <= 0)
            {
                ctx.RoundLog.Add($"[LifestealHandler] {source.EntityId} is already at full HP.");
                return result;
            }

            source.Hp           += healAmount;
            result.TotalRealHeal = healAmount;

            ctx.RoundLog.Add(
                $"[LifestealHandler] {source.EntityId} 吸血 {healAmount} HP" +
                $"（实际伤害 {totalHpDamage}×{effect.ResolvedValue}%），当前HP={source.Hp}/{source.MaxHp}");

            ctx.EventBus.Publish(new HealEvent
            {
                SourceEntityId = source.EntityId,
                TargetEntityId = source.EntityId,
                RealHealAmount = healAmount,
                SourceCardInstanceId = sourceCardInstanceId,
            });

            // 触发 OnHealed 时机（与 HealHandler 保持一致，使依赖此时机的 Buff 能响应吸血回血）
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnHealed, new TriggerContext
            {
                SourceEntityId = source.EntityId,
                TargetEntityId = source.EntityId,
                Value          = healAmount,
            });

            return result;
        }
    }
}
