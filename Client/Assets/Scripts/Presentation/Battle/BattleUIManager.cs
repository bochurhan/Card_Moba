using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.GameLogic.RoundFlow;   // RoundPhase / RoundPhaseTimerClient
using CardMoba.Client.Presentation.UI.Components;

namespace CardMoba.Client.Presentation.Battle
{
    /// <summary>
    /// 战斗UI主控制器（V2）—— 管理整个战斗界面的显示和交互。
    ///
    /// 挂载方式：挂在战斗场景的根 Canvas 上。在 Inspector 中拖拽绑定各 UI 引用。
    ///
    /// 数据来源（V2 API，完全基于 PlayerData / Entity / BattleCard）：
    ///   BattleGameManager.GetHumanPlayer()    → PlayerData
    ///   BattleGameManager.GetHumanHandCards() → List&lt;(BattleCard, CardConfig)&gt;
    ///   PlayerData.HeroEntity                 → Entity (HP/Shield/Armor)
    ///   PlayerData.GetCardsInZone(CardZone)   → List&lt;BattleCard&gt;
    ///
    /// ⚠️ 本类不再引用任何 V1 类型（PlayerBattleState / CardInstance / player.Hand / player.Energy）。
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // Inspector 绑定
        // ══════════════════════════════════════════════════════════════════════

        [Header("── 我方信息面板 ──")]
        [SerializeField] private TextMeshProUGUI _myNameText;
        [SerializeField] private TextMeshProUGUI _myHpText;
        [SerializeField] private Slider          _myHpBar;
        [SerializeField] private TextMeshProUGUI _myShieldText;
        [SerializeField] private TextMeshProUGUI _myDeckInfoText;

        [Header("── 对手信息面板 ──")]
        [SerializeField] private TextMeshProUGUI _enemyNameText;
        [SerializeField] private TextMeshProUGUI _enemyHpText;
        [SerializeField] private Slider          _enemyHpBar;
        [SerializeField] private TextMeshProUGUI _enemyShieldText;
        [SerializeField] private TextMeshProUGUI _enemyDeckInfoText;

        [Header("── 手牌区域 ──")]
        [SerializeField] private Transform       _handContainer;
        [SerializeField] private GameObject      _cardPrefab;

        [Header("── 操作按钮 ──")]
        [SerializeField] private Button          _endTurnButton;
        [SerializeField] private TextMeshProUGUI _endTurnButtonText;

        [Header("── 调试按钮 ──")]
        [Tooltip("点击后打印战场状态快照到日志面板")]
        [SerializeField] private Button          _debugStatusButton;

        [Header("── 战斗信息 ──")]
        [SerializeField] private TextMeshProUGUI _phaseText;
        [SerializeField] private TextMeshProUGUI _roundText;
        [SerializeField] private ScrollRect      _logScrollRect;
        [SerializeField] private TextMeshProUGUI _logText;

        [Header("── 计时器 UI ──")]
        [SerializeField] private RoundTimerUI    _roundTimerUI;

        [Header("── 游戏结束面板 ──")]
        [SerializeField] private GameObject      _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _gameOverText;
        [SerializeField] private Button          _restartButton;

        // ══════════════════════════════════════════════════════════════════════
        // 内部状态
        // ══════════════════════════════════════════════════════════════════════

        private GameLogic.BattleGameManager _gameManager;
        private RoundPhaseTimerClient       _phaseTimer;

        private bool _isTimerLocked;

        private readonly List<GameObject> _cardObjects = new List<GameObject>();
        private readonly List<string>     _logMessages = new List<string>();

        private const int MaxLogLines = 50;

        // ══════════════════════════════════════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureEventSystem();
            EnsureGraphicRaycaster();

            _gameManager = new GameLogic.BattleGameManager();

            _phaseTimer = GetComponent<RoundPhaseTimerClient>()
                       ?? gameObject.AddComponent<RoundPhaseTimerClient>();

            if (_roundTimerUI != null)
                _roundTimerUI.BindTimer(_phaseTimer);

            _phaseTimer.OnLocalTimerExpired += OnTimerLocked;

            _gameManager.OnStateChanged += RefreshAllUI;
            _gameManager.OnLogMessage   += AddLogMessage;
            _gameManager.OnGameOver     += ShowGameOver;
            _gameManager.OnPhaseChanged += OnPhaseChanged;

