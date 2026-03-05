
#pragma warning disable CS8632

using System;
using System.Collections.Generic;

namespace CardMoba.BattleCore.EventBus
{
    /// <summary>
    /// 战斗事件总线接口（外部广播，单向）。
    /// 订阅者只读 BattleContext，不可写入任何状态。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>发布战斗事件（广播给所有该类型的订阅者）</summary>
        void Publish<T>(T battleEvent) where T : BattleEventBase;

        /// <summary>订阅指定类型的战斗事件</summary>
        void Subscribe<T>(Action<T> handler) where T : BattleEventBase;

        /// <summary>取消订阅</summary>
        void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase;
    }

    /// <summary>
    /// 战斗事件总线实现。
    /// </summary>
    public class BattleEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new Dictionary<Type, List<object>>();

        /// <inheritdoc/>
        public void Publish<T>(T battleEvent) where T : BattleEventBase
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
                return;

            // 遍历副本，避免订阅者内部修改列表时出错
            var snapshot = new List<object>(handlers);
            foreach (var handler in snapshot)
            {
                ((Action<T>)handler).Invoke(battleEvent);
            }
        }

        /// <inheritdoc/>
        public void Subscribe<T>(Action<T> handler) where T : BattleEventBase
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<object>();
                _handlers[type] = handlers;
            }
            handlers.Add(handler);
        }

        /// <inheritdoc/>
        public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }
}
