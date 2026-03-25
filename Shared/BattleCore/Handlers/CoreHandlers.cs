
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
    // DamageHandler —— 处理 Damage / PierceDamage 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 伤害 Handler —— 处理 EffectType.Damage 和 EffectType.PierceDamage。
    ///
    /// 执行流程（A→B→C 三阶段，每次对单一目标）：
    ///   Phase A（只读）：计算修正后伤害值，检查无敌。
    ///   Phase B（写入）：护盾吸收 → HP 扣减，记录实际伤害量。
    ///   Phase C（触发）：Fire(AfterDealDamage) + Fire(AfterTakeDamage)，推入 PendingQueue。
    ///
    /// ⚠️ 定策牌 Layer 2 结算时，此 Handler 被逐张调用（按出牌顺序），
    ///    每张牌完整走完 A-B-C 再处理下一张（己方顺序依赖语义）。
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
            // 修复：传 source.OwnerPlayerId，与 BuffManager 注册修正器时使用的 ownerPlayerId 一致
            int modifiedDamage = ctx.ValueModifierManager.Apply(
                effect.Type, source.OwnerPlayerId, ModifierScope.OutgoingDamage, baseDamage);

            foreach (var target in targets)
            {
                // ── Phase A：只读校验 ─────────────────────────────────
                if (!target.IsAlive) continue;

                // 无敌检查
                if (target.IsInvincible)
                {
                    ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} 处于无敌状态，本次伤害无效。");
                    continue;
                }

                // ── Phase B：写入 ────────────────────────────────────
                // 受击方入伤修正（护甲 Add 负值 / 易伤 Mul）
                // 修复：单独以 target.OwnerPlayerId 查受击方修正，与施害方修正分开路由
                int incomingDamage = ctx.ValueModifierManager.Apply(
                    effect.Type, target.OwnerPlayerId, ModifierScope.IncomingDamage, modifiedDamage);

                int remaining = incomingDamage;
                int shieldAbsorbed = 0;
                int armorReduced = 0;

                // ── 防御快照隔离：Layer 2 定策结算时读快照，其他场景（瞬策等）读实时值 ──
                // 快照在 Pre-Layer 2 拍摄，代表"本轮定策开始前"的防御状态，
                // 使双方各自的受伤计算不受对方出牌顺序影响。
                // 修复：此前直接读 target.Shield/Armor（实时值），导致快照机制形同虚设。
                var targetPlayer = ctx.GetPlayer(target.OwnerPlayerId);
                var snapshot     = targetPlayer?.CurrentDefenseSnapshot;

                // 快照防御值：Layer 2 定策时读快照（代表本轮 Layer 2 开始前的状态），
                // 无快照时（瞬策等场景）读实时值。
                // ⚠️ 快照值必须随每次命中递减，否则多张伤害牌会重复消费同一份护盾。
                //    快照和实时值同步扣减，保持语义一致。
                //
                // 设计约定（见 SettlementRules.md §Layer2 快照隔离）：
                //   Layer 2 期间 AfterTakeDamage 等触发器动态生成的护盾会写入实时 target.Shield，
                //   但快照不更新，因此这部分护盾本回合不生效，下回合才参与防御。
                //   这是有意为之的设计：受伤后获得的护盾代表"战斗经验积累"，下回合才转化为防御力。
                int snapshotShield = snapshot != null ? snapshot.Shield : target.Shield;
                int snapshotArmor  = snapshot != null ? snapshot.Armor  : target.Armor;

                // 护盾破裂标记：提前声明，供 Phase C 统一广播使用
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

                    // 护盾破裂判断：本次命中前快照有盾，且本次吸收量耗尽了全部快照盾值
                    // snapshotShield 是递减前的值，shieldAbsorbed == snapshotShield 说明盾被打光
                    shieldBroken = shieldAbsorbed > 0 && shieldAbsorbed == snapshotShield;
                    if (shieldBroken)
                    {
                        ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} 的护盾被击破！");
                        ctx.TriggerManager.Fire(ctx, TriggerTiming.OnShieldBroken, new TriggerContext
                        {
                            SourceEntityId = source.EntityId,
                            TargetEntityId = target.EntityId,
                            Value          = shieldAbsorbed,
                        });
                        // 注意：不在此处广播 DamageDealtEvent，统一在 Phase C 末尾合并广播（避免重复）
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

                ctx.RoundLog.Add(
                    $"[DamageHandler] {source.EntityId} → {target.EntityId}：" +
                    $"基础{baseDamage} 修正后{modifiedDamage} 护盾吸收{shieldAbsorbed} 护甲减{armorReduced} 实际HP伤害{realHpDamage}，" +
                    $"剩余HP={target.Hp}");

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

                // 濒死标记：HP ≤ 0 时仅标记，不在此处触发 OnNearDeath/OnDeath。
                // 死亡链路统一由 RoundManager.CheckDeathAndBattleOver 处理，
                // 避免同一次击杀重复触发濒死/复活/OnDeath 及战斗结束判定。
                if (!target.IsAlive && !target.DeathEventFired)
                {
                    ctx.RoundLog.Add(
                        $"[DamageHandler] {target.EntityId} HP ≤ 0，等待 RoundManager 死亡结算。");
                }
            }

            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // HealHandler —— 处理 Heal 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 治疗 Handler —— 处理 EffectType.Heal。
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

                ctx.RoundLog.Add($"[HealHandler] {source.EntityId} → {target.EntityId}：治疗 {realHeal}，当前HP={target.Hp}");

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
    // ShieldHandler —— 处理 Shield 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 护盾 Handler —— 处理 EffectType.Shield。
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

                ctx.RoundLog.Add($"[ShieldHandler] {source.EntityId} → {target.EntityId}：获得护盾 {shieldAmount}，当前护盾={target.Shield}");

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
    // AddBuffHandler —— 处理 AddBuff 效果（骨架）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 附加 Buff Handler —— 处理 EffectType.AddBuff。
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
                ctx.RoundLog.Add($"[AddBuffHandler] ⚠️ effect.Params 缺少 'buffConfigId'，跳过。");
                result.Success = false;
                return result;
            }

            // Buff 层数由 HandlerPool 预解析（支持动态表达式），duration 仍从 Params 读取静态值
            int value = effect.ResolvedValue;
            effect.Params.TryGetValue("duration", out var durationStr);
            int.TryParse(durationStr, out int duration);
            if (duration == 0) duration = -1; // 默认永久

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                ctx.BuffManager.AddBuff(ctx, target.EntityId, buffConfigId, source.EntityId, value, duration);
            }

            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DrawCardHandler —— 处理 DrawCard 效果（骨架）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 抽牌 Handler —— 处理 EffectType.DrawCard。
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
                ctx.RoundLog.Add($"[DrawCardHandler] ⚠️ 抽牌数量={drawCount} 无效，跳过。");
                result.Success = false;
                return result;
            }

            var drawn = ctx.CardManager.DrawCards(ctx, source.OwnerPlayerId, drawCount);
            result.Extra["drawnCount"] = drawn.Count;
            ctx.RoundLog.Add($"[DrawCardHandler] {source.OwnerPlayerId} 抽取 {drawn.Count} 张牌。");
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // GenerateCardHandler —— 处理 GenerateCard 效果（骨架）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 生成卡牌 Handler —— 处理 EffectType.GenerateCard。
    /// </summary>
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
                ctx.RoundLog.Add($"[GenerateCardHandler] ⚠️ effect.Params 缺少 'configId'，跳过。");
                result.Success = false;
                return result;
            }

            effect.Params.TryGetValue("targetZone", out var zoneStr);
            var zone = zoneStr == "Hand" ? CardZone.Hand : CardZone.Deck;

            var card = ctx.CardManager.GenerateCard(ctx, source.OwnerPlayerId, configId, zone, tempCard: true);
            result.Extra["generatedInstanceId"] = card.InstanceId;
            ctx.RoundLog.Add($"[GenerateCardHandler] 为 {source.OwnerPlayerId} 生成临时卡牌 {configId}（{card.InstanceId}）→ {zone}");
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LifestealHandler —— 处理 Lifesteal 效果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 吸血 Handler —— 处理 EffectType.Lifesteal。
    ///
    /// 读取同一张牌前置 Damage / Pierce 效果的实际 HP 伤害总量（priorResults），
    /// 按配置百分比（effect.ResolvedValue）为施法者回复生命值。
    ///
    /// 设计约定：
    ///   - Value=100 表示 100% 吸血（即"未被护盾格挡的伤害全部回复"）；
    ///   - 回血量上限为施法者当前缺失生命，不会超过 MaxHp；
    ///   - 仅累计前置效果中类型为 Damage / Pierce 的 TotalRealHpDamage，
    ///     护盾吸收部分不计入（体现"未被护盾格挡"语义）。
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
                ctx.RoundLog.Add($"[LifestealHandler] 前置伤害为0，吸血跳过。");
                result.Success = false;
                return result;
            }

            // ── 按百分比计算回血量，不超过缺失生命 ─────────────────
            int healAmount = totalHpDamage * effect.ResolvedValue / 100;
            int missing    = source.MaxHp - source.Hp;
            healAmount     = healAmount < missing ? healAmount : missing;

            if (healAmount <= 0)
            {
                ctx.RoundLog.Add($"[LifestealHandler] {source.EntityId} 已满血，吸血无效。");
                return result;
            }

            source.Hp           += healAmount;
            result.TotalRealHeal = healAmount;

            ctx.RoundLog.Add(
                $"[LifestealHandler] {source.EntityId} 吸血 {healAmount} HP" +
                $"（实际伤害={totalHpDamage}×{effect.ResolvedValue}%），当前HP={source.Hp}/{source.MaxHp}");

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
