#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 战斗流程管理器（V2），连接 UI 层和 BattleCore V2。
    ///
    /// 职责：
    ///   - 通过 BattleFactory 创建并驱动一局战斗的完整生命周期
    ///   - 提供 UI 层调用的操作接口，例如出牌和结束回合
    ///   - 内置简单 AI 控制对手行为
    ///   - 通过 C# 事件通知 UI 层刷新显示
    ///
    /// 架构：
    ///   BattleUIManager (Presentation)
    ///     -> BattleGameManager (GameLogic)
    ///       -> BattleFactory / RoundManager (BattleCore V2)
    /// </summary>
    public class BattleGameManager
    {
        // ══════════════════════════════════════════════════════════════════════
        // UI 层订阅事件
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>对局状态发生变化时触发（HP / 护盾 / 手牌等变化）</summary>
        public event Action OnStateChanged;

        /// <summary>新增日志消息时触发，参数为可带 TMP RichText 标签的字符串。</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// 对局结束时触发。
        /// 参数 winnerCode：1 = 玩家胜，2 = AI 胜，-1 = 平局。
        /// </summary>
        public event Action<int> OnGameOver;

        /// <summary>回合阶段切换时触发，用于更新 phaseText 和驱动计时器。</summary>
        public event Action<string> OnPhaseChanged;

        // ══════════════════════════════════════════════════════════════════════
        // 玩家 ID 常量
        // ══════════════════════════════════════════════════════════════════════

        public const string HumanPlayerId = "player1";
        public const string AiPlayerId    = "player2";

        // ══════════════════════════════════════════════════════════════════════
        // V2 核心对象
        // ══════════════════════════════════════════════════════════════════════

        private BattleContext _ctx;
        private RoundManager  _roundManager;

        // configId -> CardConfig 映射，在 BattleFactory 初始化后由 BuildCardConfigMap 填充。
        private readonly Dictionary<string, CardConfig> _cardConfigMap
            = new Dictionary<string, CardConfig>();

        // ══════════════════════════════════════════════════════════════════════
        // 公开状态属性（UI 层只读）
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>当前 BattleContext。</summary>
        public BattleContext Context => _ctx;

        /// <summary>是否处于玩家操作阶段</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>对局是否已结束。</summary>
        public bool IsGameOver => _roundManager?.IsBattleOver ?? false;

        /// <summary>当前回合数。</summary>
        public int CurrentRound => _roundManager?.CurrentRound ?? 0;

        // ══════════════════════════════════════════════════════════════════════
        // 战斗启动
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 开始一场新的 1v1 对战，使用默认战士测试卡组。
        /// </summary>
        public void StartBattle()
        {
            StartBattleWithDeck(DefaultWarriorDeckIds, DefaultWarriorDeckIds);
        }

        /// <summary>
        /// 使用指定卡牌 ID 列表开始对战。
        /// </summary>
        public void StartBattleWithDeck(int[] playerDeckIds, int[] aiDeckIds)
        {
            EnsureConfigLoaded();
            _cardConfigMap.Clear();

            // ── 构建 configId 映射表，供后续按 instanceId 查找配置 ──
            BuildCardConfigMap();

            // ── 构建 DeckConfig ────────────────────────────────────
            var humanDeck = BuildDeckConfig(playerDeckIds);
            var aiDeck    = BuildDeckConfig(aiDeckIds);

            // ── 创建 EventBus 适配器 ──────────────────────────────
            var eventBus = new InternalEventBus(this);

            // ── 通过 BattleFactory 创建战斗 ──────────────────────
            var factory = new BattleFactory
            {
                BuffConfigProvider = ResolveRuntimeBuffConfig,
                CardDefinitionProvider = configId =>
                {
                    if (!_cardConfigMap.TryGetValue(configId, out var cardConfig))
                        return null;

                    string defaultTarget = CardConfigToEffectAdapter.CardTargetTypeToString(cardConfig.TargetType);
                    return new BattleCardDefinition
                    {
                        ConfigId = configId,
                        IsExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust),
                        IsStatCard = cardConfig.Tags.HasFlag(CardTag.Status),
                        Effects = CardConfigToEffectAdapter.ConvertEffects(cardConfig, defaultTarget),
                    };
                },
            };
            var result  = factory.CreateBattle(
                battleId:   "local-battle",
                randomSeed: 42,
                players: new List<PlayerSetupData>
                {
                    new PlayerSetupData
                    {
                        PlayerId     = HumanPlayerId,
                        MaxHp        = 30,
                        InitialHp    = 30,
                        InitialArmor = 0,
                        DeckConfig   = humanDeck,
                    },
                    new PlayerSetupData
                    {
                        PlayerId     = AiPlayerId,
                        MaxHp        = 30,
                        InitialHp    = 30,
                        InitialArmor = 0,
                        DeckConfig   = aiDeck,
                    },
                },
                eventBus: eventBus);

            _ctx          = result.Context;
            _roundManager = result.RoundManager;

            // 输出 setup 日志
            foreach (var log in result.SetupLog)
                OnLogMessage?.Invoke(ColorizeLog(log));

            // ── 开始第一回合 ──────────────────────────────────────
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 你的操作");
            OnStateChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 玩家操作接口
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 玩家打出一张瞬策牌，立即结算。
        /// </summary>
        /// <param name="handIndex">在人类玩家手牌列表中的位置。</param>
        /// <returns>操作结果描述</returns>
        public string PlayerPlayInstantCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true);
        }

        /// <summary>
        /// 玩家提交一张定策牌，等待 EndRound 结算。
        /// </summary>
        public string PlayerCommitPlanCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "当前无法操作";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false);
        }

        /// <summary>
        /// 玩家结束回合：AI 操作 -> 定策结算 -> 下一回合开始。
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || IsGameOver) return;

            IsPlayerTurn = false;

            // ── AI 操作阶段 ────────────────────────────────────────
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 对手操作...");
            OnStateChanged?.Invoke();
            ExecuteAiTurn();
            FlushLogs();

            if (IsGameOver) { NotifyGameOver(); return; }

            // ── 定策五层结算 ───────────────────────────────────────
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 结算中...");
            _roundManager.EndRound(_ctx);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (IsGameOver) { NotifyGameOver(); return; }

            // ── 下一回合 ───────────────────────────────────────────
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"第 {_roundManager.CurrentRound} 回合 · 你的操作");
            OnStateChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 数据访问（UI 层调用）
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>获取人类玩家数据（V2 PlayerData）。</summary>
        public PlayerData GetHumanPlayer() => _ctx?.GetPlayer(HumanPlayerId);

        /// <summary>获取 AI 玩家数据（V2 PlayerData）。</summary>
        public PlayerData GetAiPlayer() => _ctx?.GetPlayer(AiPlayerId);

        /// <summary>
        /// 获取人类玩家手牌（含对应 CardConfig 显示信息）。
        /// 返回列表顺序与 PlayerData.Hand 中的 BattleCard 顺序一致。
        /// </summary>
        public List<(BattleCard Card, CardConfig Config)> GetHumanHandCards()
        {
            var list   = new List<(BattleCard, CardConfig)>();
            var player = _ctx?.GetPlayer(HumanPlayerId);
            if (player == null) return list;

            foreach (var bc in player.GetCardsInZone(CardZone.Hand))
            {
                _cardConfigMap.TryGetValue(bc.ConfigId, out var cfg);
                list.Add((bc, cfg));
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 调试
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>打印战场完整状态快照，通过 OnLogMessage 推送给 UI。</summary>
        public void PrintBattleStatus()
        {
            if (_ctx == null)
            {
                OnLogMessage?.Invoke("<color=#ff4444>[状态快照] BattleContext 为空，对局尚未开始。</color>");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#ffffff>╔══════════ [战场状态快照] ══════════╗</color>");
            sb.AppendLine($"<color=#aaaaaa>  第 {_roundManager.CurrentRound} 回合</color>");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(HumanPlayerId), "我方");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(AiPlayerId),    "对手");
            sb.AppendLine("<color=#ffffff>╚══════════════════════════════════════╝</color>");

            foreach (var line in sb.ToString().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    OnLogMessage?.Invoke(line.TrimEnd('\r'));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：出牌核心逻辑
        // ══════════════════════════════════════════════════════════════════════

        private string PlayCardInternal(string playerId, int handIndex, bool instant)
        {
            var player = _ctx.GetPlayer(playerId);
            if (player == null) return "玩家不存在";

            var hand = player.GetCardsInZone(CardZone.Hand);
            if (handIndex < 0 || handIndex >= hand.Count)
                return $"手牌索引越界（{handIndex}/{hand.Count}）";

            var battleCard = hand[handIndex];
            if (!_cardConfigMap.TryGetValue(battleCard.ConfigId, out var cardConfig))
                return $"找不到卡牌配置 configId={battleCard.ConfigId}";

            if (!_roundManager.CanPlayCard(_ctx, playerId, battleCard.ConfigId, out var playRestrictionReason))
            {
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {playRestrictionReason}</color>");
                return playRestrictionReason;
            }

            // ── 能量校验 ──────────────────────────────────────────────
            int cost = cardConfig.EnergyCost;
            if (player.Energy < cost)
            {
                string reason = $"能量不足（当前 {player.Energy}，需要 {cost}）";
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            // ── 扣除能量 ─────────────────────────────────────────
            player.Energy -= cost;

            string cardName = cardConfig.CardName;

            // ── 出牌 ─────────────────────────────────────────────────
            if (instant)
            {
                // 瞬策牌：先移出手牌区，再结算
                _roundManager.PlayInstantCard(_ctx, playerId, battleCard.InstanceId);
                FlushLogs();
                OnLogMessage?.Invoke($"<color=#aaffaa>{(playerId == HumanPlayerId ? "你" : "对手")} 打出瞬策牌【{cardName}】（花费 {cost} 点能量）</color>");
            }
            else
            {
                // 定策牌：移入策略区，等待 EndRound 统一结算
                _roundManager.CommitPlanCard(_ctx, new CommittedPlanCard
                {
                    PlayerId       = playerId,
                    CardInstanceId = battleCard.InstanceId,
                });
                FlushLogs();
                OnLogMessage?.Invoke($"<color=#aaddff>{(playerId == HumanPlayerId ? "你" : "对手")} 提交定策牌【{cardName}】（花费 {cost} 点能量）</color>");
            }

            OnStateChanged?.Invoke();
            if (IsGameOver) NotifyGameOver();
            return cardName;
        }

        /// <summary>
        /// 出牌后根据卡牌标签决定卡牌去向。
        /// Exhaust 标签表示从游戏中移除；普通牌进入弃牌堆。
        /// </summary>
        private void MoveCardAfterPlay(BattleCard battleCard, CardConfig cardConfig)
        {
            bool isExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust);
            if (isExhaust)
            {
                // 消耗牌：从 AllCards 中彻底移除
                var owner = _ctx.GetPlayer(battleCard.OwnerId);
                owner?.AllCards.Remove(battleCard);
                _ctx.RoundLog.Add($"[BattleGameManager] 卡牌【{cardConfig.CardName}】已消耗（Exhaust）。");
            }
            else
            {
                // 普通牌：移入弃牌堆
                _ctx.CardManager.MoveCard(_ctx, battleCard, CardZone.Discard);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：AI 逻辑
        // ══════════════════════════════════════════════════════════════════════

        private void ExecuteAiTurn()
        {
            var player = _ctx.GetPlayer(AiPlayerId);
            if (player == null || !player.HeroEntity.IsAlive) return;

            // 简单策略：把所有手牌都提交，测试用，不额外做复杂决策
            var hand = player.GetCardsInZone(CardZone.Hand);
            var snapshot = new List<BattleCard>(hand); // 防止遍历时列表变更

            foreach (var battleCard in snapshot)
            {
                if (!player.HeroEntity.IsAlive || IsGameOver) break;
                if (!_cardConfigMap.TryGetValue(battleCard.ConfigId, out var cfg)) continue;

                bool isInstant = cfg.TrackType == CardTrackType.Instant;
                PlayCardInternal(AiPlayerId, 0, isInstant);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：卡组构建
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 力量战测试卡组（13 张）。
        ///   打击 ×4 + 防御 ×3 + 观察弱点 ×2 + 飞剑回旋斩 ×2 + 战斗专注 ×1 + 突破极限 ×1
        /// </summary>
        /* Legacy V1 warrior demo deck removed from active use.
        {
            2001, 2001, 2001, 2001,   // 打击 ×4        (1费，定策，造成6伤害)
            2002, 2002, 2002,         // 防御 ×3        (1费，定策，获得护盾)
            2003, 2003,               // 观察弱点 ×2    (1费，定策，条件获得力量)
            2005, 2005,               // 飞剑回旋斩 ×2  (1费，定策，2段伤害)
            1001,                     // 战斗专注 ×1    (0费，瞬策，抽3张)
            1002,                     // 突破极限 ×1    (1费，瞬策，获得力量并消耗)
            2008, 2008,               // 愤怒 x2
            1001,                     // 战斗专注 ×1    (0费，瞬策，抽3张)
            1002,                     // 突破极限 ×1    (1费，瞬策，获得力量并消耗)
        };

        */
        private static readonly int[] DefaultWarriorDeckIds = new int[]
        {
            2001, 2001, 2001,         // 无情连打 x3
            1001, 1001, 1001,         // 持盾向前 x3
            1002, 1002,               // 锻体 x2
            1003, 1003,               // 放血 x2
            1004, 1004,               // 疲劳行军 x2
            1005,                     // 竭泽而渔 x1
            2002, 2002,               // 以血还血 x2
            2003, 2003,               // 鲜血护盾 x2
            2004,                     // 死亡收割 x1
            2005, 2005,               // 护盾猛击 x2
            2006,                     // 全力一击 x1
            2007, 2007,               // 撕裂 x2
            2008, 2008,               // 愤怒 x2
            2009, 2009,               // 回旋镖 x2
            2010, 2010,               // 痛击 x2
        };

        private static BuffConfig? ResolveRuntimeBuffConfig(string buffId)
        {
            return buffId switch
            {
                "strength" => new BuffConfig
                {
                    BuffId = "strength",
                    BuffName = "力量",
                    Description = "增加造成的伤害",
                    BuffType = BuffType.Strength,
                    IsBuff = true,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 0,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "weak" => new BuffConfig
                {
                    BuffId = "weak",
                    BuffName = "虚弱",
                    Description = "造成的伤害降低 25%",
                    BuffType = BuffType.Weak,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 1,
                    DefaultValue = 25,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "vulnerable" => new BuffConfig
                {
                    BuffId = "vulnerable",
                    BuffName = "易伤",
                    Description = "受到的伤害提高 50%",
                    BuffType = BuffType.Vulnerable,
                    IsBuff = false,
                    StackRule = BuffStackRule.StackValue,
                    MaxStacks = 99,
                    DefaultDuration = 1,
                    DefaultValue = 50,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_draw_this_turn" => new BuffConfig
                {
                    BuffId = "no_draw_this_turn",
                    BuffName = "本回合禁止抽牌",
                    Description = "本回合剩余时间内无法再抽牌",
                    BuffType = BuffType.NoDrawThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_damage_card_this_turn" => new BuffConfig
                {
                    BuffId = "no_damage_card_this_turn",
                    BuffName = "本回合禁止伤害牌",
                    Description = "本回合剩余时间内无法再打出伤害牌",
                    BuffType = BuffType.NoDamageCardThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "delayed_vulnerable_next_round" => new BuffConfig
                {
                    BuffId = "delayed_vulnerable_next_round",
                    BuffName = "下回合易伤",
                    Description = "下回合开始时，获得等值易伤",
                    BuffType = BuffType.DelayedVulnerableNextRound,
                    IsBuff = false,
                    StackRule = BuffStackRule.StackValue,
                    MaxStacks = 99,
                    DefaultDuration = 2,
                    DefaultValue = 50,
                    IsDispellable = true,
                    IsPurgeable = true,
                    IsHidden = true,
                },
                _ => null,
            };
        }

        private List<(string configId, int count)> BuildDeckConfig(int[] cardIds)
        {
            var countMap = new Dictionary<int, int>();
            foreach (var id in cardIds)
            {
                if (!countMap.ContainsKey(id)) countMap[id] = 0;
                countMap[id]++;
            }

            var deck = new List<(string, int)>();
            foreach (var kv in countMap)
            {
                if (CardConfigManager.Instance.GetCard(kv.Key) != null)
                    deck.Add((kv.Key.ToString(), kv.Value));
                else
                    OnLogMessage?.Invoke($"<color=#ffaa00>[警告] 卡牌配置不存在 {kv.Key}，已跳过</color>");
            }
            return deck;
        }

        /// <summary>
        /// 构建 configId（字符串形式 CardId）到 CardConfig 的映射表。
        /// 供 PlayCardInternal 在拿到 BattleCard.ConfigId 后快速查找配置。
        /// </summary>
        private void BuildCardConfigMap()
        {
            var all = CardConfigManager.Instance.AllCards;
            if (all == null) return;
            foreach (var kv in all)
                _cardConfigMap[kv.Key.ToString()] = kv.Value;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：辅助
        // ══════════════════════════════════════════════════════════════════════

        private void EnsureConfigLoaded()
        {
            if (!CardConfigManager.Instance.IsLoaded)
                CardConfigManager.Instance.LoadAll();
        }

        private void FlushLogs()
        {
            if (_ctx == null) return;
            foreach (var raw in _ctx.RoundLog)
                OnLogMessage?.Invoke(ColorizeLog(raw));
            _ctx.RoundLog.Clear();
        }

        private void NotifyGameOver()
        {
            string? winner = _roundManager?.WinnerId;
            int code = winner == null       ? -1
                     : winner == HumanPlayerId ? 1
                     : 2;
            OnGameOver?.Invoke(code);
        }

        private static string ColorizeLog(string log)
        {
            if (log.Contains("<color=")) return log;
            string lower = log.ToLower();

            if (lower.Contains("伤害") || lower.Contains("击中") || lower.Contains("扣除"))
                return $"<color=#ff8866>{log}</color>";
            if (lower.Contains("护盾") || lower.Contains("shield"))
                return $"<color=#66aaff>{log}</color>";
            if (lower.Contains("治疗") || lower.Contains("回血") || lower.Contains("恢复"))
                return $"<color=#66ee88>{log}</color>";
            if (lower.Contains("力量") || lower.Contains("buff") || lower.Contains("护甲"))
                return $"<color=#ffdd55>{log}</color>";
            if (lower.Contains("回合") && (log.Contains("══") || log.Contains("──")))
                return $"<color=#888888><size=85%>{log}</size></color>";

            return log;
        }

        private void AppendPlayerSnapshot(System.Text.StringBuilder sb, PlayerData? p, string label)
        {
            if (p == null) { sb.AppendLine($"  [{label}]: 数据不存在"); return; }

            var hero    = p.HeroEntity;
            var hand    = p.GetCardsInZone(CardZone.Hand);
            var deck    = p.GetCardsInZone(CardZone.Deck);
            var discard = p.GetCardsInZone(CardZone.Discard);

            string hpColor = hero.Hp <= hero.MaxHp / 3 ? "#ff4444"
                           : hero.Hp <= hero.MaxHp * 2 / 3 ? "#ffaa33"
                           : "#66ee88";

            sb.AppendLine($"  <color=#ddddff>[{label}]</color>");
            sb.AppendLine($"    HP    : <color={hpColor}>{hero.Hp}/{hero.MaxHp}</color>"
                + (hero.Shield > 0 ? $"   护盾: <color=#66aaff>{hero.Shield}</color>" : "")
                + (hero.Armor  > 0 ? $"   护甲: <color=#88ccff>{hero.Armor}</color>" : ""));
            sb.AppendLine($"    能量  : <color=#ffdd55>{p.Energy}/{p.MaxEnergy}</color>");
            sb.AppendLine($"    手牌  : {hand.Count}  |  牌库: {deck.Count}   弃牌: {discard.Count}");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 内部：EventBus 适配
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 V2 BattleCore 内部事件转发到 BattleGameManager 的 C# 事件和 UI 日志。
        /// </summary>
        private sealed class InternalEventBus : IEventBus
        {
            private readonly BattleGameManager _mgr;
            public InternalEventBus(BattleGameManager mgr) => _mgr = mgr;

            public void Subscribe<T>(Action<T> handler)   where T : BattleEventBase { }
            public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase { }

            public void Publish<T>(T evt) where T : BattleEventBase
            {
                switch (evt)
                {
                    case DamageDealtEvent dmg:
                        if (dmg.RealHpDamage > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#ff6666>[伤害] {dmg.SourceEntityId} -> {dmg.TargetEntityId} {dmg.RealHpDamage} 点"
                                + (dmg.ShieldAbsorbed > 0 ? $"（护盾吸收 {dmg.ShieldAbsorbed}）" : "")
                                + "</color>");

                        else if (dmg.ShieldAbsorbed > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#66aaff>[护盾] 吸收 {dmg.ShieldAbsorbed} 点伤害</color>");
                        break;

                    case HealEvent heal:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66ee88>[治疗] {heal.TargetEntityId} 恢复 {heal.RealHealAmount} 点生命</color>");
                        break;

                    case ShieldGainedEvent sg:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66aaff>[护盾] {sg.TargetEntityId} 获得 {sg.ShieldAmount} 点护盾</color>");
                        break;

                    case RoundStartEvent rs:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 第 {rs.Round} 回合开始 ---</size></color>");
                        break;

                    case RoundEndEvent re:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 第 {re.Round} 回合结束 ---</size></color>");
                        break;

                    case PlayerDeathEvent death:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ff4444>[倒下] {death.PlayerId}</color>");
                        break;

                    case BattleEndEvent end:
                        _mgr.OnLogMessage?.Invoke(end.IsDraw
                            ? "<color=#ffdd55>[结束] 平局</color>"
                            : $"<color=#ffdd55>[结束] 胜者：{end.WinnerId}</color>");
                        break;
                }
            }
        }
    }
}
