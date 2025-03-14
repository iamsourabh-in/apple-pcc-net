# Diagram Explanation and Key Payloads

## Initialization

- **CloudBoardDaemon** initializes various components:
  - `CloudMetricsSystem`
  - `StatusMonitor`
  - `RequestFielderManager`
  - `CloudBoardJobHelperXPCClientProvider`
  - `JobHelperResponseDelegateProvider`
  - `HotPropertiesController`
  - `HealthMonitor`
  - `HealthServer`
  - `ServiceDiscoveryPublisher`
  - `AttestationProvider`
  - `HeartbeatPublisher`
  - `WorkloadController`
- Loads configurations from either a file or preferences.
- Metrics are initialized.
- **LaunchdJobHelper** cleans up existing jobs.
- **JobQuiescenceMonitor** starts the quiescence monitor.
- **IdentityManager** initializes based on the setting, either with a self-signed certificate or not.

## Service Address Resolution

- **CloudBoardDaemon** resolves the service address from the configuration.

## Service Discovery (Conditional)

- If service discovery is enabled:
  - **ServiceDiscoveryPublisher** is initialized and configured.
  - Starts to register and discover services.

## Attestation Provider

- If `AttestationProvider` is not provided to the initializer, it is created using `CloudBoardAttestationAPIXPCClient`.
- `AttestationProvider.run()` is called to start fetching attestations.
- The first attestation fetch is performed, and `StatusMonitor` waits until it is fetched.

## Key Validation

- If `blockHealthinessOnAuthSigningKeysPresence` is set to `true`:
  - The process tries to connect to `CloudBoardJobAuthAPIXPCClient` and fetch TGT and OTT signing keys.
  - If the keys are not retrieved, the process exits.

## CloudBoardProvider

- **CloudBoardProvider** is initialized to manage the gRPC interface and related resources.
- It is started to receive requests.

## Identity Management

- **IdentityManager** manages TLS certificates and refreshes them periodically.

## Health Monitoring

- **HealthProvider** and **HealthServer** are used for health checks.
- The health server starts listening to health events.

## Heartbeats (Conditional)

- If heartbeats are configured:
  - **HeartbeatPublisher** is initialized and starts publishing heartbeats.

## gRPC Server

- The gRPC server starts, using either TLS or insecure mode based on the configuration.
- The server binds to the specified service address and port.
- The method `GRPCTLSConfiguration.cloudboardProviderConfiguration()` is called to create the gRPC server configuration.

## Workload Controller (Conditional)

- If a **WorkloadController** is present, it starts running to manage cloud workload requests.

## Hot Properties (Conditional)

- If hot properties are used, it starts listening to property updates.

## Task Group and Error Handling

- All operations are launched within a `TaskGroup` to ensure clean system shutdown if any operation fails.
- `StatusMonitor` listens for any status changes.

## Drain and Quiescence

- On drain:
  - Providers initiate the drain process (e.g., `CloudBoardProvider`, `HealthMonitor`, `WorkloadController`).
- After drain:
  - **JobQuiescenceMonitor** waits for jobs to quiesce.
- A metric for `DrainCompletionTimeHistogram` is emitted to `CloudMetricsSystem`.
- The process can either end or continue.

---

## Payload Notes in the Diagram

1. **Configuration Data**:
   - Loading configuration details (from file or preferences) is the core payload in the initial steps.
2. **TLS Identity**:
   - **IdentityManager** loads or generates a TLS identity (certificate and private key), a crucial security payload.
3. **Keys Data**:
   - `requestTGTSigningKeys()`, `requestOTTSigningKeys()` return an array of signing keys.
4. **Attestation Data**:
   - `currentAttestationSet()` gets the attestation set.
5. **Metrics**:
   - **CloudMetricsSystem** receives events to trace the process.
6. **Hot Properties**:
   - Updates are handled through `HotPropertiesController`.

---

