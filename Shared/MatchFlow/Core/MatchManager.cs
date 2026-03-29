using System;
using System.Linq;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Results;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Rules;

namespace CardMoba.MatchFlow.Core
{
    public sealed class MatchManager
    {
        private readonly BattleFactory _battleFactory;
        private readonly BattleSetupBuilder _battleSetupBuilder;
        private readonly MatchStateApplier _matchStateApplier;
        private readonly BuildOfferGenerator _buildOfferGenerator;
        private readonly BuildActionApplier _buildActionApplier;
        private readonly EquipmentRuntimeFactory _equipmentRuntimeFactory;

        public MatchManager(
            BattleFactory battleFactory,
            BattleSetupBuilder? battleSetupBuilder = null,
            MatchStateApplier? matchStateApplier = null,
            IBuildCatalog? buildCatalog = null,
            BuildOfferGenerator? buildOfferGenerator = null,
            BuildActionApplier? buildActionApplier = null,
            EquipmentRuntimeFactory? equipmentRuntimeFactory = null)
        {
            _battleFactory = battleFactory ?? throw new ArgumentNullException(nameof(battleFactory));
            _battleSetupBuilder = battleSetupBuilder ?? new BattleSetupBuilder();
            _matchStateApplier = matchStateApplier ?? new MatchStateApplier();
            var catalog = buildCatalog ?? new InMemoryBuildCatalog();
            _buildOfferGenerator = buildOfferGenerator ?? new BuildOfferGenerator(catalog);
            _buildActionApplier = buildActionApplier ?? new BuildActionApplier(catalog);
            _equipmentRuntimeFactory = equipmentRuntimeFactory ?? new EquipmentRuntimeFactory(catalog);
        }

        public void StartMatch(MatchContext context, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Ruleset.Steps.Count == 0)
                throw new InvalidOperationException("Match must contain at least one step.");

            context.CurrentStepIndex = 0;
            context.CurrentPhase = MatchPhase.PreparingBattle;
            context.IsMatchOver = false;
            context.WinnerTeamId = null;
            context.MatchLog.Add($"[MatchManager] start match {context.MatchId}.");
            StartCurrentBattle(context, eventBus);
        }

        public void StartCurrentBattle(MatchContext context, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.IsMatchOver)
                return;
            if (context.CurrentStepIndex >= context.Ruleset.Steps.Count)
            {
                EndMatchWithoutMoreSteps(context);
                return;
            }

            var plan = _battleSetupBuilder.BuildCurrentStep(context);
            var runtimeEventBus = new BattleEventBus();
            IEventBus battleEventBus = eventBus == null
                ? runtimeEventBus
                : new CompositeEventBus(runtimeEventBus, eventBus);
            var result = _battleFactory.CreateBattle(
                plan.BattleId,
                plan.BattleSeed,
                plan.Players,
                eventBus: battleEventBus,
                ruleset: plan.BattleRuleset,
                objectives: plan.Objectives);

