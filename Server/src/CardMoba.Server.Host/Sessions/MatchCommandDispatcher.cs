using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Rules.Play;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Core;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Protocol.Messages.Requests;
using Microsoft.Extensions.Logging;

namespace CardMoba.Server.Host.Sessions
{
    /// <summary>
    /// 负责处理对局中的权威命令，并在必要时触发阶段推进与广播。
    /// </summary>
    public sealed class MatchCommandDispatcher
    {
        private const long BattleTurnLockCooldownMilliseconds = 1000;

        private readonly MatchContext _context;
        private readonly MatchManager _matchManager;
        private readonly MatchBroadcaster _broadcaster;
        private readonly HashSet<string> _battleLockedPlayerIds;
        private readonly IDictionary<string, long> _battleLockCooldownUntilUnixMsByPlayer;
        private readonly HashSet<string> _disconnectedPlayerIds;
        private readonly ILogger<MatchCommandDispatcher> _logger;

        public MatchCommandDispatcher(
            MatchContext context,
            MatchManager matchManager,
            MatchBroadcaster broadcaster,
            HashSet<string> battleLockedPlayerIds,
            IDictionary<string, long> battleLockCooldownUntilUnixMsByPlayer,
            HashSet<string> disconnectedPlayerIds,
            ILogger<MatchCommandDispatcher> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _matchManager = matchManager ?? throw new ArgumentNullException(nameof(matchManager));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _battleLockedPlayerIds = battleLockedPlayerIds ?? throw new ArgumentNullException(nameof(battleLockedPlayerIds));
            _battleLockCooldownUntilUnixMsByPlayer = battleLockCooldownUntilUnixMsByPlayer ?? throw new ArgumentNullException(nameof(battleLockCooldownUntilUnixMsByPlayer));
            _disconnectedPlayerIds = disconnectedPlayerIds ?? throw new ArgumentNullException(nameof(disconnectedPlayerIds));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void BeginActiveBattleRoundIfNeeded()
        {
            if (_context.ActiveRoundManager == null || _context.ActiveBattleContext == null)
                return;

            if (_context.ActiveRoundManager.CurrentRound == 0 && !_context.ActiveRoundManager.IsBattleOver)
                _context.ActiveRoundManager.BeginRound(_context.ActiveBattleContext);

            _battleLockedPlayerIds.Clear();
            _battleLockCooldownUntilUnixMsByPlayer.Clear();
            foreach (var player in _context.ActiveBattleContext.AllPlayers.Values.Where(player => player.CanAct))
            {
                if (_disconnectedPlayerIds.Contains(player.PlayerId))
                    _battleLockedPlayerIds.Add(player.PlayerId);
            }

            _logger.LogInformation(
                "对局 {MatchId} 进入新的操作回合。Round={Round} 初始锁定玩家数={LockedCount}",
                _context.MatchId,
                _context.ActiveRoundManager.CurrentRound,
                _battleLockedPlayerIds.Count);
        }

        public Task HandlePlayInstantCardAsync(string playerId, PlayInstantCardRequest request)
        {
            return HandleCardPlayAsync(playerId, request.CardInstanceId, request.RuntimeParams, instant: true, actionName: "PlayInstantCard");
        }

        public Task HandleCommitPlanCardAsync(string playerId, CommitPlanCardRequest request)
        {
            return HandleCardPlayAsync(playerId, request.CardInstanceId, request.RuntimeParams, instant: false, actionName: "CommitPlanCard");
        }

        public Task HandleEndTurnAsync(string playerId)
        {
            return HandleSetBattleTurnLockAsync(playerId, isLocked: true);
        }

        public async Task HandleSetBattleTurnLockAsync(string playerId, bool isLocked)
        {
            if (!EnsureBattleOperationPhase(
                playerId,
                isLocked ? "SetBattleTurnLock.Lock" : "SetBattleTurnLock.Unlock",
                out var battleContext,
                out var roundManager,
                requireUnlocked: false))
            {
                return;
            }

            bool currentlyLocked = _battleLockedPlayerIds.Contains(playerId);
            if (currentlyLocked == isLocked)
            {
                await _broadcaster.RejectAsync(
                    playerId,
                    "SetBattleTurnLock",
                    ProtocolActionErrorCode.BattleTurnLockStateInvalid,
                    isLocked ? "当前已经处于锁定状态。" : "当前已经处于未锁定状态。");
                return;
            }

            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_battleLockCooldownUntilUnixMsByPlayer.TryGetValue(playerId, out var cooldownUntilUnixMs)
                && cooldownUntilUnixMs > nowUnixMs)
            {
                await _broadcaster.RejectAsync(
                    playerId,
                    "SetBattleTurnLock",
                    ProtocolActionErrorCode.BattleTurnLockCooldown,
                    $"锁定状态切换冷却中，请在 {cooldownUntilUnixMs - nowUnixMs} ms 后重试。");
                return;
            }

            if (isLocked)
                _battleLockedPlayerIds.Add(playerId);
            else
                _battleLockedPlayerIds.Remove(playerId);

            _battleLockCooldownUntilUnixMsByPlayer[playerId] = nowUnixMs + BattleTurnLockCooldownMilliseconds;

            _logger.LogInformation(
                "对局 {MatchId} 玩家 {PlayerId} {Action} 回合。已锁定玩家数={LockedCount}",
                _context.MatchId,
                playerId,
                isLocked ? "锁定" : "取消锁定",
                _battleLockedPlayerIds.Count);

            if (isLocked)
            {
                await TryAdvanceBattleRoundAsync(battleContext, roundManager);
                return;
            }

            await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
        }

