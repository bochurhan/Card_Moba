
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
            List<EffectResult> priorResults)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            bool isPierce = effect.Type == EffectType.Pierce;

            // 基础伤害值由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int baseDamage    = effect.ResolvedValue;
            // 应用力量/虚弱修正（ValueModifierManager）
            int modifiedDamage = ctx.ValueModifierManager.Apply(effect.Type, source.EntityId, baseDamage);

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
                int remaining = modifiedDamage;
                int shieldAbsorbed = 0;
                int armorReduced = 0;

                // 护盾吸收（穿透伤害不跳过护盾，只跳过护甲）
                if (target.Shield > 0)
                {
                    shieldAbsorbed = remaining < target.Shield ? remaining : target.Shield;
                    target.Shield -= shieldAbsorbed;
                    remaining -= shieldAbsorbed;

                    bool shieldBroken = target.Shield == 0 && shieldAbsorbed > 0;
                    if (shieldBroken)
                    {
                        ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} 的护盾被击破！");
                        ctx.TriggerManager.Fire(ctx, TriggerTiming.OnShieldBroken, new TriggerContext
                        {
                            SourceEntityId = source.EntityId,
                            TargetEntityId = target.EntityId,
                            Value          = shieldAbsorbed,
                        });
                        ctx.EventBus.Publish(new DamageDealtEvent
                        {
                            SourceEntityId   = source.EntityId,
                            TargetEntityId   = target.EntityId,
                            ShieldBroken     = true,
                        });
                    }
                }

                // 护甲减伤（穿透伤害跳过护甲）
                if (!isPierce && target.Armor > 0 && remaining > 0)
                {
                    armorReduced = remaining < target.Armor ? remaining : target.Armor;
                    target.Armor -= armorReduced;
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

                // EventBus 广播
                ctx.EventBus.Publish(new DamageDealtEvent
                {
                    SourceEntityId   = source.EntityId,
                    TargetEntityId   = target.EntityId,
                    BaseDamage       = modifiedDamage,
                    RealHpDamage     = realHpDamage,
                    ShieldAbsorbed   = shieldAbsorbed,
                    ArmorReduced     = armorReduced,
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

                // 濒死/死亡检查
                if (!target.IsAlive)
                {
                    ctx.TriggerManager.Fire(ctx, TriggerTiming.OnNearDeath, new TriggerContext
                    {
                        SourceEntityId = target.EntityId,
                        TargetEntityId = source.EntityId,
                        Value          = realHpDamage,
                    });
                    if (!target.IsAlive) // 复活技可能在 OnNearDeath 时将 HP 恢复
                    {
                        ctx.TriggerManager.Fire(ctx, TriggerTiming.OnDeath, new TriggerContext
                        {
                            SourceEntityId = target.EntityId,
                            TargetEntityId = source.EntityId,
                        });
                        ctx.EventBus.Publish(new EntityDeathEvent
                        {
                            EntityId       = target.EntityId,
                            KillerEntityId = source.EntityId,
                        });
                    }
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
            List<EffectResult> priorResults)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 治疗量由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int baseHeal = effect.ResolvedValue;

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
            List<EffectResult> priorResults)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 护盾量由 HandlerPool 预解析填入 effect.ResolvedValue（支持动态表达式）
            int shieldAmount = effect.ResolvedValue;

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
            List<EffectResult> priorResults)
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
            List<EffectResult> priorResults)
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
            List<EffectResult> priorResults)
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
}
