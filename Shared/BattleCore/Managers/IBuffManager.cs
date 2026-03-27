#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// Runtime buff lifecycle contract.
    /// </summary>
    public interface IBuffManager
    {
        BuffUnit AddBuff(BattleContext ctx, string targetEntityId, string buffConfigId, string sourcePlayerId, int value = 0, int duration = -1);

        bool RemoveBuff(BattleContext ctx, string targetEntityId, string buffRuntimeId);

        int RemoveBuffsByConfig(BattleContext ctx, string targetEntityId, string buffConfigId);

        bool HasBuff(BattleContext ctx, string entityId, string buffConfigId);

        bool HasBuffType(BattleContext ctx, string entityId, BuffType buffType);

        IReadOnlyList<BuffUnit> GetBuffs(string entityId);

        void TickDecay(BattleContext ctx);

        void OnRoundEnd(BattleContext ctx, int round);
    }
}