        public async Task HandlePlayerDisconnectedAsync(string playerId)
        {
            if (_context.CurrentPhase == MatchPhase.BattleInProgress
                && _context.ActiveBattleContext != null
                && _context.ActiveRoundManager != null)
            {
                var player = _context.ActiveBattleContext.GetPlayer(playerId);
                if (player != null && player.CanAct)
                {
                    _battleLockedPlayerIds.Add(playerId);
                    _logger.LogInformation("对局 {MatchId} 玩家 {PlayerId} 在战斗阶段掉线，已自动锁定本回合。", _context.MatchId, playerId);
                }

                await TryAdvanceBattleRoundAsync(_context.ActiveBattleContext, _context.ActiveRoundManager);
                return;
            }

            if (_context.CurrentPhase == MatchPhase.BuildWindow
                && _context.ActiveBuildWindow != null
                && _context.ActiveBuildWindow.PlayerWindows.TryGetValue(playerId, out var playerWindow)
                && !playerWindow.IsLocked)
            {
                _matchManager.LockBuildChoice(_context, playerId);
                _logger.LogInformation("对局 {MatchId} 玩家 {PlayerId} 在构筑阶段掉线，已按默认规则锁定构筑。", _context.MatchId, playerId);

                if (_matchManager.AdvanceIfReady(_context))
                {
                    await _broadcaster.BroadcastBuildWindowClosedAsync();
                    if (_context.IsMatchOver)
                    {
                        await _broadcaster.BroadcastPhaseAsync(ServerPhaseKind.MatchEnded);
                        await _broadcaster.BroadcastMatchEndedAsync();
                        return;
                    }

                    _battleLockedPlayerIds.Clear();
                    BeginActiveBattleRoundIfNeeded();
                    await _broadcaster.BroadcastMatchStartedAsync();
                    await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
                    return;
                }

                await _broadcaster.BroadcastBuildWindowUpdatedAsync();
            }
        }

        public async Task HandleSubmitBuildChoiceAsync(string playerId, SubmitBuildChoiceRequest request)
        {
            try
            {
                if (!EnsureBuildWindow(playerId, "SubmitBuildChoice"))
                    return;

                _matchManager.ApplyBuildAction(_context, playerId, ToRuntimeBuildChoice(request.Choice));
                _logger.LogInformation("对局 {MatchId} 玩家 {PlayerId} 提交了一次构筑选择。", _context.MatchId, playerId);
                await _broadcaster.BroadcastBuildWindowUpdatedAsync();
            }
            catch (Exception ex)
            {
                await _broadcaster.RejectAsync(playerId, "SubmitBuildChoice", ProtocolActionErrorCode.BuildChoiceInvalid, ex.Message);
            }
        }

        public async Task HandleLockBuildWindowAsync(string playerId)
        {
            try
            {
                if (!EnsureBuildWindow(playerId, "LockBuildWindow"))
                    return;

                _matchManager.LockBuildChoice(_context, playerId);
                _logger.LogInformation("对局 {MatchId} 玩家 {PlayerId} 锁定了构筑阶段。", _context.MatchId, playerId);
                if (_matchManager.AdvanceIfReady(_context))
                {
                    await _broadcaster.BroadcastBuildWindowClosedAsync();
                    if (_context.IsMatchOver)
                    {
                        await _broadcaster.BroadcastPhaseAsync(ServerPhaseKind.MatchEnded);
                        await _broadcaster.BroadcastMatchEndedAsync();
                        return;
                    }

                    _battleLockedPlayerIds.Clear();
                    BeginActiveBattleRoundIfNeeded();
                    await _broadcaster.BroadcastMatchStartedAsync();
                    await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
                    return;
                }

                await _broadcaster.BroadcastBuildWindowUpdatedAsync();
            }
            catch (Exception ex)
            {
                await _broadcaster.RejectAsync(playerId, "LockBuildWindow", ProtocolActionErrorCode.BuildWindowLockBlocked, ex.Message);
            }
        }