## Key Components

1. **CloudBoardDaemon**: The central orchestrator.
2. **CloudMetricsSystem**: Handles metric collection and reporting.
3. **StatusMonitor**: Tracks and publishes the status of the daemon.
4. **RequestFielderManager**: Manages a pool of processes for handling requests.
5. **CloudBoardJobHelperXPCClientProvider**: Provides clients for communicating with `cb_jobhelper`.
6. **JobHelperResponseDelegateProvider**: Manages response delegation.
7. **HotPropertiesController**: Handles dynamic configuration updates.
8. **ServiceDiscoveryPublisher**: Registers and discovers services.
9. **HealthMonitor**: Monitors the system's health.
10. **HealthServer**: Provides health check endpoints.
11. **AttestationProvider**: Fetches and manages attestations.
12. **CloudBoardProvider**: Handles gRPC requests and interacts with `cb_jobhelper`.
13. **IdentityManager**: Manages TLS identity.
14. **HeartbeatPublisher**: Publishes heartbeat messages.
15. **WorkloadController**: Manages workload requests.
16. **gRPC Server**: Handles incoming gRPC traffic.
17. **JobQuiescenceMonitor**: Tracks job quiescence.
18. **LaunchdJobHelper**: Cleans up old launchd jobs.
19. **CloudBoardAttestationAPIXPCClient**: Communicates with `cb_attestationd`.
20. **CloudBoardJobAuthAPIXPCClient**: Communicates with `cb_jobauthd`.


