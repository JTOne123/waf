﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.Waf.Foundation;
using System.Waf.UnitTesting;

namespace Test.Waf.Foundation
{
    [TestClass]
    public class WeakEventEventHandlerTTest
    {
        [TestMethod]
        public void WeakEvent1() => WeakEvent1Core(false);

        [TestMethod]
        public void WeakEvent1B() => WeakEvent1Core(true);

        [TestMethod]
        private void WeakEvent1Core(bool removeTwice)
        {
            var publisher = new Publisher();
            var (weakManager, _, weakSubscriber) = WeakEventHandlerCore(null, publisher, null, removeTwice: removeTwice);
            GC.Collect();
            Assert.IsFalse(weakManager.TryGetTarget(out _));
            Assert.IsFalse(weakSubscriber.TryGetTarget(out _));
            Assert.AreEqual(1, publisher.EventHandlerCount);
            publisher.RaiseEvent();
            Assert.AreEqual(0, publisher.EventHandlerCount);
            publisher.RaiseEvent();
            Assert.AreEqual(0, publisher.EventHandlerCount);
        }

        [TestMethod]
        public void WeakEvent2()
        {
            var subscriber = new Subscriber();
            var (weakManager, weakPublisher, _) = WeakEventHandlerCore(null, null, subscriber);
            GC.Collect();
            Assert.IsFalse(weakManager.TryGetTarget(out _));
            Assert.IsFalse(weakPublisher.TryGetTarget(out _));
        }

        [TestMethod]
        public void WeakEvent3()
        {
            var manager = new Manager();
            var (_, weakPublisher, weakSubscriber) = WeakEventHandlerCore(manager, null, null);
            GC.Collect();
            Assert.IsFalse(weakPublisher.TryGetTarget(out _));
            Assert.IsFalse(weakSubscriber.TryGetTarget(out _));
        }

        [TestMethod]
        public void WeakEventRemove()
        {
            var publisher = new Publisher();
            var (weakManager, _, weakSubscriber) = WeakEventHandlerCore(null, publisher, null, remove: true);
            GC.Collect();
            Assert.IsFalse(weakManager.TryGetTarget(out _));
            Assert.IsFalse(weakSubscriber.TryGetTarget(out _));
            Assert.AreEqual(0, publisher.EventHandlerCount);
        }

        [TestMethod]
        public void WeakEventAddArgumentException()
        {
            var publisher = new Publisher();
            var subscriber = new Subscriber();
            AssertHelper.ExpectedException<ArgumentNullException>(() => WeakEvent.EventHandler<PropertyChangedEventArgs>.Add((Publisher)null!, subscriber.Handler, (s, h) => s.Event1 += h, (s, h) => s.Event1 -= h));
            AssertHelper.ExpectedException<ArgumentNullException>(() => WeakEvent.EventHandler<PropertyChangedEventArgs>.Add(publisher, null!, (s, h) => s.Event1 += h, (s, h) => s.Event1 -= h));
            AssertHelper.ExpectedException<ArgumentNullException>(() => WeakEvent.EventHandler<PropertyChangedEventArgs>.Add(publisher, subscriber.Handler, null!, (s, h) => s.Event1 -= h));
            AssertHelper.ExpectedException<ArgumentNullException>(() => WeakEvent.EventHandler<PropertyChangedEventArgs>.Add(publisher, subscriber.Handler, (s, h) => s.Event1 += h, null!));
        }

        [TestMethod]
        public void WeakEventPerformance()
        {
            var publisher = new Publisher();
            WeakEventHandlerCore(null, publisher, null, raiseCount: 1_000_000);
        }

        private (WeakReference<Manager>, WeakReference<Publisher>, WeakReference<Subscriber>) WeakEventHandlerCore(Manager? manager, Publisher? publisher, Subscriber? subscriber,
            int raiseCount = 1, bool remove = false, bool removeTwice = false)
        {
            manager ??= new Manager();
            publisher ??= new Publisher();
            subscriber ??= new Subscriber();
            Assert.AreEqual(0, publisher.EventHandlerCount);
            manager.Add(publisher, subscriber);
            IWeakEventProxy? proxy1 = null;
            if (removeTwice)
            {
                proxy1 = manager.Proxy;
                manager.Add(publisher, subscriber);
            }
            Assert.AreEqual(removeTwice ? 2 : 1, publisher.EventHandlerCount);
            proxy1?.Remove();
            proxy1?.Remove();

            GC.Collect();

            Assert.AreEqual(0, subscriber.HandlerCallCount);
            for (int i = 0; i < raiseCount; i++) publisher.RaiseEvent();
            Assert.AreEqual(raiseCount, subscriber.HandlerCallCount);
            if (remove)
            {
                var count = subscriber.HandlerCallCount;
                manager.Proxy!.Remove();
                publisher.RaiseEvent();
                Assert.AreEqual(count, subscriber.HandlerCallCount);
                manager.Proxy.Remove();
                publisher.RaiseEvent();
                Assert.AreEqual(count, subscriber.HandlerCallCount);
            }

            return (new WeakReference<Manager>(manager), new WeakReference<Publisher>(publisher), new WeakReference<Subscriber>(subscriber));
        }

        private class Manager
        {
            public IWeakEventProxy? Proxy { get; set; }

            public void Add(Publisher publisher, Subscriber subscriber) => Proxy = WeakEvent.EventHandler<PropertyChangedEventArgs>.Add(publisher, subscriber.Handler, (s, h) => s.Event1 += h, (s, h) => s.Event1 -= h);
        }

        private class Publisher
        {
            private static readonly PropertyChangedEventArgs args = new PropertyChangedEventArgs("Test");
            private EventHandler<PropertyChangedEventArgs>? event1;

            public int EventHandlerCount { get; private set; }

            public event EventHandler<PropertyChangedEventArgs>? Event1
            {
                add
                {
                    event1 += value;
                    EventHandlerCount++;
                }
                remove
                {
                    event1 -= value;
                    EventHandlerCount--;
                }
            }

            public void RaiseEvent() => event1?.Invoke(this, args);
        }

        private class Subscriber
        {
            public int HandlerCallCount { get; set; }

            public void Handler(object? sender, PropertyChangedEventArgs e) => HandlerCallCount++;
        }
    }
}