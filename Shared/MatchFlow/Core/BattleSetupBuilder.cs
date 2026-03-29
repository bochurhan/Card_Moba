using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Core;
using CardMoba.MatchFlow.Context;

namespace CardMoba.MatchFlow.Core
{
    public sealed class BattleSetupBuilder
    {
        public BattleSetupPlan BuildCurrentStep(MatchContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var step = context.Ruleset.GetStepOrThrow(context.CurrentStepIndex);
            var participantIds = step.ParticipantPlayerIds.Count > 0
                ? step.ParticipantPlayerIds
                : new List<string>(context.Players.Keys);

            var plan = new BattleSetupPlan
            {
                BattleId = $"{context.MatchId}_step_{context.CurrentStepIndex:D2}",
                BattleSeed = context.BaseRandomSeed + context.CurrentStepIndex,
                BattleRuleset = step.BattleRuleset,
            };

            foreach (var playerId in participantIds)
            {
                if (!context.Players.TryGetValue(playerId, out var matchPlayer))
                    throw new InvalidOperationException($"Match player not found: {playerId}.");

                plan.Players.Add(new PlayerSetupData
                {
                    PlayerId = matchPlayer.PlayerId,
                    TeamId = matchPlayer.TeamId,
                    InitialHp = Math.Clamp(matchPlayer.PersistentHp, 0, matchPlayer.MaxHp),
                    UseInitialHp = true,
                    MaxHp = matchPlayer.MaxHp,
                    DeckConfig = matchPlayer.Deck.ToDeckConfig(),
                });
            }

            foreach (var objective in step.Objectives)
            {
                var clone = new ObjectiveSetupData
                {
                    EntityId = objective.EntityId,
                    TeamId = objective.TeamId,
                    InitialHp = objective.InitialHp,
                    MaxHp = objective.MaxHp,
                    InitialShield = objective.InitialShield,
                    InitialArmor = objective.InitialArmor,
                    IsTargetable = objective.IsTargetable,
                    EndsMatchWhenDestroyed = objective.EndsMatchWhenDestroyed,
                };
                foreach (var entityId in objective.RequiredDeadEntityIdsToTarget)
                    clone.RequiredDeadEntityIdsToTarget.Add(entityId);
                plan.Objectives.Add(clone);
            }

            return plan;
        }
    }
}