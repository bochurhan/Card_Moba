
#pragma warning disable CS8632
#pragma warning disable CS8625

using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.Tests
{
    // ══════════════════════════════════════════════════════════════════════
    // BattleCore V2 集成测试
    //
    // 测试目标：验证从 BattleFactory 创建战斗 → BeginRound → 出牌 → EndRound
    // 的完整生命周期，覆盖以下场景：
    //   T-01  战斗初始化（工厂创建 + 玩家注册）
    //   T-02  瞬策伤害牌：造成伤害，HP 正确扣减
    //   T-03  瞬策治疗牌：HP 正确恢复
    //   T-04  护盾先于 HP 被扣减
    //   T-05  定策牌批量结算（两张伤害牌同回合）
    //   T-06  HP 归零触发战斗结束
    //   T-07  确定性随机：相同种子出牌结果一致
    // ══════════════════════════════════════════════════════════════════════

    public class BattleCoreIntegrationTests
    {
        // ──────────────────────────────────────────────────────────────────
        // 辅助方法：创建标准 2v1 测试战斗（P1 vs P2，各 30 HP）
        // ──────────────────────────────────────────────────────────────────

        private static BattleCreateResult CreateTestBattle(int seed = 42)
        {
            var factory = new BattleFactory();
            // BuffConfigProvider 留 null（测试中不使用 Buff）

            var players = new List<PlayerSetupData>
            {
                new PlayerSetupData
                {
                    PlayerId      = "P1",
                    MaxHp         = 30,
                    InitialHp     = 30,
                    InitialArmor  = 0,
                    DeckConfig    = new List<(string, int)>(), // 空卡组，手动出牌
                },
                new PlayerSetupData
                {
                    PlayerId      = "P2",
                    MaxHp         = 30,
                    InitialHp     = 30,
                    InitialArmor  = 0,
                    DeckConfig    = new List<(string, int)>(),
                },
            };

            return factory.CreateBattle("test-battle", seed, players);
        }

        /// <summary>构造一张 "固定数值" 伤害效果（字面量）</summary>
        private static EffectUnit MakeDamageEffect(int value, string targetType = "Enemy")
            => new EffectUnit
            {
                EffectId        = $"dmg_{value}",
                Type            = EffectType.Damage,
                TargetType      = targetType,
                ValueExpression = value.ToString(),
                Layer           = SettleLayer.Damage,
            };

        /// <summary>构造一张治疗效果</summary>
        private static EffectUnit MakeHealEffect(int value)
            => new EffectUnit
            {
                EffectId        = $"heal_{value}",
                Type            = EffectType.Heal,
                TargetType      = "Self",
                ValueExpression = value.ToString(),
                Layer           = SettleLayer.BuffSpecial,
            };

        /// <summary>构造一张护盾效果</summary>
        private static EffectUnit MakeShieldEffect(int value)
            => new EffectUnit
            {
                EffectId        = $"shield_{value}",
                Type            = EffectType.Shield,
                TargetType      = "Self",
                ValueExpression = value.ToString(),
                Layer           = SettleLayer.Defense,
            };

        // ══════════════════════════════════════════════════════════════════
        // T-01：战斗工厂初始化
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T01_BattleFactory_CreatesContext_WithTwoPlayers()
        {
            // Arrange & Act
            var result = CreateTestBattle();
            var ctx    = result.Context;

            // Assert
            ctx.Should().NotBeNull("战斗上下文不应为 null");
            ctx.AllPlayers.Should().ContainKey("P1", "P1 应已注册");
            ctx.AllPlayers.Should().ContainKey("P2", "P2 应已注册");

            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30, "P1 初始 HP=30");
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(30, "P2 初始 HP=30");

            result.RoundManager.CurrentRound.Should().Be(0, "战斗尚未开始，回合号为 0");
            result.RoundManager.IsBattleOver.Should().BeFalse("战斗应未结束");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-02：瞬策伤害牌 —— 正确扣减敌方 HP
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T02_InstantDamageCard_ReducesEnemyHp()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            // Act：P1 打出一张 8 点伤害瞬策牌，目标 Enemy = P2
            var effects  = new List<EffectUnit> { MakeDamageEffect(8) };
            rm.PlayInstantCard(ctx, "P1", "card_instant_01", effects);

            // Assert
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(22, "30 - 8 = 22");
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30, "P1 HP 不受影响");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-03：瞬策治疗牌 —— 正确恢复己方 HP（带上限限制）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T03_InstantHealCard_RestoresHp_CappedAtMaxHp()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            // 先用伤害把 P1 打到 20 HP
            rm.PlayInstantCard(ctx, "P2", "card_dmg", new List<EffectUnit> { MakeDamageEffect(10) });
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(20);

            // Act：P1 打出 15 点治疗
            rm.PlayInstantCard(ctx, "P1", "card_heal", new List<EffectUnit> { MakeHealEffect(15) });

            // Assert：30 - 10 + 15 = 35 但上限 30，应为 30
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(30, "治疗后 HP 不超过最大值");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-04：护盾先吸伤害，HP 不受影响；护盾耗尽后才扣 HP
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T04_Shield_AbsorbsDamage_BeforeHp()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            // P1 获得 5 点护盾
            rm.PlayInstantCard(ctx, "P1", "card_shield", new List<EffectUnit> { MakeShieldEffect(5) });
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(5, "护盾应为 5");

            // Act：P2 打 8 点伤害给 P1（5 护盾 + 3 HP）
            rm.PlayInstantCard(ctx, "P2", "card_dmg8", new List<EffectUnit> { MakeDamageEffect(8) });

            // Assert
            ctx.AllPlayers["P1"].HeroEntity.Shield.Should().Be(0, "护盾应被耗尽");
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(27, "30 - 3（护盾吸收5，剩余3扣HP）= 27");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-05：定策牌批量结算 —— 双方同回合互相打伤害
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T05_PlanCards_BothPlayersDealtDamage_InSameRound()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            // Act：双方各提交一张 10 点伤害定策牌
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId       = "P1",
                CardInstanceId = "plan_P1",
                Effects        = new List<EffectUnit> { MakeDamageEffect(10) },
            });
            rm.CommitPlanCard(ctx, new CommittedPlanCard
            {
                PlayerId       = "P2",
                CardInstanceId = "plan_P2",
                Effects        = new List<EffectUnit> { MakeDamageEffect(10) },
            });

            rm.EndRound(ctx);

            // Assert：双方各扣 10 HP
            ctx.AllPlayers["P1"].HeroEntity.Hp.Should().Be(20, "P1 受 10 点伤害");
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().Be(20, "P2 受 10 点伤害");
            rm.IsBattleOver.Should().BeFalse("双方均存活，战斗未结束");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-06：HP 归零 → 战斗结束，胜者正确
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T06_PlayerDies_BattleEnds_WithCorrectWinner()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);
            rm.BeginRound(ctx);

            // Act：P1 一击打出 30 点伤害，直接秒杀 P2
            rm.PlayInstantCard(ctx, "P1", "card_overkill", new List<EffectUnit> { MakeDamageEffect(30) });

            // Assert
            ctx.AllPlayers["P2"].HeroEntity.Hp.Should().BeLessOrEqualTo(0, "P2 应已死亡");
            rm.IsBattleOver.Should().BeTrue("战斗应已结束");
            rm.WinnerId.Should().Be("P1", "P1 获胜");
        }

        // ══════════════════════════════════════════════════════════════════
        // T-07：确定性随机 —— 同种子两场战斗，日志完全一致
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T07_SameSeed_ProducesSameBattleLog()
        {
            // Arrange & Act
            var r1 = CreateTestBattle(seed: 1234);
            var r2 = CreateTestBattle(seed: 1234);

            var (ctx1, rm1) = (r1.Context, r1.RoundManager);
            var (ctx2, rm2) = (r2.Context, r2.RoundManager);

            // 对两局执行完全相同的操作
            Action<CardMoba.BattleCore.Context.BattleContext, RoundManager> runOneBattle = (ctx, rm) =>
            {
                rm.BeginRound(ctx);
                rm.PlayInstantCard(ctx, "P1", "c1", new List<EffectUnit> { MakeDamageEffect(5) });
                rm.CommitPlanCard(ctx, new CommittedPlanCard
                {
                    PlayerId = "P2", CardInstanceId = "c2",
                    Effects  = new List<EffectUnit> { MakeDamageEffect(7) },
                });
                rm.EndRound(ctx);
            };

            runOneBattle(ctx1, rm1);
            runOneBattle(ctx2, rm2);

            // Assert：HP 完全一致（确定性保证）
            ctx1.AllPlayers["P1"].HeroEntity.Hp.Should().Be(ctx2.AllPlayers["P1"].HeroEntity.Hp);
            ctx1.AllPlayers["P2"].HeroEntity.Hp.Should().Be(ctx2.AllPlayers["P2"].HeroEntity.Hp);
            rm1.IsBattleOver.Should().Be(rm2.IsBattleOver);
        }

        // ══════════════════════════════════════════════════════════════════
        // T-08：多回合战斗 —— 3 回合后胜负确定
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void T08_MultiRound_BattleEndsWhenHpReachesZero()
        {
            // Arrange
            var result = CreateTestBattle();
            var (ctx, rm) = (result.Context, result.RoundManager);

            // 每回合 P1 造 11 点伤害（3 回合 = 33，超过 30 HP）
            for (int round = 1; round <= 3; round++)
            {
                if (rm.IsBattleOver) break;

                rm.BeginRound(ctx);

                // P1 出伤害牌
                rm.CommitPlanCard(ctx, new CommittedPlanCard
                {
                    PlayerId       = "P1",
                    CardInstanceId = $"plan_r{round}",
                    Effects        = new List<EffectUnit> { MakeDamageEffect(11) },
                });

                rm.EndRound(ctx);
            }

            // Assert
            rm.IsBattleOver.Should().BeTrue("3 回合 × 11 伤害 > 30 HP，P2 应已死亡");
            rm.WinnerId.Should().Be("P1");
            ctx.AllPlayers["P2"].HeroEntity.IsAlive.Should().BeFalse();
        }
    }
}