```mermaid
sequenceDiagram
    participant CB as CloudBoardDaemon
    participant CM as CloudMetricsSystem
    participant SM as StatusMonitor
    participant RFM as RequestFielderManager
    participant XPC as CloudBoardJobHelperXPCClientProvider
    participant JRP as JobHelperResponseDelegateProvider
    participant HP as HotPropertiesController
    participant SD as ServiceDiscoveryPublisher
    participant HM as HealthMonitor
    participant HS as HealthServer
    participant AP as AttestationProvider
    participant CBProvider as CloudBoardProvider
    participant IM as IdentityManager
    participant Heartbeat as HeartbeatPublisher
    participant WC as WorkloadController
    participant Server as gRPC Server
    participant JQM as JobQuiescenceMonitor
    participant LJH as LaunchdJobHelper
    participant CA as CloudBoardAttestationAPIXPCClient
    participant JA as CloudBoardJobAuthAPIXPCClient
    
    Note right of CB: CloudBoardDaemon Initialization
    CB->>+CM: Initialize
    CB->>+SM: Initialize
    CB->>+RFM: Initialize
    CB->>+XPC: Initialize
    CB->>+JRP: Initialize
    CB->>+HP: Initialize
    CB->>+HM: Initialize
    CB->>+HS: Initialize
    alt ServiceDiscovery Configured
      CB->>SD: Initialize
    end
    alt AttestationProvider not injected
      CB->>CA: Initialize local XPC
      CB->>+AP: Initialize with XPC
    else AttestationProvider injected
       CB->>AP: Initialize with injected provider
    end
    alt Heartbeat Configured
      CB->>+Heartbeat: Initialize
    end
    CB->>+WC: Initialize
    CB->>JQM: Initialize
    CB-->>CB: Configuration Loading and initialization complete.
    Note over CB,JQM: Start operation
    CB->>+LJH: cleanupManagedLaunchdJobs
    activate LJH
    LJH-->>CB: Complete
    deactivate LJH
    CB->>+JQM: startQuiescenceMonitor
    activate JQM
    JQM-->>CB: Complete
    deactivate JQM
    CB->>CB: lifecycleManager.managed{
    CB->>+IM: Initialize
    activate IM
    alt Insecure Listener = false
        IM-->>CB: loadTLSIdentity()
    else Insecure Listener = true
        IM-->>CB: No TLS Identity
    end
    deactivate IM
    alt HotProperties Enabled
        CB->>+HP: info("Hot properties are enabled.")
    else HotProperties Disabled
        CB->>+HP: info("Hot properties are disabled.")
    end
    alt Heartbeats Enabled
        CB->>+Heartbeat: info("Heartbeats are enabled.")
        CB->>Heartbeat: updateCredentialProvider { identityManager.identity?.credential }
    else Heartbeats Disabled
        CB->>+Heartbeat: info("Heartbeats are disabled.")
    end
    CB-->>CB: resolveServiceAddress
    alt Service Discovery Configured
        CB->>SD: Initialize
        CB->>SD: register(localIdentityCallback)
    else Service Discovery Injected
        CB->>SD: Using injected Service Discovery
    else Service Discovery not Configured
        CB->>SD: Service Discovery not enabled
    end
    CB->>HM: init HealthProvider
    CB->>AP: init
    Note right of CB: CloudBoardProvider Initialization
    CB->>+CBProvider: Initialize
    activate CBProvider
    CBProvider-->>CB: Completed
    deactivate CBProvider
    alt requestFielderManager is present
        CB->>RFM: run()
    end
    CB->>AP: run()
    CB->>SM: waitingForFirstAttestationFetch()
    CB->>AP: currentAttestationSet()
    AP-->>CB: Return AttestationSet
    CB->>SM: waitingForFirstKeyFetch()
    alt blockHealthinessOnAuthSigningKeysPresence
        CB->>JA: connect()
        CB->>JA: requestTGTSigningKeys()
        JA-->>CB: Return tgtSigningPublicKeyDERs
        CB->>JA: requestOTTSigningKeys()
        JA-->>CB: Return ottSigningPublicKeyDERs
        alt !tgtSigningPublicKeyDERs.isEmpty & !ottSigningPublicKeyDERs.isEmpty
            Note right of CB: Validated Keys
        else Missing keys
            CB-->>CB: Throw error InitializationError.missingAuthTokenSigningKeys
        end
    else no check
        CB-->>CB: Skip verification
    end
    CB->>SM: waitingForFirstHotPropertyUpdate()
    CB->>CBProvider: run()
    CB->>IM: identityUpdateLoop()
    alt Service Discovery Configured
        CB->>HP: waitForFirstUpdate()
        CB->>SM: waitingForWorkloadRegistration()
        CB->>SD: run()
        SD-->>CB: Service Discovery started
    end
    CB->>HP: waitForFirstUpdate()
    CB->>HM: run()
    CB->>HM: drain()
    CB->>HS: run()
    CB->>Heartbeat: run()
    CB->>+Server: Run gRPC server
    activate Server
    alt Insecure Listener = false
        Server->>Server: GRPCTLSConfiguration.cloudboardProviderConfiguration()
    else Insecure Listener = true
        Server->>Server: insecure(group:)
    end
    Server->>Server: bind()
    Server-->>CB: Server started
    deactivate Server
    alt workloadController is present
        CB->>WC: run(serviceDiscoveryPublisher,concurrentRequestCountStream, providerPause)
        WC-->>CB: workloadController started
    end
    alt hotProperties is present
        CB->>HP: run(metrics)
        HP-->>CB: Hot properties started
    end
    Note over CB: waiting for one of the previous tasks to end
    alt task completed
      CB-->>CB: task completed
      CB->>CB: group.cancelAll()
    end
    CB->>CB: } onDrain:{
    CB->>SM: daemonDrained()
    alt activeRequests present and drainDuration present
        CB->>CM: Emit Metrics.CloudBoardDaemon.DrainCompletionTimeHistogram(drainDuration,activeRequests)
    end
    CB->>JQM: quiesceCompleted()
    CB-->>CB: Continue or exit

```


---

This sequence diagram and explanation should give a comprehensive understanding of how **CloudBoardDaemon** works. Let me know if you have any other questions.


