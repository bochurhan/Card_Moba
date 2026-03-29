using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Results;
using CardMoba.MatchFlow.Catalog;
using CardMoba.MatchFlow.Context;
using CardMoba.MatchFlow.Definitions;

namespace CardMoba.MatchFlow.Core
{
    public interface IEquipmentBattleRuntime
    {
        string PlayerId { get; }
        string EquipmentId { get; }
        void OnBattleStarted(MatchContext matchContext, PlayerMatchState player, BattleContext battleContext, IEventBus runtimeEventBus);
        void OnBattleEnded(MatchContext matchContext, PlayerMatchState player, BattleContext battleContext, BattleSummary summary);
    }

    public sealed class EquipmentRuntimeFactory
    {
        private readonly IBuildCatalog _catalog;

        public EquipmentRuntimeFactory(IBuildCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public List<IEquipmentBattleRuntime> RegisterForBattle(MatchContext matchContext, BattleContext battleContext, IEventBus runtimeEventBus)
        {
            if (matchContext == null)
                throw new ArgumentNullException(nameof(matchContext));
            if (battleContext == null)
                throw new ArgumentNullException(nameof(battleContext));
            if (runtimeEventBus == null)
                throw new ArgumentNullException(nameof(runtimeEventBus));

            var runtimes = new List<IEquipmentBattleRuntime>();
            foreach (var battlePlayer in battleContext.AllPlayers.Values)
            {
                if (!matchContext.Players.TryGetValue(battlePlayer.PlayerId, out var player))
                    continue;

                foreach (var equipmentId in player.Loadout.EquipmentIds)
                {
                    var definition = _catalog.GetEquipmentDefinition(equipmentId);
                    if (definition == null)
                        continue;

                    var runtime = CreateRuntime(player.PlayerId, definition);
                    if (runtime == null)
                        continue;

                    runtime.OnBattleStarted(matchContext, player, battleContext, runtimeEventBus);
                    runtimes.Add(runtime);
                }
            }

            return runtimes;
        }

        private static IEquipmentBattleRuntime? CreateRuntime(string playerId, EquipmentDefinition definition)
        {
            return definition.EffectType switch
            {
                EquipmentEffectType.HealAfterBattleFlat => new HealAfterBattleFlatEquipmentRuntime(playerId, definition.EquipmentId, definition.EffectValue),
                _ => null,
            };
        }
    }

    public sealed class HealAfterBattleFlatEquipmentRuntime : IEquipmentBattleRuntime
    {
        private readonly int _healAmount;

        public HealAfterBattleFlatEquipmentRuntime(string playerId, string equipmentId, int healAmount)
        {
            PlayerId = playerId;
            EquipmentId = equipmentId;
            _healAmount = healAmount;
        }

        public string PlayerId { get; }
        public string EquipmentId { get; }

        public void OnBattleStarted(MatchContext matchContext, PlayerMatchState player, BattleContext battleContext, IEventBus runtimeEventBus)
        {
        }

        public void OnBattleEnded(MatchContext matchContext, PlayerMatchState player, BattleContext battleContext, BattleSummary summary)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            int beforeHp = player.PersistentHp;
            player.PersistentHp = Math.Min(player.MaxHp, player.PersistentHp + _healAmount);
            matchContext.MatchLog.Add($"[Equipment] {EquipmentId} healed {player.PlayerId} for {player.PersistentHp - beforeHp} after battle.");
        }
    }
}