            if (_endTurnButton     != null) _endTurnButton.onClick.AddListener(OnEndTurnClicked);
            if (_restartButton     != null) _restartButton.onClick.AddListener(OnRestartClicked);
            if (_debugStatusButton != null) _debugStatusButton.onClick.AddListener(OnDebugStatusClicked);

            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        }

        private void Start() => StartNewBattle();

        private void OnDestroy()
        {
            if (_phaseTimer   != null) _phaseTimer.OnLocalTimerExpired -= OnTimerLocked;
            if (_gameManager  != null)
            {
                _gameManager.OnStateChanged -= RefreshAllUI;
                _gameManager.OnLogMessage   -= AddLogMessage;
                _gameManager.OnGameOver     -= ShowGameOver;
                _gameManager.OnPhaseChanged -= OnPhaseChanged;
            }
            if (_endTurnButton != null) _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 基础保障
        // ══════════════════════════════════════════════════════════════════════

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(go);
        }

        private void EnsureGraphicRaycaster()
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
            if (canvas == null) { Debug.LogError("[BattleUI] 找不到 Canvas！"); return; }
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 对局控制
        // ══════════════════════════════════════════════════════════════════════

        private void StartNewBattle()
        {
            _logMessages.Clear();
            _isTimerLocked = false;
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_logText       != null) _logText.text = "";
            _gameManager.StartBattle();
            AddLogMessage(">>> 对局开始！<<<");
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI 刷新（V2 PlayerData / Entity / BattleCard）
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshAllUI()
        {
            RefreshPlayerPanel(_gameManager.GetHumanPlayer(), true);
            RefreshPlayerPanel(_gameManager.GetAiPlayer(),    false);
            RefreshHandCards();
            RefreshButtons();
            if (_roundText != null) _roundText.text = $"第 {_gameManager.CurrentRound} 回合";
        }

        /// <summary>刷新玩家/对手信息面板（V2 PlayerData.HeroEntity）</summary>
        private void RefreshPlayerPanel(PlayerData player, bool isHuman)
        {
            if (player == null) return;
            var hero = player.HeroEntity;

            var nameText    = isHuman ? _myNameText    : _enemyNameText;
            var hpText      = isHuman ? _myHpText      : _enemyHpText;
            var hpBar       = isHuman ? _myHpBar       : _enemyHpBar;
            var shieldText  = isHuman ? _myShieldText  : _enemyShieldText;
            var deckInfoTxt = isHuman ? _myDeckInfoText : _enemyDeckInfoText;

            if (nameText   != null) nameText.text   = player.PlayerId;
            if (hpText     != null) hpText.text     = $"HP: {hero.Hp}/{hero.MaxHp}";
            if (hpBar      != null) { hpBar.maxValue = hero.MaxHp; hpBar.value = hero.Hp; }
            if (shieldText != null) shieldText.text = hero.Shield > 0 ? $"护盾: {hero.Shield}" : "";

            if (deckInfoTxt != null)
            {
                int h = player.GetCardsInZone(CardZone.Hand).Count;
                int d = player.GetCardsInZone(CardZone.Deck).Count;
                int p = player.GetCardsInZone(CardZone.Discard).Count;
                deckInfoTxt.text = $"手牌:{h}  牌库:{d}  弃牌:{p}";
            }
        }

        /// <summary>
        /// 刷新手牌区域：销毁旧卡牌 GameObject，
        /// 按 BattleGameManager.GetHumanHandCards() 返回顺序重新生成。
        /// </summary>
        private void RefreshHandCards()
        {
            foreach (var obj in _cardObjects)
                if (obj != null) Destroy(obj);
            _cardObjects.Clear();

            if (_cardPrefab    == null) { Debug.LogError("[BattleUI] _cardPrefab 未绑定");    return; }
            if (_handContainer == null) { Debug.LogError("[BattleUI] _handContainer 未绑定"); return; }

            var handCards = _gameManager.GetHumanHandCards();

            for (int i = 0; i < handCards.Count; i++)
            {
                var (battleCard, config) = handCards[i];
                var go = Instantiate(_cardPrefab, _handContainer);
                go.name = $"Card_{i}_{config?.CardName ?? battleCard.ConfigId}";
                SetupCardUI(go, battleCard, config, i);
                _cardObjects.Add(go);
            }
        }

        /// <summary>设置单张卡牌 UI（使用 V2 BattleCard + CardConfig，config 可为 null）</summary>
        private void SetupCardUI(GameObject go, BattleCard battleCard, CardConfig config, int index)
        {
            string cardName  = config?.CardName    ?? battleCard.ConfigId;
            string desc      = config?.Description ?? "（无描述）";
            int    cost      = _gameManager.GetDisplayedCost(battleCard);
            bool   isInstant = config?.TrackType   == CardTrackType.Instant;

            var nameText  = go.transform.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            var costText  = go.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            var descText  = go.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
            var trackText = go.transform.Find("TrackText")?.GetComponent<TextMeshProUGUI>();

            if (nameText  != null) nameText.text  = cardName;
            if (costText  != null) costText.text  = cost.ToString();
            if (descText  != null) descText.text  = desc;
            if (trackText != null) trackText.text = isInstant ? "【瞬策】" : "【定策】";

            var bg = go.GetComponent<Image>();
            if (bg != null)
                bg.color = isInstant
                    ? new Color(0.85f, 0.55f, 0.25f, 1f)   // 橙色 = 瞬策
                    : new Color(0.30f, 0.55f, 0.85f, 1f);   // 蓝色 = 定策

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                int idx = index;
                btn.onClick.AddListener(() => OnCardClicked(idx));
            }
            else
            {
                Debug.LogError($"[BattleUI] 手牌[{index}] {go.name} 缺少 Button 组件！");
            }
        }

        private void RefreshButtons()
        {
            bool can = _gameManager.IsPlayerTurn && !_gameManager.IsGameOver && !_isTimerLocked;

            if (_endTurnButton     != null) _endTurnButton.interactable = can;
            if (_endTurnButtonText != null)
                _endTurnButtonText.text = can ? "结束回合" : (_isTimerLocked ? "等待结算..." : "等待中...");

            foreach (var obj in _cardObjects)
            {
                if (obj == null) continue;
                var btn = obj.GetComponent<Button>();
                if (btn != null) btn.interactable = can;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 用户交互
        // ══════════════════════════════════════════════════════════════════════

        private void OnCardClicked(int handIndex)
        {
            if (!_gameManager.IsPlayerTurn) { AddLogMessage("<color=#ff8888>当前不是你的回合</color>"); return; }
            if (_gameManager.IsGameOver)    { AddLogMessage("<color=#ff8888>游戏已结束</color>");       return; }
            if (_isTimerLocked)             { AddLogMessage("<color=#ff8888>时间已到，等待结算...</color>"); return; }

            var handCards = _gameManager.GetHumanHandCards();
            if (handIndex < 0 || handIndex >= handCards.Count) return;

            var (battleCard, config) = handCards[handIndex];
            bool isInstant = config?.TrackType == CardTrackType.Instant;

            string result = isInstant
                ? _gameManager.PlayerPlayInstantCard(handIndex)
                : _gameManager.PlayerCommitPlanCard(handIndex);

            AddLogMessage($"<color=#aaffaa>出牌: {config?.CardName ?? battleCard.ConfigId} → {result}</color>");
        }

        private void OnEndTurnClicked()
        {
            if (!_gameManager.IsPlayerTurn || _gameManager.IsGameOver) return;
            AddLogMessage("<color=#ffcc00>── 你结束了回合 ──</color>");
            _gameManager.PlayerEndTurn();
        }

        private void OnRestartClicked() => StartNewBattle();

        private void OnDebugStatusClicked()
        {
            AddLogMessage("<color=#aaaaff>── [调试] 战场快照 ──</color>");
            _gameManager.PrintBattleStatus();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 事件处理
        // ══════════════════════════════════════════════════════════════════════

        private void OnTimerLocked()
        {
            _isTimerLocked = true;
            RefreshButtons();
            AddLogMessage("<color=#ffcc44>⏱ 操作时间结束，自动结算...</color>");
            if (_gameManager.IsPlayerTurn && !_gameManager.IsGameOver)
                _gameManager.PlayerEndTurn();
        }

        private void OnPhaseChanged(string phaseDesc)
        {
            if (_phaseText != null) _phaseText.text = phaseDesc;

            if (phaseDesc.Contains("操作期"))
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _phaseTimer?.OnServerPhaseChange(RoundPhase.OperationWindow, nowMs);
                _isTimerLocked = false;
                RefreshButtons();
            }
            else if (phaseDesc.Contains("结算"))
            {
                _roundTimerUI?.ShowStaticPhase("结算中...");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 日志 / 游戏结束
        // ══════════════════════════════════════════════════════════════════════

        private void AddLogMessage(string msg)
        {
            _logMessages.Add(msg);
            while (_logMessages.Count > MaxLogLines) _logMessages.RemoveAt(0);
            if (_logText != null) _logText.text = string.Join("\n", _logMessages);
            if (_logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _logScrollRect.verticalNormalizedPosition = 0f;
            }
            Debug.Log($"[BattleUI] {msg}");
        }

        private void ShowGameOver(int winnerCode)
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
            if (_gameOverText  != null)
                _gameOverText.text = winnerCode == 1 ? "[胜利] 你赢了！"
                                   : winnerCode == 2 ? "[失败] 你输了"
                                   : "[平局]";
            AddLogMessage(">>> 对局结束！<<<");
        }
    }
}
