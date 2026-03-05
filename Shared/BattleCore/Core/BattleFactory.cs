
#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Managers;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Buff;

namespace CardMoba.BattleCore.Core
{
    // ══════════════════════════════════════════════════════════════
    // 玩家初始化描述符
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 玩家参战描述符 —— BattleFactory.CreateBattle() 的输入单元。
    /// 描述一位玩家的初始状态（英雄属性 + 卡组配置）。
    /// </summary>
    public class PlayerSetupData
    {
        /// <summary>玩家唯一 ID（全局唯一字符串）</summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>英雄配置 ID（用于查询英雄属性，如 MaxHp / 初始护甲等）</summary>
        public string HeroConfigId { get; set; } = string.Empty;

        /// <summary>英雄初始 HP（若为 0 则由 HeroConfig 决定）</summary>
        public int InitialHp { get; set; } = 0;

        /// <summary>英雄最大 HP</summary>
        public int MaxHp { get; set; } = 30;

        /// <summary>英雄初始护甲</summary>
        public int InitialArmor { get; set; } = 0;

        /// <summary>
        /// 卡组配置列表，每项为（configId, count）。
        /// 例如：[("card_fireball", 2), ("card_shield", 3)]
        /// </summary>
        public List<(string configId, int count)> DeckConfig { get; set; }
            = new List<(string, int)>();
    }

    // ══════════════════════════════════════════════════════════════
    // 战斗创建结果
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// BattleFactory.CreateBattle() 的返回结果。
    /// 包含已初始化完成、可以直接调用 BeginRound() 的战斗对象。
    /// </summary>
    public class BattleCreateResult
    {
        /// <summary>战斗唯一上下文（唯一状态容器，结算代码通过 ctx 读写）</summary>
        public BattleContext Context { get; set; }

        /// <summary>回合管理器（外部通过此对象驱动战斗流程）</summary>
        public RoundManager RoundManager { get; set; }

        /// <summary>初始化阶段产生的日志（便于调试）</summary>
        public List<string> SetupLog { get; set; } = new List<string>();
    }

    // ══════════════════════════════════════════════════════════════
    // BattleFactory
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 战斗工厂（BattleFactory）—— 组装一局战斗所需全部对象的入口。
    ///
    /// 职责：
    ///   1. 创建并注入所有管理器（TriggerManager / BuffManager / CardManager / ValueModifierManager）
    ///   2. 创建 BattleContext，注册所有玩家 PlayerData 和英雄 Entity
    ///   3. 初始化卡组（调用 CardManager.InitBattleDeck）
    ///   4. 创建 RoundManager 并调用 InitBattle
    ///   5. 返回 BattleCreateResult（外部随即调用 BeginRound() 开始战斗）
    ///
    /// ⚠️ BattleFactory 本身无状态，可作为静态工厂使用。
    /// </summary>
    public class BattleFactory
    {
        // ── 外部依赖注入点 ─────────────────────────────────────────

        /// <summary>
        /// BuffConfig 查询委托（BattleCore 层不直接依赖配置加载器）。
        /// 调用方负责实现，通常是字典查询：buffId => BuffConfig。
        /// 若为 null，则 BuffManager 不支持 AddBuff（仅警告，不崩溃）。
        /// </summary>
        public Func<string, BuffConfig?>? BuffConfigProvider { get; set; }

        // ══════════════════════════════════════════════════════════
        // 主入口：创建一局战斗
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 创建一局战斗并完成全部初始化。
        /// </summary>
        /// <param name="battleId">战斗唯一 ID（可用 Guid.NewGuid().ToString() 生成）</param>
        /// <param name="randomSeed">随机数种子（C/S 必须使用相同种子保证一致性）</param>
        /// <param name="players">所有参战玩家的初始化描述符</param>
        /// <param name="eventBus">外部事件总线（UI/日志系统订阅）；传 null 则使用内置空实现</param>
        /// <returns>已初始化的战斗结果，可立即调用 result.RoundManager.BeginRound(result.Context)</returns>
        public BattleCreateResult CreateBattle(
            string battleId,
            int randomSeed,
            List<PlayerSetupData> players,
            IEventBus? eventBus = null)
        {
            var setupLog = new List<string>();

            // ── Step 1：创建各管理器 ──────────────────────────────
            var triggerManager       = new TriggerManager();
            var buffManager          = new BuffManager(BuffConfigProvider);
            var cardManager          = new CardManager();
            var valueModifierManager = new ValueModifierManager();
            var bus                  = eventBus ?? new NoOpEventBus();

            setupLog.Add($"[BattleFactory] 管理器创建完毕（battleId={battleId}, seed={randomSeed}）。");

            // ── Step 2：创建 BattleContext ────────────────────────
            var ctx = new BattleContext(
                battleId,
                randomSeed,
                bus,
                triggerManager,
                cardManager,
                buffManager,
                valueModifierManager);

            // ── Step 3：注册玩家数据 ──────────────────────────────
            foreach (var setup in players)
            {
                if (string.IsNullOrEmpty(setup.PlayerId))
                    throw new ArgumentException("[BattleFactory] PlayerSetupData.PlayerId 不能为空。");

                // 创建英雄实体
                int hp    = setup.InitialHp > 0 ? setup.InitialHp : setup.MaxHp;
                var hero  = new Entity
                {
                    EntityId      = $"hero_{setup.PlayerId}",
                    OwnerPlayerId = setup.PlayerId,
                    Hp            = hp,
                    MaxHp         = setup.MaxHp,
                    Armor         = setup.InitialArmor,
                    Shield        = 0,
                };

                // 创建玩家数据
                var playerData = new PlayerData
                {
                    PlayerId   = setup.PlayerId,
                    HeroEntity = hero,
                };

                ctx.RegisterPlayer(playerData);
                setupLog.Add($"[BattleFactory] 注册玩家 {setup.PlayerId}（英雄 HP={hp}/{setup.MaxHp}，初始护甲={setup.InitialArmor}）。");

                // ── Step 4：初始化卡组 ────────────────────────────
                if (setup.DeckConfig.Count > 0)
                {
                    cardManager.InitBattleDeck(ctx, setup.PlayerId, setup.DeckConfig);
                    setupLog.Add($"[BattleFactory] 玩家 {setup.PlayerId} 卡组初始化完毕（{setup.DeckConfig.Count} 种牌型）。");
                }
                else
                {
                    setupLog.Add($"[BattleFactory] ⚠️ 玩家 {setup.PlayerId} 未配置卡组，卡组为空。");
                }
            }

            // ── Step 5：创建 RoundManager 并完成战斗初始化 ────────
            var roundManager = new RoundManager();
            roundManager.InitBattle(ctx);

            // 将 setup 日志写入 ctx（便于 RoundManager 归档）
            foreach (var log in setupLog)
                ctx.RoundLog.Add(log);

            setupLog.Add("[BattleFactory] 战斗初始化完成，可调用 BeginRound() 开始第一回合。");

            return new BattleCreateResult
            {
                Context      = ctx,
                RoundManager = roundManager,
                SetupLog     = setupLog,
            };
        }
    }

    // ══════════════════════════════════════════════════════════════
    // NoOpEventBus —— 空实现（无 UI 端时使用）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 事件总线的空实现，用于单元测试或无 UI 的纯服务端场景。
    /// Publish 什么都不做，Subscribe 直接忽略。
    /// </summary>
    public class NoOpEventBus : IEventBus
    {
        public void Publish<T>(T battleEvent) where T : BattleEventBase { }
        public void Subscribe<T>(Action<T> handler) where T : BattleEventBase { }
        public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase { }
    }
}
