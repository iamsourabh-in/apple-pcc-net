"# apple-pcc-net" 


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