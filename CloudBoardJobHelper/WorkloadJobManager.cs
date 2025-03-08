//using CloudBoardCommon;
//using CloudBoardJobAPI;
//using CloudBoardJobHelperAPI;
//using Grpc.Core;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Prometheus; //For metrics

//namespace CloudBoardJobHelper
//{
//    public class WorkloadJobManager
//    {
//        private readonly IAsyncEnumerable<PipelinePayload> _requestStream;
//        private readonly IAsyncEnumerator<FinalizableChunk> _responseContinuation;
//        private readonly IAsyncEnumerator<PipelinePayload> _cloudAppRequestContinuation;
//        private readonly IAsyncEnumerable<CloudAppResponse> _cloudAppResponseStream;
//        private readonly IAsyncEnumerator<CloudAppResponse> _cloudAppResponseContinuation;
//        private readonly ITokenGrantingTokenValidator _tgtValidator;
//        private readonly bool _enforceTGTValidation;
//        private readonly int _maxRequestMessageSize;
//        private readonly IMetrics _metrics;
//        private readonly Guid _jobUUID;
//        private readonly LengthPrefixBuffer _buffer;
//        private readonly IWorkload _workload; //Interface to handle workload execution
//        private string _requestID = "";
//        private ParametersData.PlaintextMetadata? _requestPlaintextMetadata;
//        private long _requestParametersReceivedInstant;

//        private readonly object _lock = new object();
//        private int _requestMessageCount = 0;
//        private int _responseMessageCount = 0;


//        public WorkloadJobManager(
//            IAsyncEnumerable<PipelinePayload> requestStream,
//            IAsyncEnumerator<FinalizableChunk> responseContinuation,
//            IAsyncEnumerator<PipelinePayload> cloudAppRequestContinuation,
//            IAsyncEnumerable<CloudAppResponse> cloudAppResponseStream,
//            IAsyncEnumerator<CloudAppResponse> cloudAppResponseContinuation,
//            ITokenGrantingTokenValidator tgtValidator,
//            bool enforceTGTValidation,
//            int maxRequestMessageSize,
//            IMetrics metrics,
//            Guid jobUUID,
//            IWorkload workload // Inject IWorkload
//        )
//        {
//            _requestStream = requestStream;
//            _responseContinuation = responseContinuation;
//            _cloudAppRequestContinuation = cloudAppRequestContinuation;
//            _cloudAppResponseStream = cloudAppResponseStream;
//            _cloudAppResponseContinuation = cloudAppResponseContinuation;
//            _tgtValidator = tgtValidator;
//            _enforceTGTValidation = enforceTGTValidation;
//            _maxRequestMessageSize = maxRequestMessageSize;
//            _metrics = metrics;
//            _jobUUID = jobUUID;
//            _buffer = new LengthPrefixBuffer(_maxRequestMessageSize);
//            _workload = workload; // Use injected workload
//        }

//        public async Task RunAsync()
//        {
//            var requestTask = ProcessRequestsAsync();
//            var responseTask = ProcessResponsesAsync();

//            try
//            {
//                await Task.WhenAll(requestTask, responseTask);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error in WorkloadJobManager: {ex}");
//            }

//        }

//        private async Task ProcessRequestsAsync()
//        {
//            try
//            {
//                await foreach (var message in _requestStream)
//                {
//                    Interlocked.Increment(ref _requestMessageCount);
//                    await ReceivePipelineMessageAsync(message);
//                }
//                // ... (handle stream end) ...
//                await _cloudAppRequestContinuation.DisposeAsync();
//            }
//            catch (Exception ex)
//            {
//                // Handle exceptions appropriately
//                Console.WriteLine($"Error processing requests: {ex.Message}");
//                await _cloudAppResponseContinuation.DisposeAsync(); // Ensure resources are released
//            }
//        }

