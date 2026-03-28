#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Costs;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Managers;
using CardMoba.BattleCore.Rules.Play;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Core
{
    public class CommittedPlanCard
    {
        public string PlayerId { get; set; } = string.Empty;
        public string CardInstanceId { get; set; } = string.Empty;
        public int CommittedCost { get; set; }
        public Dictionary<string, string> RuntimeParams { get; set; } = new Dictionary<string, string>();
    }

    public class RoundManager
    {
        private readonly SettlementEngine _settlement;
        private readonly PlayCostResolver _playCostResolver;

        public int CurrentRound { get; private set; }
        public bool IsBattleOver { get; private set; }
        public string? WinnerId { get; private set; }

        public RoundManager()
        {
            _settlement = new SettlementEngine();
            _playCostResolver = new PlayCostResolver();
        }

        public void InitBattle(BattleContext ctx)
        {
            CurrentRound = 0;
            IsBattleOver = false;
            WinnerId = null;
            ctx.PendingPlanSnapshots.Clear();

            ctx.RoundLog.Add("[RoundManager] 战斗初始化完成。");
            ctx.EventBus.Publish(new BattleStartEvent
            {
                BattleId = ctx.BattleId,
                Round = 0,
            });
        }

        public void BeginRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            CurrentRound++;
            ctx.PendingPlanSnapshots.Clear();

            ctx.CurrentRound = CurrentRound;
            ctx.CurrentPhase = BattleContext.BattlePhase.RoundStart;

            foreach (var player in ctx.AllPlayers.Values)
            {
                player.PlayedCardCountThisRound = 0;
                player.PlayedDamageCardCountThisRound = 0;
                player.PlayedDefenseCardCountThisRound = 0;
                player.PlayedCounterCardCountThisRound = 0;
                player.CorruptionFreePlaysRemainingThisRound = 0;
            }

            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合开始。");
            ctx.EventBus.Publish(new RoundStartEvent { Round = CurrentRound });

            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundStart, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            ctx.PlayRules.ResetTurnState(ctx);

            ctx.CardManager.OnRoundStart(ctx, CurrentRound);
            _settlement.DrainPendingQueue(ctx);

            ctx.CurrentPhase = BattleContext.BattlePhase.PlayerAction;
            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合准备完成。");
        }

        public List<EffectResult> PlayInstantCard(
            BattleContext ctx,
            string playerId,
            string cardInstanceId,
            Dictionary<string, string>? runtimeParams = null)
        {
            if (IsBattleOver) return new List<EffectResult>();

            var card = ctx.CardManager.GetCard(ctx, cardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[RoundManager] 找不到瞬策牌实例 {cardInstanceId}。");
                return new List<EffectResult>();
            }

            if (!card.OwnerId.Equals(playerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[RoundManager] 瞬策牌 {cardInstanceId} 不属于玩家 {playerId}。");
                return new List<EffectResult>();
            }

            var effectiveConfigId = card.GetEffectiveConfigId();
            var effects = ResolveCardEffects(ctx, card);
            if (effects == null)
            {
                ctx.RoundLog.Add($"[RoundManager] 找不到瞬策牌 {cardInstanceId}（{effectiveConfigId}）的卡牌定义。");
                return new List<EffectResult>();
            }

            var instantRules = ctx.PlayRules.Resolve(ctx, playerId, effects, PlayOrigin.PlayerHandPlay);
            if (!instantRules.Allowed)
            {
                ctx.RoundLog.Add($"[RoundManager] 瞬策牌 {cardInstanceId} 被限制，原因：{instantRules.BlockReason}");
                return new List<EffectResult>();
            }

            StampCardSourceMetadata(ctx, effects, card, runtimeParams);
            ctx.RoundLog.Add($"[RoundManager] 玩家 {playerId} 打出瞬策牌 {cardInstanceId}（{effectiveConfigId}）。");

            var results = _settlement.ResolveInstantFromCard(ctx, playerId, card, effects);
            if (results.Count > 0 || card.Zone != CardZone.Hand)
                RecordPlayedCardStats(ctx, playerId, card, effects);
            CheckDeathAndBattleOver(ctx);
            return results;
        }

        public bool CommitPlanCard(BattleContext ctx, CommittedPlanCard planCard)
        {
            if (IsBattleOver) return false;

            var card = ctx.CardManager.GetCard(ctx, planCard.CardInstanceId);
            if (card == null)
            {
                ctx.RoundLog.Add($"[RoundManager] 找不到定策牌实例 {planCard.CardInstanceId}。");
                return false;
            }

            if (!card.OwnerId.Equals(planCard.PlayerId, StringComparison.Ordinal))
            {
                ctx.RoundLog.Add($"[RoundManager] 定策牌 {planCard.CardInstanceId} 不属于玩家 {planCard.PlayerId}。");
                return false;
            }

            var effectiveConfigId = card.GetEffectiveConfigId();
            var resolvedEffects = ResolveCardEffects(ctx, card);
            if (resolvedEffects == null)
            {
                ctx.RoundLog.Add($"[RoundManager] 找不到定策牌 {planCard.CardInstanceId}（{effectiveConfigId}）的卡牌定义。");
                return false;
            }

            var planRules = ctx.PlayRules.Resolve(ctx, planCard.PlayerId, resolvedEffects, PlayOrigin.PlayerHandPlay);
            if (!planRules.Allowed)
            {
                ctx.RoundLog.Add($"[RoundManager] 定策牌 {planCard.CardInstanceId} 被限制，原因：{planRules.BlockReason}");
                return false;
            }

            StampCardSourceMetadata(ctx, resolvedEffects, card, planCard.RuntimeParams);

            if (!ctx.CardManager.CommitPlanCard(ctx, planCard.CardInstanceId))
            {
                ctx.RoundLog.Add($"[RoundManager] 定策牌 {planCard.CardInstanceId} 校验失败，拒绝提交。");
                return false;
            }

            RecordPlayedCardStats(ctx, planCard.PlayerId, card, resolvedEffects);
            var pendingCard = new PendingPlanSnapshot
            {
                PlayerId = planCard.PlayerId,
                SnapshotId = $"pps_r{CurrentRound}_n{ctx.PendingPlanSnapshots.Count + 1:D2}",
                SourceCardInstanceId = planCard.CardInstanceId,
                CommittedBaseConfigId = card.ConfigId,
                CommittedEffectiveConfigId = effectiveConfigId,
                CommittedCost = planCard.CommittedCost,
                RuntimeParams = new Dictionary<string, string>(planCard.RuntimeParams),
                FrozenInputs = BuildFrozenInputs(card, resolvedEffects),
                Effects = resolvedEffects,
                SubmitOrder = ctx.PendingPlanSnapshots.Count,
            };
            ctx.PendingPlanSnapshots.Add(pendingCard);

            ctx.RoundLog.Add(
                $"[RoundManager] 玩家 {planCard.PlayerId} 提交定策快照 {pendingCard.SnapshotId}，来源实例 {planCard.CardInstanceId}（顺序 {pendingCard.SubmitOrder}）。");
            return true;
        }

        public void EndRound(BattleContext ctx)
        {
            if (IsBattleOver) return;

            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合结算开始。");
            ctx.CurrentPhase = BattleContext.BattlePhase.Settlement;

            ctx.CardManager.ScanStatCards(ctx);
            _settlement.DrainPendingQueue(ctx);
            if (CheckDeathAndBattleOver(ctx)) return;

            if (ctx.PendingPlanSnapshots.Count > 0)
            {
                _settlement.ResolvePlanCards(ctx, ctx.PendingPlanSnapshots);
            }
            else
            {
                ctx.RoundLog.Add("[RoundManager] 本回合无定策牌，跳过定策结算。");
            }
            ctx.PendingPlanSnapshots.Clear();

            if (CheckDeathAndBattleOver(ctx)) return;

            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnRoundEnd, new TriggerContext
            {
                Round = CurrentRound,
            });
            _settlement.DrainPendingQueue(ctx);

            ctx.BuffManager.OnRoundEnd(ctx, CurrentRound);
            if (CheckDeathAndBattleOver(ctx)) return;

            foreach (var kv in ctx.AllPlayers)
            {
                var shield = kv.Value.HeroEntity.Shield;
                if (shield <= 0) continue;

                kv.Value.HeroEntity.Shield = 0;
                ctx.RoundLog.Add($"[RoundManager] {kv.Key} 回合结束护盾清零（{shield} -> 0）。");
            }

            ctx.TriggerManager.TickDecay(ctx);
            ctx.CardManager.OnRoundEnd(ctx, CurrentRound);
            ctx.CardManager.DestroyTempCards(ctx);

            ctx.CurrentPhase = BattleContext.BattlePhase.RoundEnd;
            ctx.EventBus.Publish(new RoundEndEvent { Round = CurrentRound });
            ctx.RoundLog.Add($"[RoundManager] 第 {CurrentRound} 回合结束。");
        }

        private bool CheckDeathAndBattleOver(BattleContext ctx)
        {
            var deadPlayers = new List<string>();

            foreach (var kv in ctx.AllPlayers)
            {
                if (!kv.Value.HeroEntity.IsAlive)
                    deadPlayers.Add(kv.Key);
            }

            foreach (var deadId in deadPlayers)
            {
                var deadPlayer = ctx.AllPlayers[deadId];
                if (deadPlayer.HeroEntity.DeathEventFired)
                    continue;

                deadPlayer.HeroEntity.DeathEventFired = true;
                ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 死亡。");

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnNearDeath, new TriggerContext
                {
                    SourceEntityId = deadPlayer.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object> { ["playerId"] = deadId },
                });
                _settlement.DrainPendingQueue(ctx);

                if (deadPlayer.HeroEntity.IsAlive)
                {
                    deadPlayer.HeroEntity.DeathEventFired = false;
                    ctx.RoundLog.Add($"[RoundManager] 玩家 {deadId} 被救活。");
                    continue;
                }

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnDeath, new TriggerContext
                {
                    SourceEntityId = deadPlayer.HeroEntity.EntityId,
                    Extra = new Dictionary<string, object> { ["playerId"] = deadId },
                });
                _settlement.DrainPendingQueue(ctx);

                ctx.EventBus.Publish(new EntityDeathEvent
                {
                    EntityId = deadPlayer.HeroEntity.EntityId,
                    KillerEntityId = string.Empty,
                });
                ctx.EventBus.Publish(new PlayerDeathEvent { PlayerId = deadId });
            }

            var finalAlivePlayers = new List<string>();
            foreach (var kv in ctx.AllPlayers)
            {
                if (kv.Value.HeroEntity.IsAlive)
                    finalAlivePlayers.Add(kv.Key);
            }

            if (finalAlivePlayers.Count == 0)
            {
                IsBattleOver = true;
                WinnerId = null;
                ctx.CurrentPhase = BattleContext.BattlePhase.BattleEnd;
                ctx.RoundLog.Add("[RoundManager] 战斗结束：平局。");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = null, IsDraw = true });
                return true;
            }

            if (finalAlivePlayers.Count == 1)
            {
                IsBattleOver = true;
                WinnerId = finalAlivePlayers[0];
                ctx.CurrentPhase = BattleContext.BattlePhase.BattleEnd;
                ctx.RoundLog.Add($"[RoundManager] 战斗结束：玩家 {WinnerId} 获胜。");
                ctx.EventBus.Publish(new BattleEndEvent { WinnerId = WinnerId, IsDraw = false });
                return true;
            }

            return false;
        }

        private static List<EffectUnit>? ResolveCardEffects(
            BattleContext ctx,
            BattleCard card)
        {
            return ctx.BuildCardEffects(card);
        }

        public bool CanPlayCard(
            BattleContext ctx,
            string playerId,
            string cardConfigId,
            out string reason)
        {
            reason = string.Empty;
            var effects = ctx.BuildCardEffects(cardConfigId);
            if (effects == null)
            {
                reason = $"找不到卡牌定义 {cardConfigId}";
                return false;
            }

            var playRules = ctx.PlayRules.Resolve(ctx, playerId, effects, PlayOrigin.PlayerHandPlay);
            reason = playRules.BlockReason;
            return playRules.Allowed;
        }

        public bool CanPlayCard(
            BattleContext ctx,
            string playerId,
            BattleCard card,
            out string reason)
        {
            var playRules = ResolvePlayRules(ctx, playerId, card, PlayOrigin.PlayerHandPlay);
            reason = playRules.BlockReason;
            return playRules.Allowed;
        }

        public PlayRuleResolution ResolvePlayRules(
            BattleContext ctx,
            string playerId,
            BattleCard card,
            PlayOrigin origin = PlayOrigin.PlayerHandPlay)
        {
            var effects = ResolveCardEffects(ctx, card);
            if (effects == null)
            {
                return new PlayRuleResolution
                {
                    Allowed = false,
                    BlockReason = $"找不到卡牌定义 {card.GetEffectiveConfigId()}"
                };
            }

            return ctx.PlayRules.Resolve(ctx, playerId, effects, origin);
        }

        public PlayCostResolution ResolvePlayCost(
            BattleContext ctx,
            string playerId,
            BattleCard card)
        {
            var playRules = ResolvePlayRules(ctx, playerId, card, PlayOrigin.PlayerHandPlay);
            return ResolvePlayCost(ctx, playerId, card, playRules);
        }

        public PlayCostResolution ResolvePlayCost(
            BattleContext ctx,
            string playerId,
            BattleCard card,
            PlayRuleResolution playRules)
        {
            return _playCostResolver.Resolve(ctx, playerId, card, playRules);
        }

        public void CommitSuccessfulPlayRules(
            BattleContext ctx,
            string playerId,
            PlayRuleResolution playRules)
        {
            ctx.PlayRules.CommitSuccessfulPlay(ctx, playerId, playRules);
        }

        private static void RecordPlayedCardStats(BattleContext ctx, string playerId, BattleCard card, List<EffectUnit> effects)
        {
            var player = ctx.GetPlayer(playerId);
            if (player == null)
                return;

            player.PlayedCardCountThisRound++;

            if (effects.Exists(IsDamageCard))
                player.PlayedDamageCardCountThisRound++;

            if (effects.Exists(IsDefenseCard))
                player.PlayedDefenseCardCountThisRound++;

            if (effects.Exists(IsCounterCard))
                player.PlayedCounterCardCountThisRound++;

            player.PlayedCountByInstanceId.TryGetValue(card.InstanceId, out int instanceCount);
            player.PlayedCountByInstanceId[card.InstanceId] = instanceCount + 1;

            player.PlayedCountByConfigId.TryGetValue(card.ConfigId, out int configCount);
            player.PlayedCountByConfigId[card.ConfigId] = configCount + 1;
        }

        private static Dictionary<string, string> BuildFrozenInputs(BattleCard card, List<EffectUnit> effects)
        {
            var frozen = new Dictionary<string, string>
            {
                ["sourceCard.instanceId"] = card.InstanceId,
                ["sourceCard.baseConfigId"] = card.ConfigId,
                ["sourceCard.effectiveConfigId"] = card.GetEffectiveConfigId(),
            };

            if (effects.Count > 0)
            {
                var first = effects[0];
                if (first.Params.TryGetValue("sourceCardInstancePlayedCount", out var instancePlayedCount))
                    frozen["sourceCard.instancePlayedCount"] = instancePlayedCount;
                if (first.Params.TryGetValue("sourceCardConfigPlayedCount", out var configPlayedCount))
                    frozen["sourceCard.configPlayedCount"] = configPlayedCount;
            }

            return frozen;
        }

        private static bool IsDamageCard(EffectUnit effect)
        {
            return effect.Type == EffectType.Damage
                || effect.Type == EffectType.Pierce
                || effect.Type == EffectType.Lifesteal
                || effect.Type == EffectType.Thorns
                || effect.Type == EffectType.ArmorOnHit
                || effect.Type == EffectType.DOT;
        }

        private static bool IsDefenseCard(EffectUnit effect)
        {
            return effect.Type == EffectType.Shield
                || effect.Type == EffectType.Armor
                || effect.Type == EffectType.AttackBuff
                || effect.Type == EffectType.AttackDebuff
                || effect.Type == EffectType.Reflect
                || effect.Type == EffectType.DamageReduction
                || effect.Type == EffectType.Invincible;
        }

        private static bool IsCounterCard(EffectUnit effect)
        {
            return effect.Type == EffectType.Counter;
        }

        private static void StampCardSourceMetadata(
            BattleContext ctx,
            List<EffectUnit> effects,
            BattleCard card,
            Dictionary<string, string>? runtimeParams = null)
        {
            var player = ctx.GetPlayer(card.OwnerId);
            int instancePlayedCount = 0;
            int configPlayedCount = 0;
            player?.PlayedCountByInstanceId.TryGetValue(card.InstanceId, out instancePlayedCount);
            player?.PlayedCountByConfigId.TryGetValue(card.ConfigId, out configPlayedCount);
            string effectiveConfigId = card.GetEffectiveConfigId();

            foreach (var effect in effects)
            {
                if (runtimeParams != null)
                {
                    foreach (var kv in runtimeParams)
                        effect.Params[kv.Key] = kv.Value;
                }

                effect.Params["sourceCardInstanceId"] = card.InstanceId;
                effect.Params["sourceCardConfigId"] = effectiveConfigId;
                effect.Params["sourceCardBaseConfigId"] = card.ConfigId;
                effect.Params["sourceCardInstancePlayedCount"] = instancePlayedCount.ToString();
                effect.Params["sourceCardConfigPlayedCount"] = configPlayedCount.ToString();
            }
        }
    }
}











