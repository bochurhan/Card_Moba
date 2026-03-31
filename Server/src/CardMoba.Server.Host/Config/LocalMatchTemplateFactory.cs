using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Core;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Core;
using CardMoba.MatchFlow.Deck;
using CardMoba.MatchFlow.Rules;
using CardMoba.Protocol.Enums;
using CardMoba.Server.Host.Services;

namespace CardMoba.Server.Host.Config
{
    /// <summary>
    /// localhost 1v1 测试对局模板。
    /// 当前复用客户端本地验证用的 4+1 规则和默认战士卡组。
    /// </summary>
    public sealed class LocalMatchTemplateFactory
    {
        private static readonly int[] DefaultWarriorDeckIds =
        {
            2001, 2001, 2001,
            1001, 1001, 1001,
            1002, 1002,
            1003, 1003,
            1004, 1004,
            1005,
            1008,
            2002, 2002,
            2003, 2003,
            2004,
            2005, 2005,
            2006,
            2007, 2007,
            2008, 2008,
            2009, 2009,
            2010, 2010,
            2011, 2011,
            2013,
            1006,
            2015,
        };

        private readonly MatchFactory _matchFactory;
        private readonly ServerCardCatalog _cardCatalog;

        public LocalMatchTemplateFactory(ServerCardCatalog cardCatalog)
        {
            _cardCatalog = cardCatalog;
            _matchFactory = new MatchFactory();
        }

        public MatchContext CreateLocalMatch(string matchId, PendingLocalMatchRoom room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));

            return _matchFactory.CreateMatch(
                matchId,
                CreateRuleset(),
                CreatePlayers(),
                baseRandomSeed: 42);
        }

        public MatchRuleset CreateRuleset()
        {
            string defaultPoolId = BuildCatalogAssembler.GetDefaultPoolId(HeroClass.Warrior);
            var ruleset = new MatchRuleset
            {
                BuildWindowTimeoutMs = 30000,
                DefaultTimeoutAction = BuildActionType.Heal,
            };

            for (int i = 0; i < 4; i++)
            {
                var step = new MatchBattleStep
                {
                    StepId = $"battle_{i + 1:D2}",
                    Mode = BattleStepMode.Duel1v1,
                    BattleRuleset = new BattleRuleset
                    {
                        Mode = BattleMode.Duel1v1,
                        LocalEndPolicy = BattleLocalEndPolicy.RoundLimit,
                        MaxRounds = 5,
                    },
                    OpensBuildWindowAfter = true,
                    BuildPoolId = defaultPoolId,
                };
                step.ParticipantPlayerIds.Add(LocalMatchRegistry.HostPlayerId);
                step.ParticipantPlayerIds.Add(LocalMatchRegistry.GuestPlayerId);
                ruleset.Steps.Add(step);
            }

            var finalStep = new MatchBattleStep
            {
                StepId = "battle_05_final",
                Mode = BattleStepMode.Duel1v1,
                BattleRuleset = new BattleRuleset
                {
                    Mode = BattleMode.Duel1v1,
                    LocalEndPolicy = BattleLocalEndPolicy.TeamElimination,
                    MaxRounds = 10,
                },
                OpensBuildWindowAfter = false,
                BuildPoolId = defaultPoolId,
            };
            finalStep.ParticipantPlayerIds.Add(LocalMatchRegistry.HostPlayerId);
            finalStep.ParticipantPlayerIds.Add(LocalMatchRegistry.GuestPlayerId);
            ruleset.Steps.Add(finalStep);

            return ruleset;
        }

        private IEnumerable<PlayerMatchState> CreatePlayers()
        {
            yield return CreatePlayer(LocalMatchRegistry.HostPlayerId, LocalMatchRegistry.HostTeamId);
            yield return CreatePlayer(LocalMatchRegistry.GuestPlayerId, LocalMatchRegistry.GuestTeamId);
        }

        private PlayerMatchState CreatePlayer(string playerId, string teamId)
        {
            return new PlayerMatchState
            {
                PlayerId = playerId,
                TeamId = teamId,
                MaxHp = 200,
                PersistentHp = 200,
                Deck = BuildPersistentDeck(DefaultWarriorDeckIds, playerId),
                Loadout = CreateDefaultLoadout(),
            };
        }

        private static PlayerLoadout CreateDefaultLoadout()
        {
            var loadout = new PlayerLoadout
            {
                ClassId = HeroClass.Warrior,
                DefaultBuildPoolId = BuildCatalogAssembler.GetDefaultPoolId(HeroClass.Warrior),
            };
            loadout.EquipmentIds.Add("burning_blood");
            return loadout;
        }

        private PersistentDeckState BuildPersistentDeck(IEnumerable<int> cardIds, string ownerId)
        {
            var deck = new PersistentDeckState();
            int index = 0;

            foreach (var cardId in cardIds)
            {
                if (_cardCatalog.GetCard(cardId) == null)
                    continue;

                string configId = cardId.ToString();
                deck.AddCard(new PersistentDeckCard
                {
                    PersistentCardId = $"{ownerId}_deck_{index:D2}_{configId}",
                    BaseConfigId = configId,
                    CurrentConfigId = configId,
                    UpgradeLevel = 0,
                });
                index++;
            }

            return deck;
        }
    }
}
