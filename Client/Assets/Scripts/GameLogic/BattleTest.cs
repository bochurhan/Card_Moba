using UnityEngine;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.RoundStateMachine;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 自动对战测试 —— 在 Unity Console 中模拟一场完整的1v1对战。
    /// 
    /// 使用方式：
    ///   1. 在 Unity 场景中创建一个空 GameObject
    ///   2. 把这个脚本拖上去
    ///   3. Play → 看 Console 输出
    /// 
    /// 这个脚本是临时测试用的，验证 BattleCore 逻辑无误后可以删除。
    /// </summary>
    public class BattleTest : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[BattleTest] ========== 开始自动对战测试 ==========\n");

            // 第一步：构造测试卡组
            List<CardConfig> deck1 = CreateTestDeck();
            List<CardConfig> deck2 = CreateTestDeck();

            // 第二步：初始化对局
            RoundManager roundManager = new RoundManager();
            BattleContext ctx = roundManager.InitBattle(deck1, deck2);

            // 打印初始化日志
            PrintAndClearLog(ctx);

            // 第三步：模拟多个回合的自动对战
            int maxTestRounds = 10;
            for (int round = 0; round < maxTestRounds; round++)
            {
                if (ctx.IsGameOver) break;

                // ── 阶段1：回合开始（第1回合在 InitBattle 里已处理） ──
                if (round > 0)
                {
                    roundManager.BeginNextRound(ctx);
                    PrintAndClearLog(ctx);
                }

                // ── 阶段2：操作期 ──
                Debug.Log($"[BattleTest] ── 第{ctx.CurrentRound}回合 · 操作期 ──");
                PrintPlayerHands(ctx);

                // 模拟玩家1的操作
                Debug.Log($"[BattleTest] 玩家1操作：");
                SimulatePlayerTurn(roundManager, ctx, 1);

                // 打印瞬策牌结算日志
                PrintAndClearLog(ctx);

                if (ctx.IsGameOver) break;

                // 模拟玩家2的操作
                Debug.Log($"[BattleTest] 玩家2操作：");
                SimulatePlayerTurn(roundManager, ctx, 2);

                // 打印瞬策牌结算日志
                PrintAndClearLog(ctx);

                if (ctx.IsGameOver) break;

                // ── 阶段3：指令锁定期 ──
                Debug.Log($"[BattleTest] ── 第{ctx.CurrentRound}回合 · 指令锁定期 ──");
                roundManager.LockOperation(ctx, 1);
                roundManager.LockOperation(ctx, 2);
                PrintAndClearLog(ctx);

                // 操作期结束时打印状态快照
                Debug.Log($"[BattleTest] 锁定后状态：");
                for (int i = 0; i < ctx.Players.Count; i++)
                {
                    PlayerBattleState p = ctx.Players[i];
                    Debug.Log($"[BattleTest]   {p.PlayerName} HP:{p.Hp}/{p.MaxHp} 护盾:{p.Shield} 能量剩余:{p.Energy} 手牌:{p.Hand.Count}张");
                }
                Debug.Log($"[BattleTest]   待结算定策牌：{ctx.PendingPlanActions.Count}张");

                // ── 阶段4+5：回合结算（定策牌结算 + 濒死判定） ──
                roundManager.EndRound(ctx);

                // 打印结算期日志（现在不会被清空了！）
                PrintAndClearLog(ctx);
            }

            // ── 打印最终结果 ──
            Debug.Log("\n[BattleTest] ========== 对局结束 ==========");
            if (ctx.IsGameOver)
            {
                if (ctx.WinnerPlayerId == -1)
                    Debug.Log("[BattleTest] 🏳️ 结果：平局！");
                else
                    Debug.Log($"[BattleTest] 🏆 结果：玩家{ctx.WinnerPlayerId}获胜！");
            }
            else
            {
                Debug.Log($"[BattleTest] ⏱️ 测试回合数已达上限（{maxTestRounds}回合），对局未结束");
            }

            // 打印双方最终状态
            Debug.Log("[BattleTest] ── 最终状态 ──");
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                Debug.Log($"[BattleTest]   {p.PlayerName} HP:{p.Hp}/{p.MaxHp} 护盾:{p.Shield} 存活:{p.IsAlive} 手牌:{p.Hand.Count}张 牌库:{p.Deck.Count}张 弃牌堆:{p.DiscardPile.Count}张");
            }
        }

        /// <summary>
        /// 模拟一个玩家的回合操作：简单AI，尽量把能量用完。
        /// 
        /// 因为每回合结束手牌全弃，所以策略是尽量多出牌，不浪费手牌和能量。
        /// 瞬策牌直接打出，定策牌提交暗置。
        /// </summary>
        private void SimulatePlayerTurn(RoundManager roundManager, BattleContext ctx, int playerId)
        {
            PlayerBattleState? player = ctx.GetPlayer(playerId);
            if (player == null || !player.IsAlive) return;

            int opponentId = (playerId == 1) ? 2 : 1;

            // 策略：尽量把能量用完（反正回合结束手牌和能量都会清空）
            // 需要从后往前遍历，因为出牌会改变 Hand 的索引
            bool playedAny = true;
            while (playedAny && player.Energy > 0 && player.Hand.Count > 0)
            {
                playedAny = false;
                for (int i = player.Hand.Count - 1; i >= 0; i--)
                {
                    if (player.Energy <= 0) break;

                    CardConfig card = player.Hand[i];
                    if (player.Energy < card.EnergyCost) continue; // 这张牌太贵，跳过

                    string result;
                    if (card.TrackType == CardTrackType.瞬策牌)
                    {
                        int targetId = GetTargetForCard(card, playerId, opponentId);
                        result = roundManager.PlayCard(ctx, playerId, i, targetId);
                    }
                    else
                    {
                        int targetId = GetTargetForCard(card, playerId, opponentId);
                        result = roundManager.CommitPlanCard(ctx, playerId, i, targetId);
                    }

                    Debug.Log($"[BattleTest]   玩家{playerId} → {result}");
                    playedAny = true;

                    if (ctx.IsGameOver) return;
                    break; // 每次出完一张重新从后往前扫，因为索引变了
                }
            }

            if (player.Hand.Count > 0)
            {
                Debug.Log($"[BattleTest]   玩家{playerId} 剩余{player.Hand.Count}张手牌未出（能量不足或无合适牌）");
            }
        }

        /// <summary>
        /// 根据卡牌的目标类型，决定应该指向谁。
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
        /// 将 BattleContext 的日志输出到 Unity Console，然后清空日志。
        /// 这样每次打印的都是"自上次打印以来新增的日志"，不会重复。
        /// </summary>
        private void PrintAndClearLog(BattleContext ctx)
        {
            for (int i = 0; i < ctx.RoundLog.Count; i++)
            {
                Debug.Log($"  {ctx.RoundLog[i]}");
            }
            ctx.RoundLog.Clear();
        }

        /// <summary>
        /// 打印所有玩家的手牌信息（调试用）。
        /// </summary>
        private void PrintPlayerHands(BattleContext ctx)
        {
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                string handStr = "";
                for (int j = 0; j < p.Hand.Count; j++)
                {
                    CardConfig c = p.Hand[j];
                    string trackLabel = c.TrackType == CardTrackType.瞬策牌 ? "瞬" : "定";
                    handStr += $"[{j}]{c.CardName}({trackLabel},费{c.EnergyCost}) ";
                }
                Debug.Log($"[BattleTest]   {p.PlayerName} 能量:{p.Energy} 手牌: {handStr}");
            }
        }

        /// <summary>
        /// 创建一套测试卡组（15张牌，混合瞬策和定策）。
        /// 这是临时硬编码的测试数据，后续会从配置表读取。
        /// 
        /// 注意：已升级为新的「原子效果」架构，每张卡使用 Effects 列表。
        /// </summary>
        private List<CardConfig> CreateTestDeck()
        {
            List<CardConfig> deck = new List<CardConfig>();

            // === 瞬策牌（即时生效） ===

            // 3张 低费伤害牌
            for (int i = 0; i < 3; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1001,
                    CardName = "火球术",
                    Description = "向敌人投掷火球，造成4点伤害",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.伤害型,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 4 }
                    }
                });
            }

            // 2张 高费伤害牌
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1002,
                    CardName = "雷霆一击",
                    Description = "凝聚雷电之力，造成7点伤害",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.伤害型,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 2,
                    Rarity = 2,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 7 }
                    }
                });
            }

            // 2张 瞬策防御牌
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 1003,
                    CardName = "快速格挡",
                    Description = "迅速举盾，获得3点护盾",
                    TrackType = CardTrackType.瞬策牌,
                    SubType = CardSubType.防御型,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.获得护盾, Value = 3 }
                    }
                });
            }

            // === 定策牌（回合末统一结算） ===

            // 2张 定策伤害牌（费用高但伤害更高）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2001,
                    CardName = "蓄力斩",
                    Description = "蓄力后发出致命一击，造成8点伤害",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.伤害型,
                    TargetType = CardTargetType.SingleEnemy,
                    EnergyCost = 2,
                    Rarity = 2,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.造成伤害, Value = 8 }
                    }
                });
            }

            // 1张 多效果卡：铁斩（先获得护甲，再造成伤害）—— 验证四堆叠层机制
            deck.Add(new CardConfig
            {
                CardId = 2005,
                CardName = "铁斩",
                Description = "先获得3点护甲，然后造成6点伤害",
                TrackType = CardTrackType.定策牌,
                SubType = CardSubType.伤害型,  // 主类型为伤害型
                TargetType = CardTargetType.SingleEnemy,
                EnergyCost = 2,
                Rarity = 2,
                Effects = new List<CardEffect>
                {
                    // Layer 1 效果（防御/属性层）
                    new CardEffect { EffectType = EffectType.获得护甲, Value = 3 },
                    // Layer 2 效果（伤害层）
                    new CardEffect { EffectType = EffectType.造成伤害, Value = 6 }
                }
            });

            // 2张 定策防御牌
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2002,
                    CardName = "铁壁",
                    Description = "在回合结算时获得5点护盾",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.防御型,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.获得护盾, Value = 5 }
                    }
                });
            }

            // 2张 定策功能牌（治疗）
            for (int i = 0; i < 2; i++)
            {
                deck.Add(new CardConfig
                {
                    CardId = 2003,
                    CardName = "生命回复",
                    Description = "在回合结算时恢复5点生命值",
                    TrackType = CardTrackType.定策牌,
                    SubType = CardSubType.功能型,
                    TargetType = CardTargetType.Self,
                    EnergyCost = 1,
                    Rarity = 1,
                    Effects = new List<CardEffect>
                    {
                        new CardEffect { EffectType = EffectType.恢复生命, Value = 5 }
                    }
                });
            }

            // 1张 定策反制牌（本回合暗置，下回合堆叠0层触发）
            deck.Add(new CardConfig
            {
                CardId = 2004,
                CardName = "见招拆招",
                Description = "预判对手行动，下回合触发时展开4点反制护盾",
                TrackType = CardTrackType.定策牌,
                SubType = CardSubType.反制型,
                TargetType = CardTargetType.Self,
                EnergyCost = 1,
                Rarity = 2,
                Effects = new List<CardEffect>
                {
                    new CardEffect { EffectType = EffectType.反制护盾, Value = 4 }
                }
            });

            // 1张 增益牌（力量）
            deck.Add(new CardConfig
            {
                CardId = 2006,
                CardName = "战意激昂",
                Description = "获得2点力量（永久增加伤害）",
                TrackType = CardTrackType.定策牌,
                SubType = CardSubType.增益型,
                TargetType = CardTargetType.Self,
                EnergyCost = 1,
                Rarity = 2,
                Effects = new List<CardEffect>
                {
                    new CardEffect { EffectType = EffectType.获得力量, Value = 2 }
                }
            });

            return deck;
        }
    }
}
