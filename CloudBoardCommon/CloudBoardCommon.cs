// CloudBoardCommon/JobAPI.cs
using CloudBoardJobAPI;
using Grpc.Net.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class JobRequest
    {
        public Guid JobId { get; set; }
        public string RequestData { get; set; } = ""; // Example: JSON payload
    }

    public class JobResponse
    {
        public Guid JobId { get; set; }
        public string ResponseData { get; set; } = ""; // Example: JSON payload
    }

    //Add other classes here if needed

    public interface IHealthCheck
    {
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
    }

    public interface IJobAuthClient
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task<IEnumerable<byte[]>> RequestTGTSigningKeysAsync(CancellationToken cancellationToken);
        Task<IEnumerable<byte[]>> RequestOTTSigningKeysAsync(CancellationToken cancellationToken);
    }

    public abstract class JobAuthBase
    {
    }


    public abstract class HealthCheckBase
    {
    }

    public interface IJobHelperAPI
    {
        Task<JobResponse> HandleJobRequestAsync(JobRequest request, CancellationToken cancellationToken);
    }
    public interface IJobServiceClient
    {
        Task<CloudBoardJobAPI.JobResponse> HandleJobRequestAsync(CloudBoardJobAPI.JobRequest request, CancellationToken cancellationToken);
    }

}


