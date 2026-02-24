using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.RoundStateMachine;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 战斗流程管理器 —— 连接 UI 层和 BattleCore 的桥梁。
    /// 
    /// 职责：
    ///   - 管理单局对战的完整生命周期
    ///   - 提供 UI 层调用的操作接口（出牌、结束回合等）
    ///   - 内置简单AI控制对手行为
    ///   - 通过事件通知 UI 层刷新显示
    /// 
    /// 设计原则：
    ///   - 本类属于 GameLogic 层，不依赖 UnityEngine（除非需要协程）
    ///   - UI 层通过事件订阅获取状态变化，不直接读取 BattleCore
    /// </summary>
    public class BattleGameManager
    {
        // ── 事件定义（UI层订阅这些事件来刷新显示） ──

        /// <summary>对局状态发生变化时触发（HP/能量/护盾变化、手牌变化等）</summary>
        public event Action OnStateChanged;

        /// <summary>新增日志消息时触发</summary>
        public event Action<string> OnLogMessage;

        /// <summary>对局结束时触发（参数：胜者ID，-1为平局）</summary>
        public event Action<int> OnGameOver;

        /// <summary>回合阶段切换时触发（参数：阶段描述）</summary>
        public event Action<string> OnPhaseChanged;

        // ── 内部状态 ──

        private RoundManager _roundManager;
        private BattleContext _ctx;

        /// <summary>人类玩家的ID</summary>
        public const int HumanPlayerId = 1;

        /// <summary>AI玩家的ID</summary>
        public const int AiPlayerId = 2;

        // ── 公开属性（供UI层读取） ──

        /// <summary>当前战斗上下文（只读访问）</summary>
        public BattleContext Context => _ctx;

        /// <summary>当前是否处于玩家操作阶段</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>对局是否已结束</summary>
        public bool IsGameOver => _ctx != null && _ctx.IsGameOver;

        /// <summary>当前回合数</summary>
        public int CurrentRound => _ctx?.CurrentRound ?? 0;

        // ── 初始化 ──

        /// <summary>
        /// 开始一场新的1v1对战。
        /// </summary>
        public void StartBattle()
        {
            // 确保配置已加载
            EnsureConfigLoaded();

            _roundManager = new RoundManager();

            // 从配置加载卡组
            List<CardConfig> deck1 = CreateDeckFromConfig();
            List<CardConfig> deck2 = CreateDeckFromConfig();

            // 初始化对局
            _ctx = _roundManager.InitBattle(deck1, deck2);

            // 输出初始化日志
            FlushLogs();

            // 进入玩家操作阶段
            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 你的操作期");
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 使用指定卡牌ID列表开始对战（供外部指定卡组）。
        /// </summary>
        /// <param name="playerDeckIds">玩家卡组ID列表</param>
        /// <param name="aiDeckIds">AI卡组ID列表（null则使用默认）</param>
        public void StartBattleWithDeck(int[] playerDeckIds, int[] aiDeckIds = null)
        {
            EnsureConfigLoaded();

            _roundManager = new RoundManager();

            List<CardConfig> deck1 = CreateDeckFromCardIds(playerDeckIds);
            List<CardConfig> deck2 = aiDeckIds != null 
                ? CreateDeckFromCardIds(aiDeckIds) 
                : CreateDeckFromConfig();

            _ctx = _roundManager.InitBattle(deck1, deck2);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 你的操作期");
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 确保配置管理器已加载。
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (!CardConfigManager.Instance.IsLoaded)
            {
                CardConfigManager.Instance.LoadAll();
            }
        }

        // ── 玩家操作接口（由UI层调用） ──

        /// <summary>
        /// 玩家打出一张瞬策牌。
        /// </summary>
        /// <param name="handIndex">手牌索引</param>
        /// <returns>操作结果描述</returns>
        public string PlayerPlayInstantCard(int handIndex)
        {
            if (!IsPlayerTurn || _ctx.IsGameOver) return "错误：当前不是操作阶段";

            PlayerBattleState player = _ctx.GetPlayer(HumanPlayerId);
            if (player == null) return "错误：玩家不存在";

            // 瞬策牌默认目标为对手
            CardConfig card = player.Hand[handIndex];
            int targetId = GetTargetForCard(card, HumanPlayerId, AiPlayerId);

            string result = _roundManager.PlayCard(_ctx, HumanPlayerId, handIndex, targetId);

            FlushLogs();
            OnLogMessage?.Invoke($"你 → {result}");
            OnStateChanged?.Invoke();

            // 检查游戏结束
            if (_ctx.IsGameOver)
            {
                OnGameOver?.Invoke(_ctx.WinnerPlayerId);
            }

            return result;
        }

        /// <summary>
        /// 玩家提交一张定策牌（暗置）。
        /// </summary>
        /// <param name="handIndex">手牌索引</param>
        /// <returns>操作结果描述</returns>
        public string PlayerCommitPlanCard(int handIndex)
        {
            if (!IsPlayerTurn || _ctx.IsGameOver) return "错误：当前不是操作阶段";

            PlayerBattleState player = _ctx.GetPlayer(HumanPlayerId);
            if (player == null) return "错误：玩家不存在";

            CardConfig card = player.Hand[handIndex];
            int targetId = GetTargetForCard(card, HumanPlayerId, AiPlayerId);

            string result = _roundManager.CommitPlanCard(_ctx, HumanPlayerId, handIndex, targetId);

            FlushLogs();
            OnLogMessage?.Invoke($"你 → {result}");
            OnStateChanged?.Invoke();

            return result;
        }

        /// <summary>
        /// 玩家结束回合 —— 触发AI操作 → 定策牌结算 → 回合结束 → 下一回合开始。
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || _ctx.IsGameOver) return;

            IsPlayerTurn = false;

            // ── 锁定玩家操作 ──
            _roundManager.LockOperation(_ctx, HumanPlayerId);
            FlushLogs();

            // ── AI操作阶段 ──
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 对手操作中...");
            OnStateChanged?.Invoke();

            ExecuteAiTurn();
            FlushLogs();

            if (_ctx.IsGameOver)
            {
                OnGameOver?.Invoke(_ctx.WinnerPlayerId);
                return;
            }

            // ── 锁定AI操作 ──
            _roundManager.LockOperation(_ctx, AiPlayerId);
            FlushLogs();

            // ── 回合结算（阶段4~7：定策牌结算 → 濒死判定 → 弃牌清能量） ──
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 结算中...");
            _roundManager.EndRound(_ctx);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (_ctx.IsGameOver)
            {
                OnGameOver?.Invoke(_ctx.WinnerPlayerId);
                return;
            }

            // ── 下一回合开始 ──
            _roundManager.BeginNextRound(_ctx);
            FlushLogs();

            // 进入新的玩家操作阶段
            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 你的操作期");
            OnStateChanged?.Invoke();
        }

        // ── AI逻辑 ──

        /// <summary>
        /// 简单AI：尽量把能量用完，优先出伤害牌。
        /// </summary>
        private void ExecuteAiTurn()
        {
            PlayerBattleState ai = _ctx.GetPlayer(AiPlayerId);
            if (ai == null || !ai.IsAlive) return;

            bool playedAny = true;
            while (playedAny && ai.Energy > 0 && ai.Hand.Count > 0)
            {
                playedAny = false;
                for (int i = ai.Hand.Count - 1; i >= 0; i--)
                {
                    if (ai.Energy <= 0) break;

                    CardConfig card = ai.Hand[i];
                    if (ai.Energy < card.EnergyCost) continue;

                    int targetId = GetTargetForCard(card, AiPlayerId, HumanPlayerId);

                    string result;
                    if (card.TrackType == CardTrackType.Instant)
                    {
                        result = _roundManager.PlayCard(_ctx, AiPlayerId, i, targetId);
                    }
                    else
                    {
                        result = _roundManager.CommitPlanCard(_ctx, AiPlayerId, i, targetId);
                    }

                    OnLogMessage?.Invoke($"对手 → {result}");
                    playedAny = true;

                    if (_ctx.IsGameOver) return;
                    break; // 索引变化，重新扫描
                }
            }
        }

        // ── 辅助方法 ──

        /// <summary>
        /// 根据卡牌目标类型决定目标玩家。
        /// </summary>
        private int GetTargetForCard(CardConfig card, int selfId, int opponentId)
        {
            switch (card.TargetType)
            {
                case CardTargetType.CurrentEnemy:
                case CardTargetType.AnyEnemy:
                case CardTargetType.AllEnemies:
                    return opponentId;

                case CardTargetType.Self:
                case CardTargetType.AnyAlly:
                case CardTargetType.AllAllies:
                    return selfId;

                default:
                    return opponentId;
            }
        }

        /// <summary>
        /// 将 BattleContext 中的日志通过事件发送给 UI，然后清空。
        /// </summary>
        private void FlushLogs()
        {
            if (_ctx == null) return;
            for (int i = 0; i < _ctx.RoundLog.Count; i++)
            {
                OnLogMessage?.Invoke(_ctx.RoundLog[i]);
            }
            _ctx.RoundLog.Clear();
        }

        /// <summary>
        /// 获取人类玩家的状态。
        /// </summary>
        public PlayerBattleState GetHumanPlayer()
        {
            return _ctx?.GetPlayer(HumanPlayerId);
        }

        /// <summary>
        /// 获取AI玩家的状态。
        /// </summary>
        public PlayerBattleState GetAiPlayer()
        {
            return _ctx?.GetPlayer(AiPlayerId);
        }

        // ══════════════════════════════════════════════════════════════
        // 卡组创建（从配置加载）
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 默认测试卡组的 CardId 列表（15张牌）。
        /// 对应 StreamingAssets/Config/cards.json 中的卡牌定义。
        /// 
        /// 卡牌编号参照 Excel 模板（Cards.xlsx）：
        /// 瞬策牌：1001火球术, 1002雷霆一击, 1003快速格挡
        /// 定策牌：2001蓄力斩, 2002铁壁, 2003生命回复, 2004见招拆招, 2005铁斩波, ...
        /// </summary>
        private static readonly int[] DefaultTestDeckIds = new int[]
        {
            // ═══ 瞬策牌 (7张) ═══
            1001, 1001, 1001,    // 火球术 x3 (造成4点伤害)
            1002, 1002,          // 雷霆一击 x2 (造成7点伤害)
            1003, 1003,          // 快速格挡 x2 (获得3点护盾)
            
            // ═══ 定策牌 (8张) ═══
            2001, 2001, 2001,    // 蓄力斩 x3 (8点伤害)
            2002, 2002,          // 铁壁 x2 (获得5点护盾)
            2003,                // 生命回复 x1 (回复5点生命)
            2004,                // 见招拆招 x1 (反制敌方伤害)
            2005,                // 铁斩波 x1 (4护甲+5伤害)
        };

        /// <summary>
        /// 从配置文件创建默认测试卡组。
        /// </summary>
        private List<CardConfig> CreateDeckFromConfig()
        {
            return CreateDeckFromCardIds(DefaultTestDeckIds);
        }

        /// <summary>
        /// 根据卡牌ID列表创建卡组。
        /// </summary>
        /// <param name="cardIds">卡牌ID数组</param>
        /// <returns>卡牌配置列表（每个是独立副本）</returns>
        private List<CardConfig> CreateDeckFromCardIds(int[] cardIds)
        {
            List<CardConfig> deck = new List<CardConfig>();
            var configManager = CardConfigManager.Instance;

            foreach (int cardId in cardIds)
            {
                // 使用 CloneCard 获取副本，避免修改原始配置
                CardConfig card = configManager.CloneCard(cardId);
                if (card != null)
                {
                    deck.Add(card);
                }
                else
                {
                    // 配置中不存在该卡牌，输出警告但继续
                    OnLogMessage?.Invoke($"[警告] 卡牌配置不存在: {cardId}，已跳过");
                }
            }

            // 如果配置加载失败导致卡组为空，使用后备硬编码卡组
            if (deck.Count == 0)
            {
                OnLogMessage?.Invoke("[警告] 配置加载失败，使用后备硬编码卡组");
                return CreateFallbackDeck();
            }

            return deck;
        }

        /// <summary>
        /// 后备硬编码卡组（当配置加载失败时使用）。
        /// </summary>
        private List<CardConfig> CreateFallbackDeck()
        {
            List<CardConfig> deck = new List<CardConfig>();

            // 最小可用卡组：5张火球术
            for (int i = 0; i < 5; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 9999,
                    CardName = "后备火球",
                    Description = "造成3点伤害（后备卡）",
                    TrackType = CardTrackType.Instant,
                    Tags = CardTag.Damage,
                    TargetType = CardTargetType.CurrentEnemy,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.DealDamage, Value = 3 }
                    }
                });
            }

            return deck;
        }
    }
}
