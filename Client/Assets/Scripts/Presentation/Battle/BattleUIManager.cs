using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.GameLogic.RoundFlow;
using CardMoba.Client.Presentation.UI.Components;

namespace CardMoba.Client.Presentation.Battle
{
    /// <summary>
    /// 战斗UI主控制器 —— 管理整个战斗界面的显示和交互。
    /// 
    /// 挂载方式：挂在战斗场景的根 Canvas 上。
    /// 在 Inspector 中拖拽绑定各 UI 引用。
    /// 
    /// 架构：
    ///   BattleUIManager (Presentation层)
    ///     ↓ 调用
    ///   BattleGameManager (GameLogic层)
    ///     ↓ 调用
    ///   RoundManager / SettlementEngine (BattleCore层)
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        // ══════════════════════════════════════
        // Inspector 引用 —— 在 Unity Editor 中拖拽绑定
        // ══════════════════════════════════════

        [Header("── 我方信息面板 ──")]
        [SerializeField] private TextMeshProUGUI _myNameText;
        [SerializeField] private TextMeshProUGUI _myHpText;
        [SerializeField] private Slider _myHpBar;
        [SerializeField] private TextMeshProUGUI _myEnergyText;
        [SerializeField] private TextMeshProUGUI _myShieldText;
        [SerializeField] private TextMeshProUGUI _myDeckInfoText;

        [Header("── 对手信息面板 ──")]
        [SerializeField] private TextMeshProUGUI _enemyNameText;
        [SerializeField] private TextMeshProUGUI _enemyHpText;
        [SerializeField] private Slider _enemyHpBar;
        [SerializeField] private TextMeshProUGUI _enemyEnergyText;
        [SerializeField] private TextMeshProUGUI _enemyShieldText;
        [SerializeField] private TextMeshProUGUI _enemyDeckInfoText;

        [Header("── 手牌区域 ──")]
        [SerializeField] private Transform _handContainer;
        [SerializeField] private GameObject _cardPrefab;

        [Header("── 操作按钮 ──")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private TextMeshProUGUI _endTurnButtonText;

        [Header("── 战斗信息 ──")]
        [SerializeField] private TextMeshProUGUI _phaseText;
        [SerializeField] private TextMeshProUGUI _roundText;
        [SerializeField] private ScrollRect _logScrollRect;
        [SerializeField] private TextMeshProUGUI _logText;

        [Header("── 计时器 UI ──")]
        [SerializeField] private RoundTimerUI _roundTimerUI;

        [Header("── 游戏结束面板 ──")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _gameOverText;
        [SerializeField] private Button _restartButton;

        // ══════════════════════════════════════
        // 内部状态
        // ══════════════════════════════════════

        private GameLogic.BattleGameManager _gameManager;

        /// <summary>
        /// 客户端阶段计时器（MonoBehaviour），挂在同一 GameObject 或子节点上。
        /// 由 Awake() 自动查找，也可在 Inspector 中手动绑定。
        /// </summary>
        private RoundPhaseTimerClient _phaseTimer;

        /// <summary>计时归零后是否已锁定操作</summary>
        private bool _isTimerLocked;

        private int _selectedCardIndex = -1;
        private List<GameObject> _cardObjects = new List<GameObject>();
        private List<string> _logMessages = new List<string>();

        /// <summary>日志面板最大显示行数</summary>
        private const int MaxLogLines = 50;

        // ══════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════

        private void Awake()
        {
            // ── 确保场景中存在 EventSystem（UI点击必需）──
            EnsureEventSystem();

            // ── 确保当前 Canvas 有 GraphicRaycaster（Button点击必需）──
            EnsureGraphicRaycaster();

            // 初始化游戏管理器
            _gameManager = new GameLogic.BattleGameManager();

            // ── 查找或创建 RoundPhaseTimerClient ──
            _phaseTimer = GetComponent<RoundPhaseTimerClient>();
            if (_phaseTimer == null)
                _phaseTimer = gameObject.AddComponent<RoundPhaseTimerClient>();

            // ── 将计时器绑定到 RoundTimerUI ──
            if (_roundTimerUI != null)
                _roundTimerUI.BindTimer(_phaseTimer);

            // ── 订阅计时器锁定事件 ──
            _phaseTimer.OnLocalTimerExpired += OnTimerLocked;

            // 订阅游戏管理器事件
            _gameManager.OnStateChanged += RefreshAllUI;
            _gameManager.OnLogMessage += AddLogMessage;
            _gameManager.OnGameOver += ShowGameOver;
            _gameManager.OnPhaseChanged += OnGameManagerPhaseChanged;

            // 绑定按钮事件
            if (_endTurnButton != null)
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);

            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);

            // 隐藏游戏结束面板
            if (_gameOverPanel != null)
                _gameOverPanel.SetActive(false);
        }

