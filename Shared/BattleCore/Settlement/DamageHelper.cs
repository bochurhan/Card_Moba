using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Trigger;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告

namespace CardMoba.BattleCore.Settlement
{
    /// <summary>
    /// 伤害处理工具类 —— 统一的伤害应用入口。
    /// 
    /// 职责：
    /// - 计算最终伤害（攻击方加成 + 防守方减免）
    /// - 处理护盾吸收
    /// - 扣除生命值
    /// - 触发伤害相关回调（吸血、反伤等）
    /// - 记录伤害事件
    /// 
    /// 设计原则：
    /// - 所有伤害都应通过此类处理，确保被动效果正确触发
    /// - 支持 triggerCallbacks 参数防止无限递归（如反伤触发反伤）
    /// </summary>
    public static class DamageHelper
    {
        /// <summary>
        /// 应用伤害 —— 统一的伤害处理入口。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="sourceId">伤害来源玩家ID</param>
        /// <param name="targetId">伤害目标玩家ID</param>
        /// <param name="baseDamage">基础伤害值</param>
        /// <param name="triggerCallbacks">是否触发回调（吸血、反伤），设为 false 可防止递归</param>
        /// <param name="ignoreArmor">是否无视护甲（穿透伤害）</param>
        /// <param name="damageSource">伤害来源描述（用于日志）</param>
        /// <returns>实际造成的伤害值（扣血部分，不含护盾吸收）</returns>
        public static int ApplyDamage(
            BattleContext ctx,
            string sourceId,
            string targetId,
            int baseDamage,
            bool triggerCallbacks = true,
            bool ignoreArmor = false,
            string damageSource = "未知")
        {
            var source = ctx.GetPlayer(sourceId);
            var target = ctx.GetPlayer(targetId);

            if (source == null || target == null)
            {
                ctx.RoundLog.Add($"[DamageHelper] 无效的伤害：来源={sourceId}, 目标={targetId}");
                return 0;
            }

            if (!target.IsAlive || target.IsMarkedForDeath)
            {
                ctx.RoundLog.Add($"[DamageHelper] 目标 {targetId} 已死亡或濒死，跳过伤害");
                return 0;
            }

            // ══════════════════════════════════════════════════════════
            // 1. 计算输出伤害（来源方加成）
            // ══════════════════════════════════════════════════════════
            int outgoingDamage = source.CalculateOutgoingDamage(baseDamage);

            // 触发 BeforeDealDamage 触发器（可能修改伤害）
            if (triggerCallbacks)
            {
                var beforeDealCtx = new TriggerContext
                {
                    BattleContext = ctx,
                    Timing = TriggerTiming.BeforeDealDamage,
                    SourcePlayerId = sourceId,
                    TargetPlayerId = targetId,
                    Value = outgoingDamage,
                    ModifiedValue = outgoingDamage
                };
                ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.BeforeDealDamage, beforeDealCtx);
                
                if (beforeDealCtx.ShouldCancel)
                {
                    ctx.RoundLog.Add($"[DamageHelper] 伤害被触发器取消：{damageSource}");
                    return 0;
                }
                outgoingDamage = beforeDealCtx.ModifiedValue;
            }

            // ══════════════════════════════════════════════════════════
            // 2. 计算实际受伤（目标方减免）
            // ══════════════════════════════════════════════════════════
            int actualDamage = target.CalculateIncomingDamage(outgoingDamage, ignoreArmor);

            // 触发 BeforeTakeDamage 触发器（可能修改伤害）
            if (triggerCallbacks)
            {
                var beforeTakeCtx = new TriggerContext
                {
                    BattleContext = ctx,
                    Timing = TriggerTiming.BeforeTakeDamage,
                    SourcePlayerId = sourceId,
                    TargetPlayerId = targetId,
                    Value = actualDamage,
                    ModifiedValue = actualDamage
                };
                ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.BeforeTakeDamage, beforeTakeCtx);
                
                if (beforeTakeCtx.ShouldCancel)
                {
                    ctx.RoundLog.Add($"[DamageHelper] 伤害被触发器取消：{damageSource}");
                    return 0;
                }
                actualDamage = beforeTakeCtx.ModifiedValue;
            }

