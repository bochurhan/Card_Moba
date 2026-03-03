using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Settlement;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.RoundStateMachine
{
    /// <summary>
    /// 回合管理器 —— 管理一整局对战的生命周期。
    /// 
    /// 单回合完整流程（对照设计文档 §4.2 七阶段）：
    ///   1. 回合开始结算期：抽牌、能量回满、buff/debuff结算
    ///   2. 同步操作窗口期：玩家出牌（PlayCard / CommitPlanCard）
    ///   3. 瞬策牌实时结算环节：瞬策牌打出即结算（已内嵌在操作期）
    ///   4. 指令最终锁定期：锁定所有定策牌，非法指令作废
    ///   5. 定策牌统一结算期：按优先级同步结算所有定策牌
    ///   6. 濒死判定期：统一执行死亡判定
    ///   7. 回合结束期：强制弃光手牌、清空能量、清理临时效果
    /// 
    /// 使用方式：
    ///   var manager = new RoundManager();
    ///   var ctx = manager.InitBattle(player1Cards, player2Cards);
    ///   // 玩家操作...
    ///   manager.PlayCard(ctx, playerId, cardIndex, targetPlayerId);
    ///   manager.CommitPlanCard(ctx, playerId, cardIndex, targetPlayerId);
    ///   manager.EndRound(ctx); // 阶段4~7
    ///   // 读取日志后...
    ///   manager.BeginNextRound(ctx); // 下一回合阶段1
    /// </summary>
    public class RoundManager
    {
        private readonly SettlementEngine _settlement = new SettlementEngine();

        /// <summary>每回合固定抽牌数（数值待定，暂设为5，即每回合摸满一手新牌）</summary>
        private const int DrawPerRound = 5;

        /// <summary>手牌上限（抽牌时超出上限则不抽）</summary>
        private const int MaxHandSize = 10;

        // ── 阶段状态 ──

        /// <summary>当前回合所处阶段（由服务端/Host推进，客户端只读）</summary>
        public RoundPhase CurrentPhase { get; private set; } = RoundPhase.RoundStartSettle;

        /// <summary>当前阶段开始时间戳（UTC毫秒，由推进方写入，用于客户端校正）</summary>
        public long PhaseStartTimestampMs { get; private set; }

        /// <summary>当前阶段时长（毫秒），从 RoundPhaseConfig 读取</summary>
        public int PhaseDurationMs => RoundPhaseConfig.GetDurationMs(CurrentPhase);

        /// <summary>当前是否处于允许玩家操作的阶段</summary>
        public bool IsOperationAllowed => RoundPhaseConfig.IsPlayerActionAllowed(CurrentPhase);

        // ── 初始化 ──

        /// <summary>
        /// 初始化一场1v1对战，返回完整的战斗上下文。
        /// </summary>
        /// <param name="player1Id">玩家1的ID</param>
        /// <param name="player2Id">玩家2的ID</param>
        /// <param name="player1Deck">玩家1的卡组</param>
        /// <param name="player2Deck">玩家2的卡组</param>
        /// <param name="startHandSize">初始手牌数量（默认5张）</param>
        /// <param name="maxHp">初始最大生命值（默认30）</param>
        /// <param name="energyPerRound">每回合能量恢复量（默认3）</param>
        /// <returns>初始化完毕的战斗上下文</returns>
        public BattleContext InitBattle(
            string player1Id,
            string player2Id,
            List<CardConfig> player1Deck,
            List<CardConfig> player2Deck,
            int startHandSize = 5,
            int maxHp = 30,
            int energyPerRound = 3)
        {
            // 确保 Handler 注册中心已初始化
            Settlement.Handlers.HandlerRegistry.Initialize();

            BattleContext ctx = new BattleContext();

            // 创建玩家1（队伍1）
            PlayerBattleState p1 = new PlayerBattleState
            {
                PlayerId = player1Id,
                PlayerName = "玩家1",
                TeamId = 1,
                Hp = maxHp,
                MaxHp = maxHp,
                Energy = energyPerRound,
                EnergyPerRound = energyPerRound,
            };

            // 创建玩家2（队伍2）
            PlayerBattleState p2 = new PlayerBattleState
            {
                PlayerId = player2Id,
                PlayerName = "玩家2",
                TeamId = 2,
                Hp = maxHp,
                MaxHp = maxHp,
                Energy = energyPerRound,
                EnergyPerRound = energyPerRound,
            };

            // 使用 RegisterPlayer 注册玩家，同步构建 R-06 快速查找字典
            ctx.RegisterPlayer(p1);
            ctx.RegisterPlayer(p2);

            // 将 CardConfig 列表包装为 CardInstance（每张牌分配唯一 InstanceId）
            foreach (var config in player1Deck)
                p1.Deck.Add(ctx.CreateCardInstance(player1Id, config));
            foreach (var config in player2Deck)
                p2.Deck.Add(ctx.CreateCardInstance(player2Id, config));

            // 开局前用种子随机数洗牌，保证确定性但顺序不固定
            ctx.Random.Shuffle(p1.Deck);
            ctx.Random.Shuffle(p2.Deck);

            // 各摸初始手牌
            DrawCards(p1, startHandSize, ctx);
            DrawCards(p2, startHandSize, ctx);

            ctx.RoundLog.Add("[RoundManager] === 对局开始 ===");
            ctx.RoundLog.Add($"[RoundManager] 玩家1 HP:{p1.Hp} 能量:{p1.Energy} 手牌:{p1.Hand.Count}张");
            ctx.RoundLog.Add($"[RoundManager] 玩家2 HP:{p2.Hp} 能量:{p2.Energy} 手牌:{p2.Hand.Count}张");

            // 初始化后直接进入操作窗口期，允许玩家出牌
            CurrentPhase = RoundPhase.OperationWindow;

            return ctx;
        }

        // ── 玩家操作 ──

        /// <summary>
        /// 打出一张瞬策牌（立即结算）。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="playerId">出牌玩家ID</param>
        /// <param name="handIndex">手牌中的索引位置</param>
        /// <param name="targetPlayerId">目标玩家ID</param>
        /// <returns>操作结果描述（成功/失败原因）</returns>
        public string PlayCard(BattleContext ctx, string playerId, int handIndex, string targetPlayerId)
        {
            // ── 校验 ──
            PlayerBattleState? player = ctx.GetPlayer(playerId);
            if (player == null) return "错误：玩家不存在";
            if (!IsOperationAllowed) return $"错误：当前阶段（{CurrentPhase}）不允许操作";
            if (!player.IsAlive) return "错误：玩家已阵亡";
            if (player.IsLocked) return "错误：已锁定操作，不能再出牌";
            if (handIndex < 0 || handIndex >= player.Hand.Count) return "错误：无效的手牌索引";

            CardInstance inst = player.Hand[handIndex];
            CardConfig card = inst.Config;
            if (card.TrackType != CardTrackType.Instant) return "错误：这不是瞬策牌，请使用提交定策牌";
            if (player.Energy < card.EnergyCost) return $"错误：能量不足（需要{card.EnergyCost}，当前{player.Energy}）";

            // ── 执行 ──
            player.Energy -= card.EnergyCost;
            player.Hand.RemoveAt(handIndex);
            player.DiscardPile.Add(inst);

            // 创建 PlayedCard 用于结算（RuntimeId 复用 InstanceId，保证可追溯）
            PlayedCard playedCard = inst.ToPlayedCard();
            playedCard.SourcePlayerId = playerId;
            playedCard.RawTargetGroup = new List<string> { targetPlayerId };

            // 瞬策牌：立即结算
            _settlement.ResolveInstantCard(ctx, playedCard);

            // 检查游戏结束
            CheckGameOver(ctx);

            return $"成功：打出瞬策牌「{card.CardName}」";
        }

        /// <summary>
        /// 提交一张定策牌（暗置，回合末统一结算）。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="playerId">出牌玩家ID</param>
        /// <param name="handIndex">手牌中的索引位置</param>
        /// <param name="targetPlayerId">目标玩家ID</param>
        /// <returns>操作结果描述</returns>
        public string CommitPlanCard(BattleContext ctx, string playerId, int handIndex, string targetPlayerId)
        {
            // ── 校验 ──
            PlayerBattleState? player = ctx.GetPlayer(playerId);
            if (player == null) return "错误：玩家不存在";
            if (!player.IsAlive) return "错误：玩家已阵亡";
            if (player.IsLocked) return "错误：已锁定操作，不能再出牌";
            if (handIndex < 0 || handIndex >= player.Hand.Count) return "错误：无效的手牌索引";

            CardInstance inst2 = player.Hand[handIndex];
            CardConfig card2   = inst2.Config;
            if (card2.TrackType != CardTrackType.Plan) return "错误：这不是定策牌，请使用打出瞬策牌";
            if (player.Energy < card2.EnergyCost) return $"错误：能量不足（需要{card2.EnergyCost}，当前{player.Energy}）";

            // ── 执行 ──
            player.Energy -= card2.EnergyCost;
            player.Hand.RemoveAt(handIndex);
            player.DiscardPile.Add(inst2);

            // 创建 PlayedCard 用于定策牌结算（RuntimeId 复用 InstanceId）
            PlayedCard playedCard = inst2.ToPlayedCard();
            playedCard.SourcePlayerId = playerId;
            playedCard.RawTargetGroup = new List<string> { targetPlayerId };

            // 定策牌：加入待结算队列，回合末统一结算
            ctx.PendingPlanCards.Add(playedCard);
            ctx.RoundLog.Add($"[RoundManager] 玩家{playerId}暗置了一张定策牌");

            return $"成功：提交定策牌「{card2.CardName}」（回合末结算）";
        }

        /// <summary>
        /// 玩家锁定操作（表示本回合不再出牌）。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        /// <param name="playerId">玩家ID</param>
        /// <returns>操作结果描述</returns>
        public string LockOperation(BattleContext ctx, string playerId)
        {
            PlayerBattleState? player = ctx.GetPlayer(playerId);
            if (player == null) return "错误：玩家不存在";
            if (player.IsLocked) return "已经锁定了";

            player.IsLocked = true;
            ctx.RoundLog.Add($"[RoundManager] 玩家{playerId}锁定了操作");

            return "已锁定操作";
        }

        // ── 回合流转 ──

        /// <summary>
        /// 结束当前回合（阶段4~7）：
        ///   4. 指令锁定期（由 LockOperation 处理，此处做兜底）
        ///   5. 定策牌统一结算期
        ///   6. 濒死判定期
        ///   7. 回合结束期（弃光手牌、清空能量、清理护盾等临时效果）
        /// 
        /// 注意：本方法不会清空 RoundLog，调用方应在读取完日志后手动调用 BeginNextRound()。
        /// 流程：EndRound() → 读取日志 → BeginNextRound()
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        public void EndRound(BattleContext ctx)
        {
            if (ctx.IsGameOver) return;

            // ── 阶段4：指令最终锁定期（兜底：确保所有玩家都被锁定） ──
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                ctx.Players[i].IsLocked = true;
            }

            // ── 阶段5：定策牌统一结算期 ──
            ctx.RoundLog.Add($"[RoundManager] -- 第{ctx.CurrentRound}回合 - 定策牌结算期 --");
            ctx.RoundLog.Add($"[RoundManager] 待结算定策牌数量：{ctx.PendingPlanCards.Count}张");

            _settlement.ResolvePlanCards(ctx);

            // 结算后打印各玩家状态
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                ctx.RoundLog.Add($"[RoundManager] 结算后 {p.PlayerName} HP:{p.Hp}/{p.MaxHp} 护盾:{p.Shield}");
            }

            // ── 阶段6：濒死判定期 ──
            ctx.RoundLog.Add($"[RoundManager] -- 第{ctx.CurrentRound}回合 - 濒死判定期 --");
            CheckGameOver(ctx);

            if (ctx.IsGameOver)
            {
                ctx.RoundLog.Add($"[RoundManager] 对局结束！胜者：{(ctx.WinnerTeamId == -1 ? "平局" : "队伍" + ctx.WinnerTeamId)}");
                return;
            }

            ctx.RoundLog.Add($"[RoundManager] 所有玩家存活，对局继续");

            // ── 阶段7：回合结束期 ──
            ctx.RoundLog.Add($"[RoundManager] -- 第{ctx.CurrentRound}回合 - 回合结束 --");

            // 7a. 强制弃光所有手牌（仅带「手牌保留」词条的卡牌可例外，原型阶段暂无该词条）
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                if (!p.IsAlive) continue;

                int discardCount = p.Hand.Count;
                // TODO: 未来在此处检查「手牌保留」词条，保留符合条件的牌
                p.DiscardPile.AddRange(p.Hand);
                p.Hand.Clear();
                ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 弃掉了{discardCount}张手牌");
            }

            // 7b. 清空剩余能量（仅带「能量保留」词条的卡牌/遗物可例外，原型阶段暂无）
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                if (!p.IsAlive) continue;

                if (p.Energy > 0)
                {
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 清空了剩余{p.Energy}点能量");
                    p.Energy = 0;
                }
            }

            // 7c. 清理临时效果（护盾属于临时效果，每回合结束清零）
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                if (!p.IsAlive) continue;

                if (p.Shield > 0)
                {
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 护盾消散（-{p.Shield}）");
                    p.Shield = 0;
                }
            }

            // 7d. Buff 持续时间衰减（含 NoDrawThisTurn 等回合型 Buff）
            ctx.OnRoundEnd();

            // 7e. 回合结束状态快照
            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                ctx.RoundLog.Add($"[RoundManager] 回合结束 {p.PlayerName} HP:{p.Hp}/{p.MaxHp} 手牌:{p.Hand.Count}张 牌库:{p.Deck.Count}张 弃牌堆:{p.DiscardPile.Count}张");
            }

            // 检查回合上限
            if (ctx.CurrentRound >= ctx.MaxRounds)
            {
                ctx.IsGameOver = true;
                DetermineWinnerByHp(ctx);
                return;
            }

            // 回合结束后切换到回合开始结算期，等待 BeginNextRound 推进
            CurrentPhase = RoundPhase.RoundStartSettle;
        }

        /// <summary>
        /// 开始下一回合（阶段1：回合开始结算期）：
        ///   清空上回合数据 → 回合数+1 → 能量回满 → 固定抽牌
        /// 
        /// 应在调用 EndRound() 并读取完日志后调用。
        /// 
        /// 按设计文档 §4.2 阶段1：
        ///   同步执行固定抽牌、能量回满至上限、换路惩罚能量扣减、
        ///   持续buff/debuff生效，校验对局胜负条件等
        /// 
        /// 符合《定策牌结算机制 V4.0》：
        ///   - 反制牌本回合锁定，下回合堆叠0层触发
        ///   - 回合开始时处理 Buff/Debuff 持续时间
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        public void BeginNextRound(BattleContext ctx)
        {
            if (ctx.IsGameOver) return;

            // 清空上回合数据（会自动将反制牌转移到 PendingCounterCards）
            ctx.ClearRoundData();

            // 推进回合数
            ctx.CurrentRound++;

            // ── 阶段1：回合开始结算期 ──
            ctx.RoundLog.Add($"[RoundManager] ================================");
            ctx.RoundLog.Add($"[RoundManager] -- 第{ctx.CurrentRound}回合 - 回合开始 --");

            // 调用 BattleContext 的回合开始处理（处理 Buff、控制效果等）
            ctx.OnRoundStart();

            // 检查是否有待触发的反制牌
            if (ctx.PendingCounterCards.Count > 0)
            {
                ctx.RoundLog.Add($"[RoundManager] 上回合有 {ctx.PendingCounterCards.Count} 张反制牌待触发");
            }

            for (int i = 0; i < ctx.Players.Count; i++)
            {
                PlayerBattleState p = ctx.Players[i];
                if (!p.IsAlive) continue;

                // 1a. 能量回满至上限
                p.Energy = p.EnergyPerRound;

                // 1b. TODO: 换路惩罚能量扣减（原型阶段暂不实现）

                // 1c. 打印 Buff/Debuff 状态
                if (p.Strength != 0)
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 力量:{p.Strength}");
                if (p.VulnerableStacks > 0)
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 易伤:{p.VulnerableStacks}层");
                if (p.WeakStacks > 0)
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 虚弱:{p.WeakStacks}层");
                if (p.IsSilenced)
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 沉默中");
                if (p.IsStunned)
                    ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 眩晕中");

                // 1d. 固定抽牌（回合结束弃光手牌后，这里重新摸满）
                int drawCount = DrawPerRound;
                DrawCards(p, drawCount, ctx);

                ctx.RoundLog.Add($"[RoundManager] {p.PlayerName} 能量回满:{p.Energy} 摸牌:{p.Hand.Count}张 牌库剩余:{p.Deck.Count}张 HP:{p.Hp}/{p.MaxHp}");
            }

            // 1e. TODO: 校验对局胜负条件（原型阶段在濒死判定期统一处理）

            // 回合开始结算完成，进入操作窗口期
            CurrentPhase = RoundPhase.OperationWindow;
        }

        // ── 辅助方法 ──

        /// <summary>
        /// 从牌库摸指定数量的牌到手牌。
        /// 
        /// 规则（对照设计文档 §5.4）：
        ///   - 牌库空时，将弃牌堆全部洗回牌库继续摸
        ///   - 手牌达到上限时，停止抽牌
        /// </summary>
        /// <param name="player">目标玩家</param>
        /// <param name="count">摸牌数量</param>
        /// <param name="ctx">战斗上下文（用于种子随机洗牌）</param>
        private void DrawCards(PlayerBattleState player, int count, BattleContext ctx = null)
        {
            for (int i = 0; i < count; i++)
            {
                // 手牌上限检查：达到上限则停止抽牌
                if (player.Hand.Count >= MaxHandSize)
                    break;

                // 牌库空了，把弃牌堆洗回牌库（参考杀戮尖塔洗牌机制）
                if (player.Deck.Count == 0)
                {
                    if (player.DiscardPile.Count == 0)
                        break; // 无牌可摸

                    player.Deck.AddRange(player.DiscardPile);
                    player.DiscardPile.Clear();
                    ctx.Random.Shuffle(player.Deck);
                }

                // 从牌库顶摸一张（CardInstance）
                CardInstance drawn = player.Deck[0];
                player.Deck.RemoveAt(0);
                player.Hand.Add(drawn);
            }
        }

        /// <summary>
        /// 检查是否有玩家阵亡，如果有则结束对局。
        /// 1v1 模式：根据玩家索引判定，玩家0 = 队伍1，玩家1 = 队伍2
        /// 3v3 模式：待扩展，需检查整队存活状态
        /// </summary>
        private void CheckGameOver(BattleContext ctx)
        {
            // 1v1 简化处理：假设 Players[0] 是队伍1，Players[1] 是队伍2
            if (ctx.Players.Count < 2) return;

            PlayerBattleState p1 = ctx.Players[0];
            PlayerBattleState p2 = ctx.Players[1];

            bool p1Alive = p1.IsAlive;
            bool p2Alive = p2.IsAlive;

            if (!p1Alive || !p2Alive)
            {
                ctx.IsGameOver = true;
                if (p1Alive && !p2Alive)
                {
                    ctx.WinnerTeamId = p1.TeamId;
                    ctx.RoundLog.Add($"[RoundManager] {p2.PlayerName}阵亡，{p1.PlayerName}获胜！");
                }
                else if (!p1Alive && p2Alive)
                {
                    ctx.WinnerTeamId = p2.TeamId;
                    ctx.RoundLog.Add($"[RoundManager] {p1.PlayerName}阵亡，{p2.PlayerName}获胜！");
                }
                else
                {
                    ctx.WinnerTeamId = -1; // 双方同时阵亡 = 平局
                    ctx.RoundLog.Add("[RoundManager] 双方同归于尽，平局！");
                }
            }
        }

        /// <summary>
        /// 回合上限到达时，比较双方HP判定胜负。
        /// </summary>
        private void DetermineWinnerByHp(BattleContext ctx)
        {
            if (ctx.Players.Count < 2) return;

            PlayerBattleState p1 = ctx.Players[0];
            PlayerBattleState p2 = ctx.Players[1];

            if (p1.Hp > p2.Hp)
            {
                ctx.WinnerTeamId = p1.TeamId;
                ctx.RoundLog.Add($"[RoundManager] 回合上限到达！{p1.PlayerName}(HP:{p1.Hp}) > {p2.PlayerName}(HP:{p2.Hp})，{p1.PlayerName}获胜！");
            }
            else if (p2.Hp > p1.Hp)
            {
                ctx.WinnerTeamId = p2.TeamId;
                ctx.RoundLog.Add($"[RoundManager] 回合上限到达！{p2.PlayerName}(HP:{p2.Hp}) > {p1.PlayerName}(HP:{p1.Hp})，{p2.PlayerName}获胜！");
            }
            else
            {
                ctx.WinnerTeamId = -1;
                ctx.RoundLog.Add($"[RoundManager] 回合上限到达！双方HP相同({p1.Hp})，平局！");
            }
        }
    }
}
