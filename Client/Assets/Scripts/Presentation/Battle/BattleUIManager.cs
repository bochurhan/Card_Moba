using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

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

        [Header("── 游戏结束面板 ──")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _gameOverText;
        [SerializeField] private Button _restartButton;

        // ══════════════════════════════════════
        // 内部状态
        // ══════════════════════════════════════

        private GameLogic.BattleGameManager _gameManager;
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
            // 初始化游戏管理器
            _gameManager = new GameLogic.BattleGameManager();

            // 订阅事件
            _gameManager.OnStateChanged += RefreshAllUI;
            _gameManager.OnLogMessage += AddLogMessage;
            _gameManager.OnGameOver += ShowGameOver;
            _gameManager.OnPhaseChanged += UpdatePhaseText;

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
            // 清理事件订阅
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= RefreshAllUI;
                _gameManager.OnLogMessage -= AddLogMessage;
                _gameManager.OnGameOver -= ShowGameOver;
                _gameManager.OnPhaseChanged -= UpdatePhaseText;
            }

            // 清理按钮事件
            if (_endTurnButton != null)
                _endTurnButton.onClick.RemoveListener(OnEndTurnClicked);

            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(OnRestartClicked);
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

            if (_gameOverPanel != null)
                _gameOverPanel.SetActive(false);

            if (_logText != null)
                _logText.text = "";

            _gameManager.StartBattle();

            AddLogMessage(">>> 对局开始! 选择一张手牌点击出牌 <<<");
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

            // 获取玩家手牌
            PlayerBattleState player = _gameManager.GetHumanPlayer();
            if (player == null || _cardPrefab == null || _handContainer == null) return;

            // 为每张手牌创建UI
            for (int i = 0; i < player.Hand.Count; i++)
            {
                CardConfig card = player.Hand[i];
                GameObject cardObj = Instantiate(_cardPrefab, _handContainer);
                cardObj.name = $"Card_{i}_{card.CardName}";

                // 设置卡牌显示信息
                SetupCardUI(cardObj, card, i);

                _cardObjects.Add(cardObj);
            }
        }

        /// <summary>
        /// 设置单张卡牌的UI显示和点击事件。
        /// </summary>
        private void SetupCardUI(GameObject cardObj, CardConfig card, int index)
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
            }
        }

        /// <summary>
        /// 刷新按钮状态。
        /// </summary>
        private void RefreshButtons()
        {
            bool canOperate = _gameManager.IsPlayerTurn && !_gameManager.IsGameOver;

            if (_endTurnButton != null)
                _endTurnButton.interactable = canOperate;

            if (_endTurnButtonText != null)
                _endTurnButtonText.text = canOperate ? "结束回合" : "等待中...";
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
            if (!_gameManager.IsPlayerTurn || _gameManager.IsGameOver) return;

            PlayerBattleState player = _gameManager.GetHumanPlayer();
            if (player == null || handIndex < 0 || handIndex >= player.Hand.Count) return;

            CardConfig card = player.Hand[handIndex];

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
        /// 更新阶段提示文字。
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
