// CloudBoardCommon/GrpcJobServiceClient.cs
using CloudBoardJobAPI;
using Grpc.Net.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class GrpcJobServiceClient : IJobServiceClient
    {
        private readonly JobService.JobServiceClient _client;

        public GrpcJobServiceClient(string jobHelperAddress)
        {
            var channel = GrpcChannel.ForAddress(jobHelperAddress);
            _client = new JobService.JobServiceClient(channel);
        }

        public async Task<CloudBoardJobAPI.JobResponse> HandleJobRequestAsync(CloudBoardJobAPI.JobRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _client.HandleJobRequestAsync(request, cancellationToken: cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling JobHelper: {ex.Message}");
                throw; // Or handle the error more gracefully (retry, fallback, etc.)
            }
        }
    }
}