            context.ActiveBattleContext = result.Context;
            context.ActiveRoundManager = result.RoundManager;
            context.ActiveBuildWindow = null;
            context.ActiveEquipmentRuntimes.Clear();
            foreach (var runtime in _equipmentRuntimeFactory.RegisterForBattle(context, result.Context, runtimeEventBus))
                context.ActiveEquipmentRuntimes.Add(runtime);
            context.CurrentPhase = MatchPhase.BattleInProgress;
            context.MatchLog.Add($"[MatchManager] started battle step {context.CurrentStepIndex} ({plan.BattleId}).");
        }

        public void CompleteCurrentBattle(MatchContext context, BattleSummary summary, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));
            if (context.ActiveBattleContext == null || context.ActiveRoundManager == null)
                throw new InvalidOperationException("No active battle to complete.");

            var step = context.Ruleset.GetStepOrThrow(context.CurrentStepIndex);
            _matchStateApplier.ApplyBattleResult(context, summary);
            foreach (var runtime in context.ActiveEquipmentRuntimes)
            {
                if (context.Players.TryGetValue(runtime.PlayerId, out var player))
                    runtime.OnBattleEnded(context, player, context.ActiveBattleContext, summary);
            }

            context.MatchLog.Add($"[MatchManager] completed battle step {context.CurrentStepIndex} ({summary.BattleId}).");
            context.ActiveBattleContext = null;
            context.ActiveRoundManager = null;
            context.ActiveEquipmentRuntimes.Clear();

            if (context.IsMatchOver)
            {
                context.CurrentPhase = MatchPhase.MatchEnded;
                context.MatchLog.Add($"[MatchManager] match ended by battle summary. winner={context.WinnerTeamId ?? "<draw>"}.");
                return;
            }

            bool hasNextStep = context.CurrentStepIndex + 1 < context.Ruleset.Steps.Count;
            if (!hasNextStep)
            {
                context.IsMatchOver = true;
                context.WinnerTeamId = summary.WinningTeamId;
                context.CurrentPhase = MatchPhase.MatchEnded;
                context.MatchLog.Add($"[MatchManager] match ended after final step. winner={context.WinnerTeamId ?? "<draw>"}.");
                return;
            }

            if (step.OpensBuildWindowAfter)
            {
                OpenBuildWindow(context, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return;
            }

            context.CurrentStepIndex++;
            context.CurrentPhase = MatchPhase.PreparingBattle;
            StartCurrentBattle(context, eventBus);
        }

        public void CompleteCurrentBattle(MatchContext context, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ActiveRoundManager?.CompletedBattleSummary == null)
                throw new InvalidOperationException("Active round manager has no completed battle summary.");

            CompleteCurrentBattle(context, context.ActiveRoundManager.CompletedBattleSummary, eventBus);
        }

        public void OpenBuildWindow(MatchContext context, long nowUnixMs)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var buildWindow = _buildOfferGenerator.CreateBuildWindow(context, nowUnixMs);
            foreach (var player in context.Players.Values)
                player.IsBuildLocked = false;

            context.ActiveBuildWindow = buildWindow;
            context.CurrentPhase = MatchPhase.BuildWindow;
            context.MatchLog.Add($"[MatchManager] opened build window after step {context.CurrentStepIndex}.");
        }

        public void ApplyBuildAction(MatchContext context, string playerId, BuildActionType actionType)
        {
            ApplyBuildAction(context, playerId, BuildChoice.Create(actionType));
        }

        public void ApplyBuildAction(MatchContext context, string playerId, BuildChoice choice)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (choice == null)
                throw new ArgumentNullException(nameof(choice));
            if (context.ActiveBuildWindow == null)
                throw new InvalidOperationException("No active build window.");
            if (!context.Players.TryGetValue(playerId, out var player))
                throw new InvalidOperationException($"Match player not found: {playerId}.");

            var playerWindow = GetPlayerWindow(context, playerId);
            if (playerWindow.IsLocked)
                throw new InvalidOperationException($"Player {playerId} is already locked for the active build window.");
            if (playerWindow.NextOpportunityIndex >= playerWindow.OpportunityCount)
                throw new InvalidOperationException($"Player {playerId} has no editable build opportunities remaining.");

            var opportunity = playerWindow.Opportunities[playerWindow.NextOpportunityIndex];
            _buildActionApplier.ApplyChoice(player, playerWindow, opportunity, choice);
            playerWindow.NextOpportunityIndex++;
            if (playerWindow.NextOpportunityIndex < playerWindow.OpportunityCount)
            {
                playerWindow.Opportunities.Add(_buildOfferGenerator.CreateOpportunity(context, player, playerWindow, playerWindow.NextOpportunityIndex));
            }

            context.MatchLog.Add($"[MatchManager] player {playerId} resolved build opportunity {opportunity.OpportunityIndex} with action {choice.ActionType}.");
        }

        public void LockBuildChoice(MatchContext context, string playerId)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ActiveBuildWindow == null)
                throw new InvalidOperationException("No active build window.");
            if (!context.Players.TryGetValue(playerId, out var player))
                throw new InvalidOperationException($"Match player not found: {playerId}.");

            var playerWindow = GetPlayerWindow(context, playerId);
            while (playerWindow.NextOpportunityIndex < playerWindow.OpportunityCount)
            {
                if (playerWindow.Opportunities.Count <= playerWindow.NextOpportunityIndex)
                {
                    playerWindow.Opportunities.Add(_buildOfferGenerator.CreateOpportunity(context, player, playerWindow, playerWindow.NextOpportunityIndex));
                }

                var opportunity = playerWindow.Opportunities[playerWindow.NextOpportunityIndex];
                if (!opportunity.IsResolved)
                {
                    var defaultChoice = CreateDefaultChoice(opportunity, context.Ruleset.DefaultTimeoutAction);
                    _buildActionApplier.ApplyChoice(player, playerWindow, opportunity, defaultChoice);
                    context.MatchLog.Add($"[MatchManager] player {playerId} auto-resolved build opportunity {opportunity.OpportunityIndex} with default action {defaultChoice.ActionType}.");
                }

                playerWindow.NextOpportunityIndex++;
            }

            playerWindow.IsLocked = true;
            player.IsBuildLocked = true;
            context.MatchLog.Add($"[MatchManager] player {playerId} locked build choice.");
        }

        public bool AdvanceIfReady(MatchContext context, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ActiveBuildWindow == null || context.CurrentPhase != MatchPhase.BuildWindow)
                return false;

            var pendingPlayerIds = context.ActiveBuildWindow.PlayerWindows.Values
                .Where(playerWindow => !playerWindow.IsLocked)
                .Select(playerWindow => playerWindow.PlayerId)
                .ToList();
            if (pendingPlayerIds.Count > 0)
                return false;

            foreach (var playerWindow in context.ActiveBuildWindow.PlayerWindows.Values)
            {
                if (context.Players.TryGetValue(playerWindow.PlayerId, out var player))
                    _buildActionApplier.CommitResolvedState(player, playerWindow);
            }

            context.ActiveBuildWindow = null;
            context.CurrentStepIndex++;
            context.CurrentPhase = MatchPhase.PreparingBattle;
            context.MatchLog.Add($"[MatchManager] advanced from build window to step {context.CurrentStepIndex}.");
            StartCurrentBattle(context, eventBus);
            return true;
        }

        public bool HandleBuildWindowTimeout(MatchContext context, long nowUnixMs, IEventBus? eventBus = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ActiveBuildWindow == null || nowUnixMs < context.ActiveBuildWindow.DeadlineUnixMs)
                return false;

            foreach (var playerWindow in context.ActiveBuildWindow.PlayerWindows.Values)
            {
                if (!playerWindow.IsLocked)
                    LockBuildChoice(context, playerWindow.PlayerId);
            }

            return AdvanceIfReady(context, eventBus);
        }

        private static void EndMatchWithoutMoreSteps(MatchContext context)
        {
            context.IsMatchOver = true;
            context.CurrentPhase = MatchPhase.MatchEnded;
            context.MatchLog.Add("[MatchManager] match ended because no more steps were available.");
        }

        private static PlayerBuildWindowState GetPlayerWindow(MatchContext context, string playerId)
        {
            if (context.ActiveBuildWindow == null || !context.ActiveBuildWindow.PlayerWindows.TryGetValue(playerId, out var playerWindow))
                throw new InvalidOperationException($"Active build window not found for player: {playerId}.");

            return playerWindow;
        }

        private static BuildChoice CreateDefaultChoice(BuildOpportunityState opportunity, BuildActionType preferredAction)
        {
            var actionType = opportunity.AvailableActions.Contains(preferredAction)
                ? preferredAction
                : opportunity.AvailableActions.First();
            return BuildChoice.Create(actionType);
        }
    }
}
