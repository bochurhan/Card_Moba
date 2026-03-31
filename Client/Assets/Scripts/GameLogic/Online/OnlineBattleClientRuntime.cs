using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardMoba.Client.GameLogic.Abstractions;
using CardMoba.Client.Network.Connection;
using CardMoba.Protocol.Messages.Common;
using CardMoba.Protocol.Messages.Messages;

namespace CardMoba.Client.GameLogic.Online
{
    /// <summary>
    /// 联机战斗运行时。
    /// 负责把服务端协议消息映射为 UI 视图状态，并通过连接层发送玩家操作。
    /// </summary>
    public sealed class OnlineBattleClientRuntime : IBattleClientRuntime, IDisposable
    {
        private const long TurnLockCooldownMilliseconds = 1000;

        private readonly IMatchHubConnection _connection;
        private readonly SynchronizationContext _syncContext;

        private BattleSnapshotViewState _currentBattleView = new BattleSnapshotViewState();
        private BuildWindowViewState _currentBuildWindow;
        private BattlePhaseViewState _currentPhaseView = new BattlePhaseViewState();
        private string _localPlayerId = string.Empty;
        private bool? _pendingTurnLockState;
        private long _pendingTurnLockCooldownUntilUnixMs;

        public OnlineBattleClientRuntime(IMatchHubConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _syncContext = SynchronizationContext.Current;
            BindConnectionEvents();
        }

        public event Action OnStateChanged;
        public event Action<string> OnLogMessage;
        public event Action<int> OnGameOver;
        public event Action<BattlePhaseViewState> OnPhaseChanged;
        public event Action<BuildWindowViewState> OnBuildWindowOpened;
        public event Action<BuildWindowViewState> OnBuildWindowUpdated;
        public event Action OnBuildWindowClosed;

        public bool IsPlayerTurn => _currentPhaseView.PhaseKind == BattleClientPhaseKind.Operation && !IsGameOver;
        public bool IsGameOver => _currentPhaseView.PhaseKind == BattleClientPhaseKind.MatchEnded;
        public bool SupportsTurnLockToggle => true;
        public bool IsTurnLocked => GetEffectiveTurnLockedState();
        public bool CanToggleTurnLock => IsPlayerTurn && !IsGameOver && !IsTurnLockCoolingDown();
        public int CurrentRound => _currentBattleView.CurrentRound;
        public BattleSnapshotViewState CurrentBattleView => _currentBattleView;
        public BuildWindowViewState CurrentBuildWindow => _currentBuildWindow;

        public void StartBattle()
        {
            RaiseLog("<color=#99ddff>[联机] 当前 runtime 由房间和 Ready 流程驱动，不会在 StartBattle 中直接创建对局。</color>");
        }

        public async Task HostLocalMatchAsync(string hubUrl, string displayName, bool autoReady = true)
        {
            await DisconnectAsync();
            await ConnectAsync(hubUrl);
            await CreateLocalMatchAsync(displayName);
            if (autoReady)
                await ReadyAsync();
        }

        public async Task JoinLocalMatchAsync(string hubUrl, string matchId, string displayName, bool autoReady = true)
        {
            await DisconnectAsync();
            await ConnectAsync(hubUrl);
            await JoinLocalMatchAsync(matchId, displayName);
            if (autoReady)
                await ReadyAsync();
        }

        public Task ConnectAsync(string hubUrl)
        {
            return _connection.ConnectAsync(hubUrl);
        }

        public Task DisconnectAsync()
        {
            ClearPendingTurnLock();
            return _connection.DisconnectAsync();
        }

        public Task CreateLocalMatchAsync(string displayName)
        {
            return _connection.CreateLocalMatchAsync(displayName);
        }

        public Task JoinLocalMatchAsync(string matchId, string displayName)
        {
            return _connection.JoinLocalMatchAsync(matchId, displayName);
        }

        public Task ReadyAsync()
        {
            return _connection.ReadyAsync();
        }

        public void PlayerEndTurn()
        {
            SetTurnLock(true);
        }

        public void SetTurnLock(bool isLocked)
        {
            if (!SupportsTurnLockToggle)
                return;

            if (!CanToggleTurnLock)
            {
                RaiseLog("<color=#ff8866>[联机] 当前不能切换回合锁定状态。</color>");
                return;
            }

            if (IsTurnLocked == isLocked)
                return;

            MarkPendingTurnLock(isLocked);
            Dispatch(() => OnStateChanged?.Invoke());
            _ = _connection.SetBattleTurnLockAsync(isLocked);
        }

        public string PlayerPlayInstantCard(string cardInstanceId)
        {
            _ = _connection.PlayInstantCardAsync(cardInstanceId);
            return "已发送";
        }

        public string PlayerPlayInstantCard(string cardInstanceId, Dictionary<string, string> runtimeParams)
        {
            _ = _connection.PlayInstantCardAsync(cardInstanceId, runtimeParams);
            return "已发送";
        }

