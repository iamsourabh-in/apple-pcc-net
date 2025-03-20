using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class ServiceHealthMonitor : IHealthPublisher
    {
        private readonly List<IHealthSubscriber> _subscribers = new();
        private HealthStatus _currentStatus = HealthStatus.NotServing;
        private readonly object _lock = new();

        public HealthStatus CurrentStatus
        {
            get
            {
                lock (_lock)
                {
                    return _currentStatus;
                }
            }
        }

        public void AddSubscriber(IHealthSubscriber subscriber)
        {
            lock (_lock)
            {
                _subscribers.Add(subscriber);
                subscriber.OnHealthUpdate(_currentStatus);
            }
        }

        public void RemoveSubscriber(IHealthSubscriber subscriber)
        {
            lock (_lock)
            {
                _subscribers.Remove(subscriber);
            }
        }

        public void UpdateStatus(HealthStatus status)
        {
            lock (_lock)
            {
                if (_currentStatus == status)
                {
                    return;
                }

                _currentStatus = status;

                foreach (var subscriber in _subscribers)
                {
                    subscriber.OnHealthUpdate(status);
                }
            }
        }

        public void Drain()
        {
            UpdateStatus(HealthStatus.NotServing);
        }
    }

    public enum HealthStatus
    {
        Unknown,
        Serving,
        NotServing
    }

    public interface IHealthPublisher
    {
        HealthStatus CurrentStatus { get; }
        void AddSubscriber(IHealthSubscriber subscriber);
        void RemoveSubscriber(IHealthSubscriber subscriber);
        void UpdateStatus(HealthStatus status);
        void Drain();
    }

    public interface IHealthSubscriber
    {
        void OnHealthUpdate(HealthStatus status);
    }
}
