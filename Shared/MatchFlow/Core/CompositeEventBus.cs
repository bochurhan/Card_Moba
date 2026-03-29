using System;
using System.Collections.Generic;
using CardMoba.BattleCore.EventBus;

namespace CardMoba.MatchFlow.Core
{
    public sealed class CompositeEventBus : IEventBus
    {
        private readonly List<IEventBus> _children = new List<IEventBus>();

        public CompositeEventBus(params IEventBus[] children)
        {
            foreach (var child in children)
            {
                if (child != null)
                    _children.Add(child);
            }
        }

        public void Publish<T>(T battleEvent) where T : BattleEventBase
        {
            foreach (var child in _children)
                child.Publish(battleEvent);
        }

        public void Subscribe<T>(Action<T> handler) where T : BattleEventBase
        {
            foreach (var child in _children)
                child.Subscribe(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase
        {
            foreach (var child in _children)
                child.Unsubscribe(handler);
        }
    }
}