        public string PlayerCommitPlanCard(string cardInstanceId)
        {
            _ = _connection.CommitPlanCardAsync(cardInstanceId);
            return "已发送";
        }

        public string PlayerCommitPlanCard(string cardInstanceId, Dictionary<string, string> runtimeParams)
        {
            _ = _connection.CommitPlanCardAsync(cardInstanceId, runtimeParams);
            return "已发送";
        }

        public void SubmitBuildChoice(BuildChoiceViewState choice)
        {
            _ = _connection.SubmitBuildChoiceAsync(ToProtocolBuildChoice(choice));
        }

        public void LockBuildWindow()
        {
            _ = _connection.LockBuildWindowAsync();
        }

        public void PrintBattleStatus()
        {
            if (_currentBattleView == null)
            {
                RaiseLog("<color=#ff4444>[联机] 当前没有可打印的战斗快照。</color>");
                return;
            }

            RaiseLog(
                $"<color=#99ddff>[联机快照] 第 {_currentBattleView.BattleIndex}/{_currentBattleView.TotalBattleCount} 场，回合 {_currentBattleView.CurrentRound}，阶段 {_currentPhaseView.DisplayText}</color>");
            RaiseLog(
                $"<color=#99ddff>[联机快照] 我方 HP {_currentBattleView.LocalPlayer.Hp}/{_currentBattleView.LocalPlayer.MaxHp}，对手 HP {_currentBattleView.OpponentPlayer.Hp}/{_currentBattleView.OpponentPlayer.MaxHp}</color>");
        }

        public void Dispose()
        {
            UnbindConnectionEvents();
            _connection.Dispose();
        }

        private void BindConnectionEvents()
        {
            _connection.MatchCreated += OnMatchCreated;
            _connection.MatchJoined += OnMatchJoined;
            _connection.MatchStarted += OnMatchStarted;
            _connection.PhaseChanged += OnServerPhaseChanged;
            _connection.BattleSnapshotReceived += OnBattleSnapshotReceived;
            _connection.BuildWindowOpened += OnBuildWindowOpenedMessage;
            _connection.BuildWindowUpdated += OnBuildWindowUpdatedMessage;
            _connection.BuildWindowClosed += OnBuildWindowClosedMessage;
            _connection.BattleEnded += OnBattleEndedMessage;
            _connection.MatchEnded += OnMatchEndedMessage;
            _connection.ActionRejected += OnActionRejectedMessage;
        }

        private void UnbindConnectionEvents()
        {
            _connection.MatchCreated -= OnMatchCreated;
            _connection.MatchJoined -= OnMatchJoined;
            _connection.MatchStarted -= OnMatchStarted;
            _connection.PhaseChanged -= OnServerPhaseChanged;
            _connection.BattleSnapshotReceived -= OnBattleSnapshotReceived;
            _connection.BuildWindowOpened -= OnBuildWindowOpenedMessage;
            _connection.BuildWindowUpdated -= OnBuildWindowUpdatedMessage;
            _connection.BuildWindowClosed -= OnBuildWindowClosedMessage;
            _connection.BattleEnded -= OnBattleEndedMessage;
            _connection.MatchEnded -= OnMatchEndedMessage;
            _connection.ActionRejected -= OnActionRejectedMessage;
        }

        private void OnMatchCreated(MatchCreatedMessage message)
        {
            _localPlayerId = message.LocalPlayerId;
            RaiseLog($"<color=#99ddff>[联机] 已创建房间：{message.MatchId}</color>");
        }

        private void OnMatchJoined(MatchJoinedMessage message)
        {
            if (string.IsNullOrWhiteSpace(_localPlayerId))
                _localPlayerId = _connection.LocalPlayerId;
            RaiseLog($"<color=#99ddff>[联机] 已加入房间：{message.MatchId}</color>");
        }

        private void OnMatchStarted(MatchStartedMessage message)
        {
            RaiseLog($"<color=#99ddff>[联机] 对局开始，第 {message.BattleIndex}/{message.TotalBattleCount} 场。</color>");
        }

        private void OnServerPhaseChanged(PhaseChangedMessage message)
        {
            _currentPhaseView = OnlineMessageMapper.ToBattlePhaseViewState(message);
            Dispatch(() => OnPhaseChanged?.Invoke(_currentPhaseView));
        }

        private void OnBattleSnapshotReceived(BattleSnapshotMessage message)
        {
            _currentBattleView = OnlineMessageMapper.ToBattleViewState(message.Snapshot);
            if (string.IsNullOrWhiteSpace(_localPlayerId))
                _localPlayerId = _currentBattleView.LocalPlayer.PlayerId;

            ClearPendingTurnLock();
            Dispatch(() => OnStateChanged?.Invoke());
        }

        private void OnBuildWindowOpenedMessage(BuildWindowOpenedMessage message)
        {
            _currentBuildWindow = OnlineMessageMapper.ToBuildWindowViewState(message.BuildWindow);
            Dispatch(() =>
            {
                OnBuildWindowOpened?.Invoke(_currentBuildWindow);
                OnStateChanged?.Invoke();
            });
        }

