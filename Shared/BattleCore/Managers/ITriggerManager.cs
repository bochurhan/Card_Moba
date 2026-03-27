#pragma warning disable CS8632

using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Foundation;

namespace CardMoba.BattleCore.Managers
{
    /// <summary>
    /// Runtime trigger registry and dispatch contract.
    /// </summary>
    public interface ITriggerManager
    {
        string Register(TriggerUnit trigger);

        bool Unregister(string triggerId);

        int UnregisterBySourceId(string sourceId);

        void Fire(BattleContext ctx, TriggerTiming timing, TriggerContext triggerCtx);

        void TickDecay(BattleContext ctx);
    }
}