        private async Task HandleCardPlayAsync(
            string playerId,
            string cardInstanceId,
            IReadOnlyDictionary<string, string> runtimeParams,
            bool instant,
            string actionName)
        {
            try
            {
                if (!EnsureBattleOperationPhase(playerId, actionName, out var battleContext, out var roundManager, requireUnlocked: true))
                    return;

                var player = battleContext.GetPlayer(playerId);
                if (player == null)
                {
                    await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.PlayerNotFound, $"战斗中不存在玩家：{playerId}");
                    return;
                }

                var battleCard = battleContext.CardManager.GetCard(battleContext, cardInstanceId);
                if (battleCard == null)
                {
                    await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.CardNotFound, $"未找到卡牌实例：{cardInstanceId}");
                    return;
                }

                if (!string.Equals(battleCard.OwnerId, playerId, StringComparison.Ordinal))
                {
                    await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.CardOwnershipMismatch, "不能操作不属于自己的卡牌。");
                    return;
                }

                var playRules = roundManager.ResolvePlayRules(battleContext, playerId, battleCard, PlayOrigin.PlayerHandPlay);
                if (!playRules.Allowed)
                {
                    await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.PlayRuleBlocked, playRules.BlockReason);
                    return;
                }

                var playCost = roundManager.ResolvePlayCost(battleContext, playerId, battleCard, playRules);
                if (player.Energy < playCost.FinalCost)
                {
                    await _broadcaster.RejectAsync(
                        playerId,
                        actionName,
                        ProtocolActionErrorCode.NotEnoughEnergy,
                        $"能量不足（当前 {player.Energy}，需要 {playCost.FinalCost}）。");
                    return;
                }

                bool hadForceConsume = battleCard.ExtraData.TryGetValue("forceConsumeAfterResolve", out var previousForceConsume);
                if (playRules.ForceConsumeAfterResolve)
                    battleCard.ExtraData["forceConsumeAfterResolve"] = true;

                player.Energy -= playCost.FinalCost;
                bool success;
                if (instant)
                {
                    var results = roundManager.PlayInstantCard(battleContext, playerId, cardInstanceId, new Dictionary<string, string>(runtimeParams));
                    success = results.Count > 0 || battleCard.Zone != CardZone.Hand;
                }
                else
                {
                    success = roundManager.CommitPlanCard(battleContext, new CommittedPlanCard
                    {
                        PlayerId = playerId,
                        CardInstanceId = cardInstanceId,
                        CommittedCost = playCost.FinalCost,
                        RuntimeParams = new Dictionary<string, string>(runtimeParams),
                    });
                }

                if (!success)
                {
                    player.Energy += playCost.FinalCost;
                    if (hadForceConsume)
                        battleCard.ExtraData["forceConsumeAfterResolve"] = previousForceConsume!;
                    else
                        battleCard.ExtraData.Remove("forceConsumeAfterResolve");

                    await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.ActionExecutionFailed, "出牌失败。");
                    return;
                }

                _logger.LogInformation(
                    "对局 {MatchId} 玩家 {PlayerId} {ActionName} 成功。CardInstanceId={CardInstanceId} Cost={Cost}",
                    _context.MatchId,
                    playerId,
                    actionName,
                    cardInstanceId,
                    playCost.FinalCost);

                roundManager.CommitSuccessfulPlayRules(battleContext, playerId, playRules);
                if (await HandleBattleCompletedAsync())
                    return;

                await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
            }
            catch (Exception ex)
            {
                await _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.ActionExecutionFailed, ex.Message);
            }
        }

        private async Task TryAdvanceBattleRoundAsync(BattleContext battleContext, RoundManager roundManager)
        {
            var activePlayers = battleContext.AllPlayers.Values
                .Where(player => player.CanAct)
                .Select(player => player.PlayerId)
                .ToList();

            if (activePlayers.Any(player => !_battleLockedPlayerIds.Contains(player)))
            {
                await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
                return;
            }

            _logger.LogInformation("对局 {MatchId} 本回合所有可行动玩家均已锁定，开始提前推进结算。", _context.MatchId);

            await _broadcaster.BroadcastPhaseAsync(ServerPhaseKind.BattleSettlement);
            roundManager.EndRound(battleContext);
            if (await HandleBattleCompletedAsync())
                return;

            _battleLockedPlayerIds.Clear();
            _battleLockCooldownUntilUnixMsByPlayer.Clear();
            roundManager.BeginRound(battleContext);
            BeginActiveBattleRoundIfNeeded();
            await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
        }

        private bool EnsureBattleOperationPhase(
            string playerId,
            string actionName,
            out BattleContext battleContext,
            out RoundManager roundManager,
            bool requireUnlocked)
        {
            battleContext = _context.ActiveBattleContext!;
            roundManager = _context.ActiveRoundManager!;

            if (_context.CurrentPhase != MatchPhase.BattleInProgress || _context.ActiveBattleContext == null || _context.ActiveRoundManager == null)
            {
                _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.InvalidPhase, "当前不在战斗阶段。");
                return false;
            }

            battleContext = _context.ActiveBattleContext;
            roundManager = _context.ActiveRoundManager;
            if (battleContext.CurrentPhase != BattleContext.BattlePhase.PlayerAction)
            {
                _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.InvalidPhase, "当前不在操作期。");
                return false;
            }

            var player = battleContext.GetPlayer(playerId);
            if (player == null)
            {
                _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.PlayerNotFound, "当前战斗中不存在该玩家。");
                return false;
            }

            if (!player.CanAct)
            {
                _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.PlayerCannotAct, "当前玩家不能行动。");
                return false;
            }

            if (requireUnlocked && _battleLockedPlayerIds.Contains(playerId))
            {
                _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.BattleTurnLockStateInvalid, "当前已锁定回合，无法再执行其他操作。");
                return false;
            }

            return true;
        }

        private bool EnsureBuildWindow(string playerId, string actionName)
        {
            if (_context.CurrentPhase == MatchPhase.BuildWindow && _context.ActiveBuildWindow != null)
                return true;

            _ = _broadcaster.RejectAsync(playerId, actionName, ProtocolActionErrorCode.InvalidPhase, "当前不在构筑阶段。");
            return false;
        }

        private async Task<bool> HandleBattleCompletedAsync()
        {
            if (_context.ActiveRoundManager == null || !_context.ActiveRoundManager.IsBattleOver)
                return false;

            var summary = _context.ActiveRoundManager.CompletedBattleSummary
                ?? throw new InvalidOperationException("战斗结束后缺少 BattleSummary。");

            await _broadcaster.BroadcastBattleEndedAsync(summary);
            _matchManager.CompleteCurrentBattle(_context);

            if (_context.IsMatchOver)
            {
                await _broadcaster.BroadcastPhaseAsync(ServerPhaseKind.MatchEnded);
                await _broadcaster.BroadcastMatchEndedAsync();
                return true;
            }

            if (_context.ActiveBuildWindow != null)
            {
                await _broadcaster.BroadcastPhaseAsync(ServerPhaseKind.BuildWindow);
                await _broadcaster.BroadcastBuildWindowOpenedAsync();
                return true;
            }

            _battleLockedPlayerIds.Clear();
            _battleLockCooldownUntilUnixMsByPlayer.Clear();
            BeginActiveBattleRoundIfNeeded();
            await _broadcaster.BroadcastMatchStartedAsync();
            await _broadcaster.BroadcastBattlePhaseAndSnapshotAsync();
            return true;
        }

        private static BuildChoice ToRuntimeBuildChoice(BuildChoiceDto dto)
        {
            var choice = BuildChoice.Create(ToRuntimeBuildActionType(dto.ActionType));
            choice.TargetPersistentCardId = dto.TargetPersistentCardId;
            foreach (var entry in dto.SelectedDraftOfferIdsByGroup)
                choice.SelectedDraftOfferIdsByGroup[entry.Key] = entry.Value;
            return choice;
        }

        private static BuildActionType ToRuntimeBuildActionType(ProtocolBuildActionType actionType)
        {
            return actionType switch
            {
                ProtocolBuildActionType.Heal => BuildActionType.Heal,
                ProtocolBuildActionType.AddCard => BuildActionType.AddCard,
                ProtocolBuildActionType.RemoveCard => BuildActionType.RemoveCard,
                ProtocolBuildActionType.UpgradeCard => BuildActionType.UpgradeCard,
                ProtocolBuildActionType.CustomPlaceholder => BuildActionType.CustomPlaceholder,
                _ => BuildActionType.None,
            };
        }
    }
}