            // ══════════════════════════════════════════════════════════
            // 3. 护盾吸收
            // ══════════════════════════════════════════════════════════
            int shieldAbsorbed = 0;
            if (target.Shield > 0 && actualDamage > 0)
            {
                shieldAbsorbed = target.Shield >= actualDamage ? actualDamage : target.Shield;
                target.Shield -= shieldAbsorbed;
                actualDamage -= shieldAbsorbed;

                ctx.RoundLog.Add($"[DamageHelper] {damageSource} 被护盾吸收 {shieldAbsorbed} 点");

                // 护盾破碎触发
                if (target.Shield <= 0 && triggerCallbacks)
                {
                    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.OnShieldBroken, new TriggerContext
                    {
                        BattleContext = ctx,
                        Timing = TriggerTiming.OnShieldBroken,
                        SourcePlayerId = sourceId,
                        TargetPlayerId = targetId,
                        Value = shieldAbsorbed
                    });
                }
            }

            // ══════════════════════════════════════════════════════════
            // 4. 扣血
            // ══════════════════════════════════════════════════════════
            int hpDamage = actualDamage;
            if (hpDamage > 0)
            {
                target.Hp -= hpDamage;
                target.DamageTakenThisRound += hpDamage;
                source.DamageDealtThisRound += hpDamage;

                ctx.RoundLog.Add($"[DamageHelper] {damageSource}: {sourceId} → {targetId} 造成 {hpDamage} 点伤害（血量: {target.Hp + hpDamage} → {target.Hp}）");

                // 记录战斗事件
                ctx.EventRecorder.RecordDamage(sourceId, targetId, hpDamage, false, damageSource);
            }

            // ══════════════════════════════════════════════════════════
            // 5. 触发伤害后回调（吸血、反伤等由 TriggerManager 统一调度）
            // ══════════════════════════════════════════════════════════
            if (triggerCallbacks && hpDamage > 0)
            {
                // 攻击方造成伤害后触发器（含 Lifesteal Buff 注册的吸血触发器）
                // 注意：AfterDealDamage 中 SourcePlayerId = 攻击方
                ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.AfterDealDamage, new TriggerContext
                {
                    BattleContext = ctx,
                    Timing = TriggerTiming.AfterDealDamage,
                    SourcePlayerId = sourceId,
                    TargetPlayerId = targetId,
                    Value = hpDamage
                });

                // 防守方受到伤害后触发器（含 Thorns Buff 注册的反伤触发器）
                // 注意：AfterTakeDamage 中 SourcePlayerId = 受伤方，TargetPlayerId = 攻击方
                ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.AfterTakeDamage, new TriggerContext
                {
                    BattleContext = ctx,
                    Timing = TriggerTiming.AfterTakeDamage,
                    SourcePlayerId = targetId,
                    TargetPlayerId = sourceId,
                    Value = hpDamage
                });
            }

            // ══════════════════════════════════════════════════════════
            // 6. 检查濒死状态
            // ══════════════════════════════════════════════════════════
            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.IsMarkedForDeath = true;
                source.HasKilledThisRound = true;
                ctx.RoundLog.Add($"[DamageHelper] {targetId} 进入濒死状态");

                // 触发濒死触发器（含 Resurrection Buff 注册的复活触发器，由 TriggerManager 统一调度）
                if (triggerCallbacks)
                {
                    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.OnNearDeath, new TriggerContext
                    {
                        BattleContext = ctx,
                        Timing = TriggerTiming.OnNearDeath,
                        SourcePlayerId = sourceId,
                        TargetPlayerId = targetId,
                        Value = hpDamage
                    });

                    // 如果濒死被触发器取消（如复活 Buff），则清除标记
                    if (target.Hp > 0)
                    {
                        target.IsMarkedForDeath = false;
                        ctx.RoundLog.Add($"[DamageHelper] {targetId} 被复活效果救活");
                    }
                }
            }

            return hpDamage;
        }

        /// <summary>
        /// 应用治疗 —— 统一的治疗处理入口。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="sourceId">治疗来源玩家ID</param>
        /// <param name="targetId">治疗目标玩家ID</param>
        /// <param name="healAmount">治疗量</param>
        /// <param name="healSource">治疗来源描述（用于日志）</param>
        /// <returns>实际治疗量</returns>
        public static int ApplyHeal(
            BattleContext ctx,
            string sourceId,
            string targetId,
            int healAmount,
            string healSource = "未知")
        {
            var target = ctx.GetPlayer(targetId);

            if (target == null || !target.IsAlive)
            {
                return 0;
            }

            int oldHp = target.Hp;
            target.Hp += healAmount;
            if (target.Hp > target.MaxHp)
                target.Hp = target.MaxHp;

            int actualHeal = target.Hp - oldHp;

            if (actualHeal > 0)
            {
                ctx.RoundLog.Add($"[DamageHelper] {healSource}: {targetId} 恢复 {actualHeal} 点生命（{oldHp} → {target.Hp}）");

                // 触发治疗触发器
                ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.OnHealed, new TriggerContext
                {
                    BattleContext = ctx,
                    Timing = TriggerTiming.OnHealed,
                    SourcePlayerId = sourceId,
                    TargetPlayerId = targetId,
                    Value = actualHeal
                });
            }

            return actualHeal;
        }

        /// <summary>
        /// 应用护盾 —— 统一的护盾处理入口。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="sourceId">护盾来源玩家ID</param>
        /// <param name="targetId">护盾目标玩家ID</param>
        /// <param name="shieldAmount">护盾值</param>
        /// <param name="shieldSource">护盾来源描述（用于日志）</param>
        /// <returns>实际获得的护盾值</returns>
        public static int ApplyShield(
            BattleContext ctx,
            string sourceId,
            string targetId,
            int shieldAmount,
            string shieldSource = "未知")
        {
            var target = ctx.GetPlayer(targetId);

            if (target == null || !target.IsAlive)
            {
                return 0;
            }

            int oldShield = target.Shield;
            target.Shield += shieldAmount;

            ctx.RoundLog.Add($"[DamageHelper] {shieldSource}: {targetId} 获得 {shieldAmount} 点护盾（{oldShield} → {target.Shield}）");

            // 触发护盾获得触发器
            ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.OnGainShield, new TriggerContext
            {
                BattleContext = ctx,
                Timing = TriggerTiming.OnGainShield,
                SourcePlayerId = sourceId,
                TargetPlayerId = targetId,
                Value = shieldAmount
            });

            return shieldAmount;
        }
    }
}
