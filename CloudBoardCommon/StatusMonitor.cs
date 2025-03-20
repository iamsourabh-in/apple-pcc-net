using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public enum DaemonStatus
    {
        Starting,
        Initializing,
        WaitingForFirstAttestationFetch,
        WaitingForFirstKeyFetch,
        WaitingForFirstHotPropertyUpdate,
        WaitingForWorkloadRegistration,
        ServiceDiscoveryRunningFailed,
        GrpcServerRunningFailed,
        WorkloadControllerRunningFailed,
        DaemonExitingOnError,
        DaemonDrained
    }

    public interface IStatusMonitor
    {
        void Initializing();
        void WaitingForFirstAttestationFetch();
        void WaitingForFirstKeyFetch();
        void WaitingForFirstHotPropertyUpdate();
        void WaitingForWorkloadRegistration();
        void ServiceDiscoveryRunningFailed();
        void GrpcServerRunningFailed();
        void WorkloadControllerRunningFailed();
        void DaemonExitingOnError();
        void DaemonDrained();
    }
    public class StatusMonitor : IStatusMonitor
    {
        private readonly IMetricsSystem _metrics;
        private DaemonStatus _currentStatus = DaemonStatus.Starting;

        public StatusMonitor(IMetricsSystem metrics)
        {
            _metrics = metrics;
        }

        public void Initializing()
        {
            UpdateStatus(DaemonStatus.Initializing);
        }

        public void WaitingForFirstAttestationFetch()
        {
            UpdateStatus(DaemonStatus.WaitingForFirstAttestationFetch);
        }

        public void WaitingForFirstKeyFetch()
        {
            UpdateStatus(DaemonStatus.WaitingForFirstKeyFetch);
        }

        public void WaitingForFirstHotPropertyUpdate()
        {
            UpdateStatus(DaemonStatus.WaitingForFirstHotPropertyUpdate);
        }

        public void WaitingForWorkloadRegistration()
        {
            UpdateStatus(DaemonStatus.WaitingForWorkloadRegistration);
        }

        public void ServiceDiscoveryRunningFailed()
        {
            UpdateStatus(DaemonStatus.ServiceDiscoveryRunningFailed);
        }

        public void GrpcServerRunningFailed()
        {
            UpdateStatus(DaemonStatus.GrpcServerRunningFailed);
        }

        public void WorkloadControllerRunningFailed()
        {
            UpdateStatus(DaemonStatus.WorkloadControllerRunningFailed);
        }

        public void DaemonExitingOnError()
        {
            UpdateStatus(DaemonStatus.DaemonExitingOnError);
        }

        public void DaemonDrained()
        {
            UpdateStatus(DaemonStatus.DaemonDrained);
        }

        private void UpdateStatus(DaemonStatus status)
        {
            _currentStatus = status;
            _metrics.EmitStatusUpdate(status);
        }
    }
