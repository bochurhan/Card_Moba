using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;

#pragma warning disable CS8632 // nullable 注解警告在非 nullable 上下文中使用

namespace CardMoba.BattleCore.Settlement.Handlers
{
    /// <summary>
    /// 护甲效果处理器 —— 处理 GainArmor 效果。
    /// 护甲减少收到的伤害（百分比或固定值）。
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

            int armorAmount = effect.Value;
            armorTarget.Armor += armorAmount;

            ctx.RoundLog.Add($"[ArmorHandler] 玩家{armorTarget.PlayerId}获得{armorAmount}点护甲（当前护甲：{armorTarget.Armor}）");
        }
    }

    /// <summary>
    /// 力量效果处理器 —— 处理 GainStrength / ReduceStrength 效果。
    /// 力量影响造成的伤害。
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

            int change = effect.Value;
            if (effect.Type == Protocol.Enums.EffectType.ReduceStrength)
            {
                change = -change;
            }

            strengthTarget.Strength += change;
            ctx.RoundLog.Add($"[StrengthHandler] 玩家{strengthTarget.PlayerId}力量变化{change:+#;-#;0}（当前力量：{strengthTarget.Strength}）");
        }
    }

    /// <summary>
    /// 易伤效果处理器 —— 处理 Vulnerable 效果。
    /// 易伤状态下受到伤害增加。
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
            target.IsVulnerable = true;
            target.VulnerableRounds = duration;

            ctx.RoundLog.Add($"[VulnerableHandler] 玩家{target.PlayerId}获得易伤{duration}回合");
        }
    }

    /// <summary>
    /// 虚弱效果处理器 —— 处理 Weak 效果。
    /// 虚弱状态下造成伤害降低。
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
            target.IsWeak = true;
            target.WeakRounds = duration;

            ctx.RoundLog.Add($"[WeakHandler] 玩家{target.PlayerId}获得虚弱{duration}回合");
        }
    }

    /// <summary>
    /// 伤害减免处理器 —— 处理 DamageReduction 效果。
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
            var dmgReduceTarget = target ?? source;
            if (!dmgReduceTarget.IsAlive)
            {
                ctx.RoundLog.Add($"[DamageReductionHandler] 目标玩家{dmgReduceTarget.PlayerId}已死亡，伤害减免无效");
                return;
            }

            int duration = effect.Duration > 0 ? effect.Duration : 1;
            dmgReduceTarget.DamageReduction = effect.Value;
            dmgReduceTarget.DamageReductionRounds = duration;

            ctx.RoundLog.Add($"[DamageReductionHandler] 玩家{dmgReduceTarget.PlayerId}获得{effect.Value}%伤害减免，持续{duration}回合");
        }
    }

    /// <summary>
    /// 无敌效果处理器 —— 处理 Invincible 效果。
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
            invTarget.IsInvincible = true;
            invTarget.InvincibleRounds = duration;

            ctx.RoundLog.Add($"[InvincibleHandler] 玩家{invTarget.PlayerId}获得无敌{duration}回合");
        }
    }

    /// <summary>
    /// 吸血效果处理器 —— 处理 Lifesteal 效果。
    /// 为玩家附加吸血 Buff。
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
            lsTarget.LifestealPercent = effect.Value;
            lsTarget.LifestealRounds = duration;

            ctx.RoundLog.Add($"[LifestealHandler] 玩家{lsTarget.PlayerId}获得{effect.Value}%吸血，持续{duration}回合");
        }
    }

    /// <summary>
    /// 反伤效果处理器 —— 处理 Thorns 效果。
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
            thornsTarget.ThornsValue = effect.Value;
            thornsTarget.ThornsRounds = duration;

            ctx.RoundLog.Add($"[ThornsHandler] 玩家{thornsTarget.PlayerId}获得{effect.Value}点反伤，持续{duration}回合");
        }
    }

    /// <summary>
    /// 沉默效果处理器 —— 处理 Silence 效果。
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
            target.IsSilenced = true;
            target.SilencedRounds = duration;

            ctx.RoundLog.Add($"[SilenceHandler] 玩家{target.PlayerId}被沉默{duration}回合");
        }
    }

    /// <summary>
    /// 抽牌效果处理器 —— 处理 Draw 效果。
    /// 采用杀戮尖塔风格的抽牌机制：
    /// - 从牌库抽牌，不足时自动洗入弃牌堆继续抽
    /// - 手牌达到上限（10张）时停止抽牌
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

            int drawCount = effect.Value > 0 ? effect.Value : 1;
            int handBefore = drawTarget.Hand.Count;

            // 调用 PlayerBattleState 的抽牌方法
            int actualDrawn = drawTarget.DrawCards(drawCount, ctx.Random);

            // 记录日志
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
    /// 能量效果处理器 —— 处理 GainEnergy 效果。
    /// 能量可以临时超过上限（本回合有效），下回合开始时恢复到 MaxEnergy。
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

            int energyAmount = effect.Value;
            int energyBefore = energyTarget.Energy;
            energyTarget.Energy += energyAmount;

            // 能量可以临时超过上限，下回合开始时会重置为 MaxEnergy
            ctx.RoundLog.Add($"[EnergyHandler] 玩家{energyTarget.PlayerId}获得{energyAmount}点能量（{energyBefore}→{energyTarget.Energy}）");
        }
    }
}
