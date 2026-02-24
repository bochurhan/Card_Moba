using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.RoundStateMachine;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

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
            _roundManager = new RoundManager();

            // 创建双方卡组（临时硬编码，后续从配置表读取）
            List<CardConfig> deck1 = CreateTestDeck();
            List<CardConfig> deck2 = CreateTestDeck();

            // 初始化对局
            _ctx = _roundManager.InitBattle(deck1, deck2);

            // 输出初始化日志
            FlushLogs();

            // 进入玩家操作阶段
            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第{_ctx.CurrentRound}回合 · 你的操作期");
            OnStateChanged?.Invoke();
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
                    if (card.TrackType == CardTrackType.瞬策牌)
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
                case CardTargetType.SingleEnemy:
                case CardTargetType.AllEnemiesInLane:
                    return opponentId;

                case CardTargetType.Self:
                case CardTargetType.SingleAlly:
                case CardTargetType.AllAlliesInLane:
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

        // ── 测试卡组（临时，后续从配置表读取） ──

        /// <summary>
        /// 创建一套测试卡组（15张牌，混合瞬策和定策）。
        /// 使用新的 Effects 列表系统，符合 V4.0 结算机制。
        /// </summary>
        private List<CardConfig> CreateTestDeck()
        {
            List<CardConfig> deck = new List<CardConfig>();

            // ═══ 瞬策牌 ═══

            // 火球术 x3：造成4点伤害（瞬策）
            for (int i = 0; i < 3; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1001,
                    CardName = "火球术",
                    Description = "造成4点伤害",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.伤害,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 4 }
                    }
                });
            }

            // 雷霆一击 x2：造成7点伤害（瞬策）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1002,
                    CardName = "雷霆一击",
                    Description = "造成7点伤害",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.伤害,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 2,
                    Rarity = 2,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 7 }
                    }
                });
            }

            // 快速格挡 x2：获得3点护盾（瞬策）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1003,
                    CardName = "快速格挡",
                    Description = "获得3点护盾",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.防御,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.获得护盾, Value = 3 }
                    }
                });
            }

            // ═══ 定策牌 ═══

            // 蓄力斩 x3：造成8点伤害（定策，堆叠2层结算）
            for (int i = 0; i < 3; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2001,
                    CardName = "蓄力斩",
                    Description = "造成8点伤害",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.伤害,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 2,
                    Rarity = 2,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 8 }
                    }
                });
            }

            // 铁壁 x2：获得5点护盾（定策，堆叠1层结算）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2002,
                    CardName = "铁壁",
                    Description = "获得5点护盾",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.防御,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.获得护盾, Value = 5 }
                    }
                });
            }

            // 生命回复 x2：回复5点生命（定策，堆叠3层结算）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2003,
                    CardName = "生命回复",
                    Description = "回复5点生命",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.资源,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.回复生命, Value = 5 }
                    }
                });
            }

            // 见招拆招 x1：反制敌方首张伤害牌（定策反制，本回合锁定，下回合堆叠0层触发）
            deck.Add(new CardConfig
            {
                CardId = 2004,
                CardName = "见招拆招",
                Description = "反制敌方下回合的首张伤害牌",
                TrackType = CardTrackType.定策牌,
                SubType = CardSubType.反制,
                TargetType = CardTargetType.SingleEnemy,
                EnergyCost = 1,
                Rarity = 2,
                Effects = new List<CardEffect>
                {
                    new CardEffect { EffectType = EffectType.反制首张伤害牌, Value = 1 }
                }
            });

            // ═══ 多效果卡牌示例 ═══

            // 铁斩波 x1：获得4点护甲 + 造成5点伤害（多子类型，堆叠1层+堆叠2层分别结算）
            deck.Add(new CardConfig
            {
                CardId = 2005,
                CardName = "铁斩波",
                Description = "获得4点护甲，造成5点伤害",
                TrackType = CardTrackType.定策牌,
                SubType = CardSubType.伤害 | CardSubType.防御, // 多类型：伤害+防御
                TargetType = CardTargetType.SingleEnemy,
                EnergyCost = 2,
                Rarity = 2,
                Effects = new List<CardEffect>
                {
                    new CardEffect { EffectType = EffectType.获得护甲, Value = 4 }, // 堆叠1层
                    new CardEffect { EffectType = EffectType.造成伤害, Value = 5 }  // 堆叠2层
                }
            });

            return deck;
        }
    }
}