        private void Start()
        {
            // 开始一场新对战
            StartNewBattle();
        }

        private void OnDestroy()
        {
            // 清理计时器事件
            if (_phaseTimer != null)
                _phaseTimer.OnLocalTimerExpired -= OnTimerLocked;

            // 清理事件订阅
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= RefreshAllUI;
                _gameManager.OnLogMessage -= AddLogMessage;
                _gameManager.OnGameOver -= ShowGameOver;
                _gameManager.OnPhaseChanged -= OnGameManagerPhaseChanged;
            }

            // 清理按钮事件
            if (_endTurnButton != null)
                _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);

            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        // ══════════════════════════════════════
        // 场景基础组件保障
        // ══════════════════════════════════════

        /// <summary>
        /// 确保场景中存在 EventSystem。
        /// Unity UI 的所有点击事件都依赖 EventSystem，缺少时按钮完全无响应。
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                DontDestroyOnLoad(esGo);
                Debug.Log("[BattleUI] 自动创建 EventSystem");
            }
        }

        /// <summary>
        /// 确保本 GameObject 所在的 Canvas 上有 GraphicRaycaster。
        /// 没有 GraphicRaycaster，Canvas 内的所有 Button 点击都不会被检测到。
        /// </summary>
        private void EnsureGraphicRaycaster()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // BattleUIManager 本身就挂在 Canvas 上
                canvas = GetComponent<Canvas>();
            }
            if (canvas == null)
            {
                Debug.LogError("[BattleUI] 找不到 Canvas！请将 BattleUIManager 挂在 Canvas GameObject 上。");
                return;
            }
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log("[BattleUI] 自动添加 GraphicRaycaster 到 Canvas");
            }
        }

        // ══════════════════════════════════════
        // 对局控制
        // ══════════════════════════════════════

        /// <summary>
        /// 开始一场新的对战。
        /// </summary>
        private void StartNewBattle()
        {
            _logMessages.Clear();
            _selectedCardIndex = -1;
            _isTimerLocked = false;

            if (_gameOverPanel != null)
                _gameOverPanel.SetActive(false);

            if (_logText != null)
                _logText.text = "";

            _gameManager.StartBattle();

            AddLogMessage(">>> 对局开始! 操作窗口期 10 秒 <<<");
        }

        // ══════════════════════════════════════
        // UI 刷新
        // ══════════════════════════════════════

        /// <summary>
        /// 刷新所有 UI 元素（由 OnStateChanged 事件触发）。
        /// </summary>
        private void RefreshAllUI()
        {
            RefreshPlayerInfo();
            RefreshEnemyInfo();
            RefreshHandCards();
            RefreshButtons();
            RefreshRoundInfo();
        }

        /// <summary>
        /// 刷新我方信息面板。
        /// </summary>
        private void RefreshPlayerInfo()
        {
            PlayerBattleState player = _gameManager.GetHumanPlayer();
            if (player == null) return;

            if (_myNameText != null)
                _myNameText.text = player.PlayerName;

            if (_myHpText != null)
                _myHpText.text = $"HP: {player.Hp}/{player.MaxHp}";

            if (_myHpBar != null)
            {
                _myHpBar.maxValue = player.MaxHp;
                _myHpBar.value = player.Hp;
            }

            if (_myEnergyText != null)
                _myEnergyText.text = $"能量: {player.Energy}/{player.EnergyPerRound}";

            if (_myShieldText != null)
                _myShieldText.text = player.Shield > 0 ? $"护盾: {player.Shield}" : "";

            if (_myDeckInfoText != null)
                _myDeckInfoText.text = $"牌库:{player.Deck.Count} 弃牌:{player.DiscardPile.Count}";
        }

        /// <summary>
        /// 刷新对手信息面板。
        /// </summary>
        private void RefreshEnemyInfo()
        {
            PlayerBattleState enemy = _gameManager.GetAiPlayer();
            if (enemy == null) return;

            if (_enemyNameText != null)
                _enemyNameText.text = enemy.PlayerName;

            if (_enemyHpText != null)
                _enemyHpText.text = $"HP: {enemy.Hp}/{enemy.MaxHp}";

            if (_enemyHpBar != null)
            {
                _enemyHpBar.maxValue = enemy.MaxHp;
                _enemyHpBar.value = enemy.Hp;
            }

            if (_enemyEnergyText != null)
                _enemyEnergyText.text = $"能量: {enemy.Energy}/{enemy.EnergyPerRound}";

            if (_enemyShieldText != null)
                _enemyShieldText.text = enemy.Shield > 0 ? $"护盾: {enemy.Shield}" : "";

            if (_enemyDeckInfoText != null)
                _enemyDeckInfoText.text = $"牌库:{enemy.Deck.Count} 弃牌:{enemy.DiscardPile.Count}";
        }

        /// <summary>
        /// 刷新手牌区域：销毁旧卡牌UI，根据当前手牌重新生成。
        /// </summary>
        private void RefreshHandCards()
        {
            // 清空旧的卡牌UI
            for (int i = 0; i < _cardObjects.Count; i++)
            {
                if (_cardObjects[i] != null)
                    Destroy(_cardObjects[i]);
            }
            _cardObjects.Clear();
            _selectedCardIndex = -1;

            // ── 诊断：检查必要引用是否绑定 ──
            PlayerBattleState player = _gameManager.GetHumanPlayer();
            if (player == null)
            {
                Debug.LogError("[BattleUI] RefreshHandCards: GetHumanPlayer() 返回 null！");
                return;
            }
            if (_cardPrefab == null)
            {
                Debug.LogError("[BattleUI] RefreshHandCards: _cardPrefab 未绑定！请在 Inspector 中拖拽赋值。");
                return;
            }
            if (_handContainer == null)
            {
                Debug.LogError("[BattleUI] RefreshHandCards: _handContainer 未绑定！请在 Inspector 中拖拽赋值。");
                return;
            }

            Debug.Log($"[BattleUI] 刷新手牌，共 {player.Hand.Count} 张");

            // 为每张手牌创建UI
            for (int i = 0; i < player.Hand.Count; i++)
            {
                CardInstance card = player.Hand[i];
                GameObject cardObj = Instantiate(_cardPrefab, _handContainer);
                cardObj.name = $"Card_{i}_{card.CardName}";

                // 设置卡牌显示信息
                SetupCardUI(cardObj, card, i);

                _cardObjects.Add(cardObj);
                Debug.Log($"[BattleUI] 创建手牌[{i}]: {card.CardName} | 费用={card.EnergyCost} | 类型={card.TrackType}");
            }
        }

        /// <summary>
        /// 设置单张卡牌的UI显示和点击事件。
        /// </summary>
        private void SetupCardUI(GameObject cardObj, CardInstance card, int index)
        {
            // 查找子物体上的文本组件（TextMeshPro版本）
            TextMeshProUGUI nameText = cardObj.transform.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = cardObj.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI descText = cardObj.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI trackText = cardObj.transform.Find("TrackText")?.GetComponent<TextMeshProUGUI>();
            Image bgImage = cardObj.GetComponent<Image>();

            if (nameText != null) nameText.text = card.CardName;
            if (costText != null) costText.text = card.EnergyCost.ToString();
            if (descText != null) descText.text = card.Description;
            if (trackText != null)
                trackText.text = card.TrackType == CardTrackType.Instant ? "【瞬策】" : "【定策】";

            // 根据轨道类型设置背景色
            if (bgImage != null)
            {
                if (card.TrackType == CardTrackType.Instant)
                    bgImage.color = new Color(0.85f, 0.55f, 0.25f, 1f); // 橙色 = 瞬策
                else
                    bgImage.color = new Color(0.3f, 0.55f, 0.85f, 1f); // 蓝色 = 定策
            }

            // 绑定点击事件
            Button btn = cardObj.GetComponent<Button>();
            if (btn != null)
            {
                int capturedIndex = index; // 闭包捕获
                btn.onClick.AddListener(() => OnCardClicked(capturedIndex));
                Debug.Log($"[BattleUI] 手牌[{index}] Button 已绑定，interactable={btn.interactable}");
            }
            else
            {
                Debug.LogError($"[BattleUI] 手牌[{index}] {cardObj.name} 上找不到 Button 组件！");
            }
        }

        /// <summary>
        /// 刷新按钮状态。
        /// 当计时器锁定时，即使是玩家回合也不允许操作。
        /// 同时同步所有手牌按钮的可交互状态。
        /// </summary>
        private void RefreshButtons()
        {
            bool canOperate = _gameManager.IsPlayerTurn && !_gameManager.IsGameOver && !_isTimerLocked;

            if (_endTurnButton != null)
                _endTurnButton.interactable = canOperate;

            if (_endTurnButtonText != null)
                _endTurnButtonText.text = canOperate ? "结束回合" : (_isTimerLocked ? "等待结算..." : "等待中...");

            // ── 同步所有手牌按钮的可交互状态 ──
            for (int i = 0; i < _cardObjects.Count; i++)
            {
                if (_cardObjects[i] == null) continue;
                Button btn = _cardObjects[i].GetComponent<Button>();
                if (btn != null)
                    btn.interactable = canOperate;
            }
        }

        /// <summary>
        /// 刷新回合信息。
        /// </summary>
        private void RefreshRoundInfo()
        {
            if (_roundText != null)
                _roundText.text = $"第 {_gameManager.CurrentRound} 回合";
        }

        // ══════════════════════════════════════
        // 用户交互回调
        // ══════════════════════════════════════

        /// <summary>
        /// 点击手牌卡牌 —— 直接尝试打出/提交该卡牌。
        /// </summary>
        private void OnCardClicked(int handIndex)
        {
            // ── 诊断日志（确认点击已到达此处）──
            Debug.Log($"[BattleUI] OnCardClicked({handIndex}) | IsPlayerTurn={_gameManager.IsPlayerTurn} | IsGameOver={_gameManager.IsGameOver} | IsTimerLocked={_isTimerLocked}");

            if (!_gameManager.IsPlayerTurn)
            {
                AddLogMessage("<color=#ff8888>当前不是你的回合，无法出牌</color>");
                return;
            }
            if (_gameManager.IsGameOver)
            {
                AddLogMessage("<color=#ff8888>游戏已结束</color>");
                return;
            }
            if (_isTimerLocked)
            {
                AddLogMessage("<color=#ff8888>时间已到，等待结算中...</color>");
                return;
            }

            PlayerBattleState player = _gameManager.GetHumanPlayer();
            if (player == null)
            {
                Debug.LogError("[BattleUI] 玩家状态为 null！");
                return;
            }
            if (handIndex < 0 || handIndex >= player.Hand.Count)
            {
                Debug.LogError($"[BattleUI] 手牌索引越界: {handIndex}，手牌数量: {player.Hand.Count}");
                return;
            }

            CardInstance card = player.Hand[handIndex];
            Debug.Log($"[BattleUI] 尝试打出: {card.CardName}(ID={card.CardId}) 类型={card.TrackType} 费用={card.EnergyCost} 当前能量={player.Energy}");

            // 能量检查
            if (player.Energy < card.EnergyCost)
            {
                AddLogMessage($"<color=#ff4444>能量不足！需要{card.EnergyCost}，当前{player.Energy}</color>");
                return;
            }

            // 根据牌的轨道类型决定操作
            string result;
            if (card.TrackType == CardTrackType.Instant)
            {
                result = _gameManager.PlayerPlayInstantCard(handIndex);
            }
            else
            {
                result = _gameManager.PlayerCommitPlanCard(handIndex);
            }

            AddLogMessage($"<color=#aaffaa>出牌: {card.CardName} → {result}</color>");
            Debug.Log($"[BattleUI] 出牌结果: {result}");
        }

        /// <summary>
        /// 点击「结束回合」按钮。
        /// </summary>
        private void OnEndTurnClicked()
        {
            if (!_gameManager.IsPlayerTurn || _gameManager.IsGameOver) return;

            AddLogMessage("<color=#ffcc00>-- 你结束了回合 --</color>");
            _gameManager.PlayerEndTurn();
        }

        /// <summary>
        /// 点击「重新开始」按钮。
        /// </summary>
        private void OnRestartClicked()
        {
            StartNewBattle();
        }

        // ══════════════════════════════════════
        // 事件处理
        // ══════════════════════════════════════

        /// <summary>
        /// 计时器归零时：锁定玩家输入，自动触发结束回合（模拟服务端推进阶段）。
        /// </summary>
        private void OnTimerLocked()
        {
            _isTimerLocked = true;
            RefreshButtons();
            AddLogMessage("<color=#ffcc44>⏱ 操作时间结束，自动结算...</color>");
            Debug.Log("[BattleUI] 操作窗口关闭 → 自动触发结算");

            // 本地单机模式：计时器到期时自动推进回合（模拟服务端推送结算）
            if (_gameManager.IsPlayerTurn && !_gameManager.IsGameOver)
            {
                _gameManager.PlayerEndTurn();
            }
        }

        /// <summary>
        /// 接收 BattleGameManager 的阶段变更通知，同时驱动计时器和静态文字。
        /// </summary>
        private void OnGameManagerPhaseChanged(string phaseDesc)
        {
            // 旧的文字显示保留（兼容现有 _phaseText）
            if (_phaseText != null)
                _phaseText.text = phaseDesc;

            // 根据阶段描述判断是否是操作窗口期，驱动计时器
            if (phaseDesc.Contains("操作期"))
            {
                // 模拟服务端推送：用当前 UTC 时间戳启动操作窗口期
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _phaseTimer?.OnServerPhaseChange(RoundPhase.OperationWindow, nowMs);
                _isTimerLocked = false;
                RefreshButtons();
            }
            else if (phaseDesc.Contains("结算"))
            {
                // 结算阶段：显示静态文字，停止计时器倒计时显示
                _roundTimerUI?.ShowStaticPhase("结算中...");
            }
        }

        /// <summary>
        /// 更新阶段提示文字（保留兼容旧签名的包装）。
        /// </summary>
        private void UpdatePhaseText(string phaseDesc)
        {
            if (_phaseText != null)
                _phaseText.text = phaseDesc;
        }

        /// <summary>
        /// 添加一条日志消息到日志面板。
        /// </summary>
        private void AddLogMessage(string message)
        {
            _logMessages.Add(message);

            // 限制最大行数
            while (_logMessages.Count > MaxLogLines)
            {
                _logMessages.RemoveAt(0);
            }

            // 更新显示
            if (_logText != null)
            {
                _logText.text = string.Join("\n", _logMessages);
            }

            // 自动滚动到底部
            if (_logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _logScrollRect.verticalNormalizedPosition = 0f;
            }

            // 同步输出到 Console（方便调试）
            Debug.Log($"[BattleUI] {message}");
        }

        /// <summary>
        /// 显示游戏结束面板。
        /// </summary>
        private void ShowGameOver(int winnerTeamId)
        {
            if (_gameOverPanel != null)
                _gameOverPanel.SetActive(true);

            if (_gameOverText != null)
            {
                // winnerTeamId: 1 = 玩家队伍, 2 = AI队伍, -1 = 平局
                if (winnerTeamId == 1)
                    _gameOverText.text = "胜利! 你赢了!";
                else if (winnerTeamId == 2)
                    _gameOverText.text = "失败... 你输了";
                else
                    _gameOverText.text = "平局!";
            }

            AddLogMessage(">>> 对局结束! <<<");
        }
    }
}
