using System;
using System.Collections.Generic;

namespace CardMoba.Client.GameLogic.Abstractions
{
    /// <summary>
    /// 客户端战斗运行时抽象。
    /// UI 只依赖这一层，不直接依赖 BattleCore / MatchFlow 的运行时对象。
    /// </summary>
    public interface IBattleClientRuntime
    {
        event Action OnStateChanged;
        event Action<string> OnLogMessage;
        event Action<int> OnGameOver;
        event Action<BattlePhaseViewState> OnPhaseChanged;
        event Action<BuildWindowViewState> OnBuildWindowOpened;
        event Action<BuildWindowViewState> OnBuildWindowUpdated;
        event Action OnBuildWindowClosed;

        bool IsPlayerTurn { get; }
        bool IsGameOver { get; }
        bool SupportsTurnLockToggle { get; }
        bool IsTurnLocked { get; }
        bool CanToggleTurnLock { get; }
        int CurrentRound { get; }
        BattleSnapshotViewState CurrentBattleView { get; }
        BuildWindowViewState CurrentBuildWindow { get; }

        void StartBattle();
        void PlayerEndTurn();
        void SetTurnLock(bool isLocked);
        string PlayerPlayInstantCard(string cardInstanceId);
        string PlayerPlayInstantCard(string cardInstanceId, Dictionary<string, string> runtimeParams);
        string PlayerCommitPlanCard(string cardInstanceId);
        string PlayerCommitPlanCard(string cardInstanceId, Dictionary<string, string> runtimeParams);
        void SubmitBuildChoice(BuildChoiceViewState choice);
        void LockBuildWindow();
        void PrintBattleStatus();
    }
}
