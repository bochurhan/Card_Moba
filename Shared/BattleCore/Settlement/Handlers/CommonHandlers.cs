using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    // ═══════════════════════════════════════════════════════════
    // 护甲 Handler —— 瞬时护甲不走 BuffManager（立即修改属性）；
    // 若需要"持续几回合的护甲增益"请走 BuffType.Armor 路径。
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 护甲效果处理器 —— 处理 GainArmor 效果（瞬时加甲）。
    /// AppliesBuff = true 时走 BuffManager（持续护甲）；否则直接修改属性。
    /// </summary>
    public class ArmorHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var armorTarget = target ?? source;
            if (!armorTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[ArmorHandler] 目标玩家{armorTarget.PlayerId}已死亡，护甲无效");
                return;
            }

            if (effect.AppliesBuff)
            {
                int duration = effect.Duration > 0 ? effect.Duration : 1;
                var mgr = ctx.GetBuffManager(armorTarget.PlayerId);
                if (mgr == null)
                {
                    ctx.RoundLog.Add($"[ArmorHandler] 找不到玩家{armorTarget.PlayerId}的BuffManager");
                    return;
                }
                mgr.AddBuff(new BuffInstance
                {
                    BuffId          = $"armor_{source.PlayerId}",
                    BuffType        = BuffType.Armor,
                    SourcePlayerId = source.PlayerId,
                    Value           = effect.Value,
                    Stacks          = 1,
                    RemainingRounds = duration,
                    IsPermanent     = false,
                    IsDispellable   = effect.IsBuffDispellable,
                    TriggerTiming   = BuffTriggerTiming.None,
                    StackRule       = effect.BuffStackRule,
                });
                ctx.RoundLog.Add($"[ArmorHandler] 玩家{armorTarget.PlayerId}获得持续护甲{effect.Value}点，持续{duration}回合");
            }
            else
            {
                armorTarget.Armor += effect.Value;
                ctx.RoundLog.Add($"[ArmorHandler] 玩家{armorTarget.PlayerId}获得{effect.Value}点护甲（当前：{armorTarget.Armor}）");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 力量 Handler —— Buff 类型，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 力量效果处理器 —— 处理 AttackBuff / AttackDebuff。
    /// 通过 BuffManager 施加 Strength Buff，由 BuffManager 管理属性修正和衰减。
    /// </summary>
    public class StrengthHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var strengthTarget = target ?? source;
            if (!strengthTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[StrengthHandler] 目标玩家{strengthTarget.PlayerId}已死亡，力量变化无效");
                return;
            }

            int change = effect.EffectType == Protocol.Enums.EffectType.AttackDebuff
                ? -effect.Value
                : effect.Value;

            if (effect.AppliesBuff)
            {
                int duration = effect.Duration > 0 ? effect.Duration : 1;
                var mgr = ctx.GetBuffManager(strengthTarget.PlayerId);
                if (mgr == null)
                {
                    ctx.RoundLog.Add($"[StrengthHandler] 找不到玩家{strengthTarget.PlayerId}的BuffManager");
                    return;
                }
                mgr.AddBuff(new BuffInstance
                {
                    BuffId          = $"strength_{source.PlayerId}",
                    BuffType        = BuffType.Strength,
                    SourcePlayerId = source.PlayerId,
                    Value           = change,
                    Stacks          = 1,
                    RemainingRounds = duration,
                    IsPermanent     = false,
                    IsDispellable   = effect.IsBuffDispellable,
                    TriggerTiming   = BuffTriggerTiming.None,
                    StackRule       = effect.BuffStackRule,
                });
                ctx.RoundLog.Add($"[StrengthHandler] 玩家{strengthTarget.PlayerId}力量变化{change:+#;-#;0}（持续{duration}回合）");
            }
            else
            {
                strengthTarget.Strength += change;
                ctx.RoundLog.Add($"[StrengthHandler] 玩家{strengthTarget.PlayerId}力量变化{change:+#;-#;0}（当前：{strengthTarget.Strength}）");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 易伤 Handler —— 控制类 Buff，必须走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 易伤效果处理器 —— 处理 Vulnerable 效果。
    /// 通过 BuffManager 施加易伤状态，统一管理 VulnerableStacks 和 IsVulnerable。
    /// </summary>
    public class VulnerableHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive)
            {
                ctx.RoundLog.Add($"[VulnerableHandler] 目标无效，易伤施加失败");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;
            var stackRule = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.StackValue;

            var mgr = ctx.GetBuffManager(target.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[VulnerableHandler] 找不到玩家{target.PlayerId}的BuffManager，易伤失败");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"vulnerable_{source.PlayerId}",
                BuffType        = BuffType.Vulnerable,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value > 0 ? effect.Value : 1,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = stackRule,
            });
            ctx.RoundLog.Add($"[VulnerableHandler] 玩家{target.PlayerId}获得易伤{duration}回合（来源：{source.PlayerId}）");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 虚弱 Handler —— 控制类 Buff，必须走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 虚弱效果处理器 —— 处理 Weak 效果。
    /// 通过 BuffManager 施加虚弱状态，统一管理 WeakStacks 和 IsWeak。
    /// </summary>
    public class WeakHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive)
            {
                ctx.RoundLog.Add($"[WeakHandler] 目标无效，虚弱施加失败");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;
            var stackRule = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.StackValue;

            var mgr = ctx.GetBuffManager(target.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[WeakHandler] 找不到玩家{target.PlayerId}的BuffManager，虚弱失败");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"weak_{source.PlayerId}",
                BuffType        = BuffType.Weak,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value > 0 ? effect.Value : 1,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = stackRule,
            });
            ctx.RoundLog.Add($"[WeakHandler] 玩家{target.PlayerId}获得虚弱{duration}回合（来源：{source.PlayerId}）");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 伤害减免 Handler —— 持续 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 伤害减免处理器 —— 处理 DamageReduction 效果。
    /// 通过 BuffManager 施加减免状态，由 ApplyBuffModifiers 写入 DamageReductionPercent。
    /// </summary>
    public class DamageReductionHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var dmgTarget = target ?? source;
            if (!dmgTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[DamageReductionHandler] 目标玩家{dmgTarget.PlayerId}已死亡，伤害减免无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(dmgTarget.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[DamageReductionHandler] 找不到玩家{dmgTarget.PlayerId}的BuffManager");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"dmgred_{source.PlayerId}",
                BuffType        = BuffType.DamageReduction,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.KeepHighest,
            });
            ctx.RoundLog.Add($"[DamageReductionHandler] 玩家{dmgTarget.PlayerId}获得{effect.Value}%伤害减免，持续{duration}回合");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 无敌 Handler —— 持续 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 无敌效果处理器 —— 处理 Invincible 效果。
    /// 通过 BuffManager 施加无敌状态，由 ApplyBuffModifiers 写入 IsInvincible。
    /// </summary>
    public class InvincibleHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var invTarget = target ?? source;
            if (!invTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[InvincibleHandler] 目标玩家{invTarget.PlayerId}已死亡，无敌无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(invTarget.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[InvincibleHandler] 找不到玩家{invTarget.PlayerId}的BuffManager");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"invincible_{source.PlayerId}",
                BuffType        = BuffType.Invincible,
                SourcePlayerId = source.PlayerId,
                Value           = 0,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.RefreshDuration,
            });
            ctx.RoundLog.Add($"[InvincibleHandler] 玩家{invTarget.PlayerId}获得无敌{duration}回合");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 吸血 Handler —— 触发式 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 吸血效果处理器 —— 处理 Lifesteal 效果。
    /// 向目标 BuffManager 注册 Lifesteal Buff；
    /// TD-01 后由 BuffManager.AddBuff 自动在 TriggerManager 注册 AfterDealDamage 触发器，
    /// 每次造成伤害后自动执行治疗。
    /// </summary>
    public class LifestealHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var lsTarget = target ?? source;
            if (!lsTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[LifestealHandler] 目标玩家{lsTarget.PlayerId}已死亡，吸血无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(lsTarget.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[LifestealHandler] 找不到玩家{lsTarget.PlayerId}的BuffManager");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"lifesteal_{source.PlayerId}",
                BuffType        = BuffType.Lifesteal,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value,     // 吸血百分比
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.OnDamageDealt,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.RefreshDuration,
            });
            ctx.RoundLog.Add($"[LifestealHandler] 玩家{lsTarget.PlayerId}获得{effect.Value}%吸血，持续{duration}回合");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 反伤 Handler —— 触发式 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 反伤效果处理器 —— 处理 Thorns 效果。
    /// 向目标 BuffManager 注册 Thorns Buff；
    /// TD-01 后由 BuffManager.AddBuff 自动在 TriggerManager 注册 AfterTakeDamage 触发器，
    /// 每次受到伤害后自动对攻击方执行反伤。
    /// </summary>
    public class ThornsHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var thornsTarget = target ?? source;
            if (!thornsTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[ThornsHandler] 目标玩家{thornsTarget.PlayerId}已死亡，反伤无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(thornsTarget.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[ThornsHandler] 找不到玩家{thornsTarget.PlayerId}的BuffManager");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"thorns_{source.PlayerId}",
                BuffType        = BuffType.Thorns,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value,     // 反伤固定值
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.OnDamageTaken,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.StackValue,
            });
            ctx.RoundLog.Add($"[ThornsHandler] 玩家{thornsTarget.PlayerId}获得{effect.Value}点反伤，持续{duration}回合");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 沉默 Handler —— 控制类 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 沉默效果处理器 —— 处理 Silence 效果。
    /// 通过 BuffManager 施加沉默状态，由 ApplyBuffModifiers 写入 IsSilenced。
    /// </summary>
    public class SilenceHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive)
            {
                ctx.RoundLog.Add($"[SilenceHandler] 目标无效，沉默施加失败");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(target.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[SilenceHandler] 找不到玩家{target.PlayerId}的BuffManager，沉默失败");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"silence_{source.PlayerId}",
                BuffType        = BuffType.Silence,
                SourcePlayerId = source.PlayerId,
                Value           = 0,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.RefreshDuration,
            });
            ctx.RoundLog.Add($"[SilenceHandler] 玩家{target.PlayerId}被沉默{duration}回合（来源：{source.PlayerId}）");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 减速 Handler —— 控制类 Buff（暂时通过 Root 近似），走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 减速效果处理器 —— 处理 Slow 效果。
    /// 通过 BuffManager 施加减速状态，由 RoundManager 读取 IsSlowed 决定行动优先级。
    /// </summary>
    public class SlowHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            if (target == null || !target.IsAlive)
            {
                ctx.RoundLog.Add($"[SlowHandler] 目标无效，减速施加失败");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(target.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[SlowHandler] 找不到玩家{target.PlayerId}的BuffManager，减速失败");
                return;
            }

            // Slow 暂时映射到 Root（禁锢换路），未来若有独立 Slow BuffType 可替换
            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"slow_{source.PlayerId}",
                BuffType        = BuffType.Root,
                SourcePlayerId = source.PlayerId,
                Value           = 0,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.RefreshDuration,
            });
            // Root BuffType 的 ApplyBuffModifiers 暂未覆盖 IsSlowed，临时直写
            target.IsSlowed = true;
            ctx.RoundLog.Add($"[SlowHandler] 玩家{target.PlayerId}被减速{duration}回合（来源：{source.PlayerId}）");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 受击获甲 Handler —— 触发式 Buff，走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 受击获甲处理器 —— 处理 ArmorOnHit 效果。
    /// 通过 BuffManager 注册 OnDamageTaken 触发型 Buff，受伤时自动获甲。
    /// </summary>
    public class ArmorOnHitHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var buffTarget = target ?? source;
            if (!buffTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[ArmorOnHitHandler] 目标玩家{buffTarget.PlayerId}已死亡，受击获甲无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;

            var mgr = ctx.GetBuffManager(buffTarget.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[ArmorOnHitHandler] 找不到玩家{buffTarget.PlayerId}的BuffManager");
                return;
            }

            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"armoronhit_{source.PlayerId}",
                BuffType        = BuffType.Armor,
                SourcePlayerId = source.PlayerId,
                Value           = effect.Value,
                Stacks          = 1,
                RemainingRounds = duration,
                IsPermanent     = false,
                IsDispellable   = effect.AppliesBuff ? effect.IsBuffDispellable : true,
                TriggerTiming   = BuffTriggerTiming.OnDamageTaken,  // 受伤时触发
                StackRule       = effect.AppliesBuff ? effect.BuffStackRule : BuffStackRule.Independent,
            });
            ctx.RoundLog.Add($"[ArmorOnHitHandler] 玩家{buffTarget.PlayerId}获得受击获甲{effect.Value}点，持续{duration}回合");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 以下 Handler 为瞬时效果，不走 BuffManager，保持直写
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 抽牌效果处理器 —— 处理 Draw 效果（瞬时，无 Buff）。
    /// 若目标身上有 NoDrawThisTurn Buff，则本次抽牌被拦截。
    /// </summary>
    public class DrawHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var drawTarget = target ?? source;
            if (!drawTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[DrawHandler] 目标玩家{drawTarget.PlayerId}已死亡，抽牌无效");
                return;
            }

            // ── 检查"禁止抽牌"Debuff ──
            var buffMgr = ctx.GetBuffManager(drawTarget.PlayerId);
            if (buffMgr != null && buffMgr.HasBuffType(BuffType.NoDrawThisTurn))
            {
                ctx.RoundLog.Add($"[DrawHandler] 玩家{drawTarget.PlayerId}持有「禁止抽牌」状态，本次抽牌被取消");
                return;
            }

            int drawCount = effect.Value > 0 ? effect.Value : 1;
            int handBefore = drawTarget.Hand.Count;
            int actualDrawn = drawTarget.DrawCards(drawCount, ctx.Random);

            if (actualDrawn == drawCount)
            {
                ctx.RoundLog.Add($"[DrawHandler] 玩家{drawTarget.PlayerId}抽{actualDrawn}张牌（手牌：{handBefore}→{drawTarget.Hand.Count}）");
            }
            else if (actualDrawn > 0)
            {
                string reason = drawTarget.Hand.Count >= PlayerBattleState.MaxHandSize ? "手牌已满" : "牌库弃牌堆均空";
                ctx.RoundLog.Add($"[DrawHandler] 玩家{drawTarget.PlayerId}尝试抽{drawCount}张，实际抽{actualDrawn}张（{reason}）");
            }
            else
            {
                string reason = drawTarget.Hand.Count >= PlayerBattleState.MaxHandSize ? "手牌已满" : "牌库弃牌堆均空";
                ctx.RoundLog.Add($"[DrawHandler] 玩家{drawTarget.PlayerId}无法抽牌（{reason}）");
            }
        }
    }

    /// <summary>
    /// 能量效果处理器 —— 处理 GainEnergy 效果（瞬时，无 Buff）。
    /// </summary>
    public class EnergyHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var energyTarget = target ?? source;
            if (!energyTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[EnergyHandler] 目标玩家{energyTarget.PlayerId}已死亡，能量恢复无效");
                return;
            }

            int before = energyTarget.Energy;
            energyTarget.Energy += effect.Value;
            ctx.RoundLog.Add($"[EnergyHandler] 玩家{energyTarget.PlayerId}获得{effect.Value}点能量（{before}→{energyTarget.Energy}）");
        }
    }

    /// <summary>
    /// 弃牌效果处理器 —— 处理 Discard 效果（瞬时，无 Buff）。
    /// </summary>
    public class DiscardHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            var discardTarget = target ?? source;
            if (!discardTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[DiscardHandler] 目标玩家{discardTarget.PlayerId}已死亡，弃牌无效");
                return;
            }

            int discardCount = effect.Value > 0 ? effect.Value : 1;
            int actualDiscarded = 0;

            for (int i = 0; i < discardCount && discardTarget.Hand.Count > 0; i++)
            {
                int lastIndex = discardTarget.Hand.Count - 1;
                var discardedCard = discardTarget.Hand[lastIndex];
                discardTarget.Hand.RemoveAt(lastIndex);
                discardTarget.DiscardPile.Add(discardedCard);
                actualDiscarded++;
            }

            ctx.RoundLog.Add($"[DiscardHandler] 玩家{discardTarget.PlayerId}弃置{actualDiscarded}张牌");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 禁止抽牌 Handler —— 施加 NoDrawThisTurn Buff（本回合生效）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 禁止抽牌效果处理器 —— 处理 BanDraw 效果。
    /// 对施法者自身施加 NoDrawThisTurn Buff（持续1回合），
    /// DrawHandler 在执行时会检测该 Buff 并拦截后续所有抽牌。
    /// </summary>
    public class BanDrawHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            // BanDraw 始终作用于施法者自身
            if (!source.IsAlive)
            {
                ctx.RoundLog.Add($"[BanDrawHandler] 施法者{source.PlayerId}已死亡，禁止抽牌施加无效");
                return;
            }

            var mgr = ctx.GetBuffManager(source.PlayerId);
            if (mgr == null)
            {
                ctx.RoundLog.Add($"[BanDrawHandler] 找不到玩家{source.PlayerId}的BuffManager");
                return;
            }

            // 持续 1 回合（当前回合剩余时间内），不可叠加
            mgr.AddBuff(new BuffInstance
            {
                BuffId          = $"nodraw_{source.PlayerId}",
                BuffType        = BuffType.NoDrawThisTurn,
                SourcePlayerId = source.PlayerId,
                Value           = 0,
                Stacks          = 1,
                RemainingRounds = 1,
                IsPermanent     = false,
                IsDispellable   = false,   // 不可被驱散，机制型限制
                TriggerTiming   = BuffTriggerTiming.None,
                StackRule       = BuffStackRule.RefreshDuration,
            });
            ctx.RoundLog.Add($"[BanDrawHandler] 玩家{source.PlayerId}本回合禁止抽牌");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 力量翻倍 Handler —— 消耗型瞬时效果，不走 BuffManager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 力量翻倍效果处理器 —— 处理 DoubleStrength 效果（「突破极限」专属）。
    /// 
    /// 规则：
    ///   - 将施法者当前 Strength 值×2（若为0则保持0，翻倍无意义）
    ///   - 属于消耗型效果（卡牌打出后消耗，不进入弃牌堆，由 CardConfig.IsExhaust 控制）
    ///   - 瞬时执行，不挂 Buff，不受持续时间影响
    /// </summary>
    public class DoubleStrengthHandler : IEffectHandler
    {
        public void Execute(
            PlayedCard card,
            CardEffect effect,
            PlayerBattleState source,
            PlayerBattleState? target,
            BattleContext ctx)
        {
            // 始终作用于施法者自身，忽略 target
            if (!source.IsAlive)
            {
                ctx.RoundLog.Add($"[DoubleStrengthHandler] 施法者{source.PlayerId}已死亡，力量翻倍无效");
                return;
            }

            int before = source.Strength;

            if (before == 0)
            {
                // 力量为0时翻倍没有意义，给出提示但不报错
                ctx.RoundLog.Add($"[DoubleStrengthHandler] 玩家{source.PlayerId}当前力量为0，翻倍无效果");
                return;
            }

            source.Strength = before * 2;
            ctx.RoundLog.Add($"[DoubleStrengthHandler] 玩家{source.PlayerId}力量翻倍：{before} → {source.Strength}");
        }
    }
}
