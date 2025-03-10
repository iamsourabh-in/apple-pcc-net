# apple-pcc-net

A project structure mirroring the PCC architecture in .NET C#, focusing on daemon processes, inter-process communication (IPC), External Services, Gateway, metrics, health checks, and service discovery. We'll leverage the insights gained from the Swift code.

```uml
@startuml
!theme silver
actor User as "User/Administrator"
actor ExternalService as "External Services"

participant CloudBoardDaemon
participant IdentityManager
participant ServiceDiscoveryPublisher
participant HeartbeatPublisher
participant WorkloadController
participant AttestationProvider
participant HealthServer
participant LifecycleManager
participant MetricsSystem
participant TransparencyLogService as "Transparency Log Service"

User -> CloudBoardDaemon: Start Daemon with Configuration

CloudBoardDaemon -> IdentityManager: Initialize TLS Identity
IdentityManager -> ExternalService: Request TLS Certificates
ExternalService -> IdentityManager: Provide TLS Certificates

CloudBoardDaemon -> ServiceDiscoveryPublisher: Initialize Service Discovery
ServiceDiscoveryPublisher -> ExternalService: Register and Discover Services
ExternalService -> ServiceDiscoveryPublisher: Service Discovery Information

CloudBoardDaemon -> HeartbeatPublisher: Initialize Heartbeat
HeartbeatPublisher -> ExternalService: Send Heartbeat Signals

CloudBoardDaemon -> WorkloadController: Initialize Workload Management
WorkloadController -> MetricsSystem: Monitor Resource Usage
WorkloadController -> ExternalService: Fetch Workload Data
ExternalService -> WorkloadController: Provide Workload Data

CloudBoardDaemon -> AttestationProvider: Initialize Attestation
AttestationProvider -> ExternalService: Request Attestation Verification
ExternalService -> AttestationProvider: Attestation Results

CloudBoardDaemon -> HealthServer: Start Health Monitoring
HealthServer -> ExternalService: Provide Health Status

CloudBoardDaemon -> LifecycleManager: Manage Service Lifecycle
LifecycleManager -> ExternalService: Start/Stop Services

CloudBoardDaemon -> MetricsSystem: Collect and Report Metrics
MetricsSystem -> ExternalService: Send Metrics Data

CloudBoardDaemon -> TransparencyLogService: Log Operations
TransparencyLogService -> ExternalService: Verify Log Entries
@enduml

```


## Approach 1 (Normal .NET C#)

You'll need several projects, each representing a daemon or core service:

- `CloudBoardD` (.NET Console App): This is the main CloudBoard daemon (cloudboardd).
- `CloudBoardAttestationD` (.NET Console App): This daemon handles key management and attestation (cb_attestationd).
- `CloudBoardJobAuthD` (.NET Console App): Manages authentication tokens and keys (cb_jobauthd).
- `CloudBoardJobHelper` (.NET Console App): Handles individual job executions (cb_jobhelper).
- `CloudBoardCommon` (.NET Class Library): Shared types, interfaces, and utilities across all daemons.
- `CloudBoardMetrics` (.NET Class Library): Provides the metrics infrastructure (counters, gauges, histograms). Consider using a library like Prometheus .NET Client.
- `CloudBoardHealth` (.NET Class Library): Implement health check logic (HTTP and gRPC endpoints).
- `CloudBoardServiceDiscovery` (.NET Class Library): Handles service registration and discovery (e.g., using Consul, etcd, or a custom solution). This part will depend on how you want to handle service discovery.
- `CloudBoardWorkload` (Multiple .NET Console Apps): One for each type of workload (cloud app). This mirrors the Swift approach where each application is handled by a separate workload.
- `CloudBoardIdentity` (.NET Class Library): Responsible for managing identities and certificates.
- `CloudBoardJobAPI` (.NET Class Library): Define the contract for job requests.
- `CloudBoardAttestationAPI` (.NET Class Library): Define the contract for attestation requests.


## Approach 2 (using Actors)

Identified Actors:

CloudBoardDaemon: This is a central actor managing the CloudBoard daemon's lifecycle, handling requests, and interacting with various other components.
CloudBoardAttestationDaemon: This actor manages key rotation, attestation generation, and service interactions.
CloudBoardJobHelper: This actor handles individual job requests, performing authentication, workload management, and communication with the client.
CloudBoardMessenger: This actor manages communication and encryption/decryption within CloudBoardJobHelper.
TokenGrantingTokenValidator: This actor validates authentication tokens.
WorkloadJobManager: This actor within CloudBoardJobHelper manages the asynchronous streams, interacts with CloudBoardAppWorkload, and handles job execution.
CloudBoardAppWorkload: This actor manages the lifecycle of a single workload process and data streaming.
CloudBoardAppStateMachine: A state machine actor within CloudBoardAppWorkload to manage the different phases of workload execution.
JobAuthClientDelegate: An actor that handles responses from CloudBoardJobAuthD.
CloudAppResponseDelegate: An actor that handles responses from the workload.
CloudBoardAttestationServer: An actor in CloudBoardAttestationD that handles requests for attestations and keys.
AttestationStateMachine: A state machine actor within CloudBoardAttestationServer.

Inferred Actors (Likely Present but Not Directly Shown):

Actors within the CloudBoardJobAuthD daemon for handling authentication-related tasks.
Actors or classes within CloudBoardAttestationD to manage low-level details of key management and attestation generation.
Actors to handle service discovery operations.
Actors to handle hot properties updates.
Actors in the MetricsSystem to monitor events.

### Why Orleans Isn't Ideal for a Single-Machine Deployment:
-------


Orleans is designed for distributed systems. 
Its primary benefits—scalability and fault tolerance across multiple machines—are not needed when everything runs on a single machine. Using Orleans in this scenario would introduce significant overhead without providing any commensurate benefits.

Communication Overhead: Orleans relies on network communication between grains (even on the same machine). This adds latency and complexity that's unnecessary in a single-machine context. Direct inter-process communication (IPC) mechanisms like named pipes or shared memory would be far more efficient.

Serialization/Deserialization: Orleans serializes and deserializes messages when communicating between grains. This overhead is not necessary in a single-machine, in-memory context.

Management Complexity: Managing an Orleans cluster adds complexity, especially for a single-machine deployment. This added complexity is not needed if everything is running on a single machine.
Better Alternatives for a Single-Machine Deployment:

### For a single-machine deployment, consider these alternatives:

.NET's built-in concurrency features: Use Task, async, await, and other built-in concurrency features to handle multiple requests efficiently and asynchronously. This is significantly simpler to implement than Orleans for a single-machine application.
Tasks and Thread Pools: Use Task.Run and the thread pool to manage the execution of workloads concurrently.

Named Pipes or Shared Memory: These are efficient IPC mechanisms for communication between processes on the same machine. Shared memory is even faster, but it is more complex to implement, requires proper synchronization, and is more difficult to debug. Named pipes are more robust and better suited to handle asynchronous communication.
Actors using a simpler framework: Consider using a simpler actor framework designed for single-machine deployments if you still want an actor model.

In summary: For a single-machine deployment of the Apple PCC system, a simpler, non-distributed architecture using .NET's built-in concurrency features and efficient IPC mechanisms would be far more efficient and easier to manage than Orleans. The overhead introduced by Orleans would outweigh any benefits in this scenario.