        private void OnBuildWindowUpdatedMessage(BuildWindowUpdatedMessage message)
        {
            _currentBuildWindow = OnlineMessageMapper.ToBuildWindowViewState(message.BuildWindow);
            Dispatch(() =>
            {
                OnBuildWindowUpdated?.Invoke(_currentBuildWindow);
                OnStateChanged?.Invoke();
            });
        }

        private void OnBuildWindowClosedMessage(BuildWindowClosedMessage message)
        {
            _currentBuildWindow = null;
            Dispatch(() =>
            {
                OnBuildWindowClosed?.Invoke();
                OnStateChanged?.Invoke();
            });
        }

        private void OnBattleEndedMessage(BattleEndedMessage message)
        {
            RaiseLog(
                $"<color=#99ddff>[联机] 第 {message.Result.BattleIndex}/{message.Result.TotalBattleCount} 场结束：{message.Result.BattleEndReason}</color>");
        }

        private void OnMatchEndedMessage(MatchEndedMessage message)
        {
            _currentPhaseView = new BattlePhaseViewState
            {
                PhaseKind = BattleClientPhaseKind.MatchEnded,
                DisplayText = "整局结束",
                TimerText = "整局结束",
                UseOperationTimer = false,
            };

            Dispatch(() => OnPhaseChanged?.Invoke(_currentPhaseView));
            int resultCode = ResolveGameOverCode(message);
            Dispatch(() => OnGameOver?.Invoke(resultCode));
        }

        private void OnActionRejectedMessage(ActionRejectedMessage message)
        {
            if (string.Equals(message.ActionName, "SetBattleTurnLock", StringComparison.Ordinal)
                || string.Equals(message.ActionName, "SetBattleTurnLock.Lock", StringComparison.Ordinal)
                || string.Equals(message.ActionName, "SetBattleTurnLock.Unlock", StringComparison.Ordinal))
            {
                ClearPendingTurnLock();
                Dispatch(() => OnStateChanged?.Invoke());
            }

            RaiseLog($"<color=#ff8866>[联机] {message.ActionName} 被拒绝：{message.ErrorCode} / {message.Reason}</color>");
        }

        private int ResolveGameOverCode(MatchEndedMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.WinnerTeamId))
                return -1;

            string localTeamId = _currentBattleView?.LocalPlayer?.TeamId ?? string.Empty;
            return string.Equals(localTeamId, message.WinnerTeamId, StringComparison.Ordinal) ? 1 : 2;
        }

        private bool GetEffectiveTurnLockedState()
        {
            if (_pendingTurnLockState.HasValue && _pendingTurnLockCooldownUntilUnixMs > GetCurrentUnixTimeMilliseconds())
                return _pendingTurnLockState.Value;

            return _currentBattleView?.LocalPlayer?.IsTurnLocked ?? false;
        }

        private bool IsTurnLockCoolingDown()
        {
            long nowUnixMs = GetCurrentUnixTimeMilliseconds();
            long serverCooldownUntil = _currentBattleView?.LocalPlayer?.LockCooldownUntilUnixMs ?? 0;
            return _pendingTurnLockCooldownUntilUnixMs > nowUnixMs || serverCooldownUntil > nowUnixMs;
        }

        private void MarkPendingTurnLock(bool isLocked)
        {
            _pendingTurnLockState = isLocked;
            _pendingTurnLockCooldownUntilUnixMs = GetCurrentUnixTimeMilliseconds() + TurnLockCooldownMilliseconds;
        }

        private void ClearPendingTurnLock()
        {
            _pendingTurnLockState = null;
            _pendingTurnLockCooldownUntilUnixMs = 0;
        }

        private static long GetCurrentUnixTimeMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static BuildChoiceDto ToProtocolBuildChoice(BuildChoiceViewState choice)
        {
            var dto = new BuildChoiceDto
            {
                ActionType = ToProtocolBuildActionType(choice.ActionType),
                TargetPersistentCardId = choice.TargetPersistentCardId,
            };

            foreach (var pair in choice.SelectedDraftOfferIdsByGroup)
                dto.SelectedDraftOfferIdsByGroup[pair.Key] = pair.Value;

            return dto;
        }

        private static ProtocolBuildActionType ToProtocolBuildActionType(BuildActionViewType actionType)
        {
            return actionType switch
            {
                BuildActionViewType.Heal => ProtocolBuildActionType.Heal,
                BuildActionViewType.AddCard => ProtocolBuildActionType.AddCard,
                BuildActionViewType.RemoveCard => ProtocolBuildActionType.RemoveCard,
                BuildActionViewType.UpgradeCard => ProtocolBuildActionType.UpgradeCard,
                _ => ProtocolBuildActionType.None,
            };
        }

        private void RaiseLog(string message)
        {
            Dispatch(() => OnLogMessage?.Invoke(message));
        }

        private void Dispatch(Action action)
        {
            if (action == null)
                return;

            if (_syncContext == null)
            {
                action();
                return;
            }

            _syncContext.Post(_ => action(), null);
        }
    }
}