//        private async Task ProcessResponsesAsync()
//        {
//            // This method should be improved, for now it is a simple implementation
//            try
//            {
//                await _responseContinuation.MoveNextAsync();
//                var responseSummary = new CloudBoardJobHelperRequestSummary(_jobUUID);
//                await foreach (var response in _cloudAppResponseStream)
//                {
//                    Interlocked.Increment(ref _responseMessageCount);
//                    await SendResponseAsync(response);
//                }
//                responseSummary.EndTimeNanos = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//                responseSummary.RequestMessageCount = _requestMessageCount;
//                responseSummary.ResponseMessageCount = _responseMessageCount;
//                // ... (handle stream end) ...
//                await _responseContinuation.DisposeAsync();
//            }
//            catch (Exception ex)
//            {
//                // Handle exceptions appropriately
//                Console.WriteLine($"Error processing responses: {ex.Message}");
//                await _responseContinuation.DisposeAsync(); // Ensure resources are released
//            }
//        }


//        private async Task ReceivePipelineMessageAsync(PipelinePayload message)
//        {
//            _metrics.IncrementCounter("total_requests_received"); // Increment total requests counter

//            switch (message.Type)
//            {
//                case PipelinePayloadType.Warmup:
//                    await _cloudAppRequestContinuation.MoveNextAsync();
//                    _cloudAppRequestContinuation.Current = message;
//                    break;
//                case PipelinePayloadType.OneTimeToken:
//                    try { await _tgtValidator.ValidateOneTimeTokenAsync(message.Data); }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"OneTimeToken validation failed: {ex.Message}");
//                        // Handle exception accordingly
//                    }
//                    break;
//                case PipelinePayloadType.Parameters:
//                    _requestParametersReceivedInstant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//                    _requestPlaintextMetadata = message.ParametersData.PlaintextMetadata;
//                    _requestID = message.ParametersData.PlaintextMetadata.RequestID;
//                    await _cloudAppRequestContinuation.MoveNextAsync();
//                    _cloudAppRequestContinuation.Current = message;
//                    break;
//                case PipelinePayloadType.Chunk:
//                    await ProcessEncodedRequestChunkAsync(message.Data);
//                    break;
//                case PipelinePayloadType.EndOfInput:
//                    // ... (handle end of input) ...
//                    break;
//                case PipelinePayloadType.Teardown:
//                    // ... (handle teardown) ...
//                    break;
//            }
//        }


//        private async Task ProcessEncodedRequestChunkAsync(byte[] encodedRequestChunk)
//        {
//            var chunks = _buffer.Append(encodedRequestChunk);

//            foreach (var chunk in chunks)
//            {
//                try
//                {
//                    var request = PrivateCloudComputeRequest.Parser.ParseFrom(chunk.Data);
//                    switch (request.Type)
//                    {
//                        case PrivateCloudComputeRequestType.ApplicationPayload:
//                            await _cloudAppRequestContinuation.MoveNextAsync();
//                            _cloudAppRequestContinuation.Current = new PipelinePayload(PipelinePayloadType.Chunk, chunk.Data);
//                            break;
//                        case PrivateCloudComputeRequestType.AuthToken:
//                            var requests = await _tgtValidator.ValidateAuthTokenAsync(request.AuthToken);
//                            foreach (var req in requests)
//                            {
//                                await _cloudAppRequestContinuation.MoveNextAsync();
//                                _cloudAppRequestContinuation.Current = req;
//                            }
//                            break;
//                        case PrivateCloudComputeRequestType.FinalMessage:
//                            await _cloudAppRequestContinuation.MoveNextAsync();
//                            _cloudAppRequestContinuation.Current = new PipelinePayload(PipelinePayloadType.EndOfInput);
//                            break;
//                        default:
//                            Console.WriteLine($"Received encoded request of unknown type, ignoring");
//                            break;
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Error parsing encoded request chunk: {ex}");
//                }
//            }
//        }


//        private async Task SendResponseAsync(CloudAppResponse response)
//        {
//            switch (response.Type)
//            {
//                case CloudAppResponseType.Chunk:
//                    var serializedResponse = PrivateCloudComputeResponse.Parser.ParseFrom(response.Data).ToByteArray();
//                    try
//                    {
//                        await _responseContinuation.MoveNextAsync();
//                        _responseContinuation.Current = new FinalizableChunk(serializedResponse, response.IsFinal);
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"Error sending response chunk: {ex}");
//                    }
//                    break;
//                case CloudAppResponseType.AppTermination:
//                    // ... (handle app termination) ...
//                    break;
//            }
//        }


//    }



//    // ... (other classes and interfaces) ...
//}
