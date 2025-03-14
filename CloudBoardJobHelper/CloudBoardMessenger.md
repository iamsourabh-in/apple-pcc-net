

```mermaid
sequenceDiagram
    participant CBJobHelper as CloudBoardJobHelper
    participant CBMessenger as CloudBoardMessenger
    participant CBAttestation as CloudBoardAttestationAPIXPCClient
    participant OHTTP as OHTTPServerStateMachine
    participant Metrics as MetricsSystem
    participant Logger as Logger
    participant Keychain as Keychain
    participant SecureEnclave as SecureEnclave
    participant LAContext as LAContext
    participant CBServer as CloudBoardJobHelperAPIServerToClientProtocol
    participant EncodedRequestContinuation as AsyncStream<PipelinePayload<Data>>.Continuation
    participant ResponseStream as AsyncStream<FinalizableChunk<Data>>

    Note over CBJobHelper,CBMessenger: Initialization & Setup
    CBJobHelper->>CBMessenger: init(attestationClient, server, encodedRequestContinuation, responseStream, metrics)
    activate CBMessenger
    CBMessenger->>CBAttestation: CloudBoardAttestationAPIXPCClient.localConnection() (optional)
    activate CBAttestation
    CBMessenger->>CBMessenger: Initialize logger
    CBMessenger->>CBMessenger: Initialize OHTTPKeyState = .initialized
    CBMessenger->>CBMessenger: laContext = LAContext()
    CBMessenger->>Metrics: Initialize metrics system
    deactivate CBAttestation
    deactivate CBMessenger

    Note over CBJobHelper,CBMessenger: Run Process Begins
    CBJobHelper->>CBMessenger: run()
    activate CBMessenger
    CBMessenger->>CBAttestation: set(delegate: self)
    activate CBAttestation
    deactivate CBAttestation
    CBMessenger->>CBAttestation: connect()
    activate CBAttestation
    deactivate CBAttestation
    CBMessenger->>CBAttestation: requestAttestedKeySet()
    activate CBAttestation
    CBAttestation-->>CBMessenger: AttestedKeySet {currentKey, unpublishedKeys}
    Note right of CBMessenger: KeySet contains Attested keys (KeyChain or direct)
    deactivate CBAttestation
    CBMessenger->>CBMessenger: Array(keySet:keySEt,laContext)
    activate CBMessenger
    CBMessenger->>CBMessenger: Append currentKey
    activate CBMessenger
    CBMessenger->>Keychain: fetchKey(persistentRef) (If needed)
    activate Keychain
    Keychain-->>CBMessenger: secKey
    deactivate Keychain
    CBMessenger->>SecureEnclave:  Curve25519.KeyAgreement.PrivateKey(from:,authenticationContext:) (if needed)
    activate SecureEnclave
    SecureEnclave-->>CBMessenger: privateKey
    deactivate SecureEnclave
    CBMessenger-->>CBMessenger: CachedAttestedKey
    deactivate CBMessenger
    alt unpublishedKeys loop
        CBMessenger->>CBMessenger: Append unpublishedKey
        activate CBMessenger
        CBMessenger->>Keychain: fetchKey(persistentRef) (If needed)
        activate Keychain
        Keychain-->>CBMessenger: secKey
        deactivate Keychain
         CBMessenger->>SecureEnclave:  Curve25519.KeyAgreement.PrivateKey(from:,authenticationContext:) (if needed)
        activate SecureEnclave
        SecureEnclave-->>CBMessenger: privateKey
        deactivate SecureEnclave
        CBMessenger-->>CBMessenger: CachedAttestedKey
        deactivate CBMessenger
    end
    CBMessenger-->>CBMessenger: [CachedAttestedKey] keys
    deactivate CBMessenger
    CBMessenger->>CBMessenger: set ohttpKeyState = .available(keys)
    CBMessenger->>Logger: log("Received OHTTP node keys from attestation daemon")

    Note over CBJobHelper,CBMessenger: Workload Request Flow
    CBJobHelper->>CBMessenger: invokeWorkloadRequest(request)
    activate CBMessenger
    CBMessenger->>Metrics: emit(TotalRequestsReceivedCounter)
    alt request is .warmup
        CBMessenger->>Logger: log("Received warmup data")
        CBMessenger->>EncodedRequestContinuation: yield(.warmup(warmupData))
    else request is .requestChunk
        CBMessenger->>Logger: log("Received request chunk")
        CBMessenger->>OHTTP: receiveChunk(encryptedPayload, isFinal)
        activate OHTTP
        OHTTP-->>CBMessenger: data
        deactivate OHTTP
        CBMessenger->>Metrics: emit(RequestChunkReceivedSizeHistogram)
        CBMessenger->>EncodedRequestContinuation: yield(.chunk(data, isFinal))
    else request is .parameters
        CBMessenger->>Logger: log("Received request parameters")
        CBMessenger->>CBMessenger: requestTrackingID = parameters.requestID
        CBMessenger->>EncodedRequestContinuation: yield(.oneTimeToken(parameters.oneTimeToken))
        CBMessenger->>EncodedRequestContinuation: yield(.parameters(parameters))
        CBMessenger->>CBMessenger: invokePrivateKeyRequest(keyID, wrappedKey)
        activate CBMessenger
         CBMessenger->>CBMessenger: ohttpKeys
        activate CBMessenger
        alt  ohttpKeyState is .initialized
                CBMessenger->>CBMessenger: log("Waiting for OHTTP keys to become available")
                CBMessenger->>CBMessenger: ohttpKeyState = .awaitingKeys(promise)
                CBMessenger->>CBMessenger: await Future(promise).valueWithCancellation
        else ohttpKeyState is .awaitingKeys
             CBMessenger->>CBMessenger: log("Waiting for OHTTP keys to become available")
             CBMessenger->>CBMessenger: await Future(promise).valueWithCancellation
        end
        CBMessenger-->>CBMessenger:[CachedAttestedKey]
        deactivate CBMessenger
         CBMessenger->>CBMessenger: Find Attested key by keyID
          alt key not found
             CBMessenger->>Logger:log("No node key found with the provided key ID")
             CBMessenger->>CBServer: sendWorkloadResponse(.failureReport)
             CBMessenger-->>CBJobHelper: error
          else key found
               CBMessenger->>Logger:log("Found attested key for request")
                alt key expired
                    CBMessenger->>Logger:log("Provided key has expired")
                     CBMessenger->>CBServer: sendWorkloadResponse(.failureReport)
                     CBMessenger-->>CBJobHelper: error
                else key not expired
                    CBMessenger->>OHTTP: receiveKey(wrappedKey, privateKey)
                    activate OHTTP
                    OHTTP-->>CBMessenger: chunks, encapsulator
                    deactivate OHTTP
                    CBMessenger->>CBMessenger: responseEncapsulator = encapsulator
                   loop for chunk in chunks
                        CBMessenger->>EncodedRequestContinuation: yield(.chunk(chunk))
                   end
                end
          end
        CBMessenger-->>CBMessenger: Void
        deactivate CBMessenger
    end
    CBMessenger-->>CBJobHelper: Void
    deactivate CBMessenger
    
    Note over CBJobHelper,CBMessenger: Response Stream Processing
    loop responseStream
        ResponseStream-->>CBMessenger: response (FinalizableChunk<Data>)
        activate CBMessenger
        CBMessenger->>Metrics: emit(TotalResponseChunksReceivedCounter)
        CBMessenger->>Metrics: emit(TotalResponseChunkReceivedSizeHistogram)
        CBMessenger->>Logger: log("Received response")
        alt responseEncapsulator == nil
            CBMessenger->>Logger: log("Buffering encoded response until OHTTP encapsulation is set up")
            CBMessenger->>CBMessenger: append(response) to bufferedResponses
            CBMessenger->>Metrics: emit(TotalResponseChunksInBuffer)
            CBMessenger->>Metrics: emit(TotalResponseChunksBufferedSizeHistogram)
        else responseEncapsulator != nil
             alt bufferedResponses is not empty
                    loop through bufferedResponses + [response]
                         CBMessenger->>CBMessenger: responseEncapsulator.encapsulate(chunk, final:isFinal)
                        activate CBMessenger
                        CBMessenger-->>CBMessenger:responseMessage
                        deactivate CBMessenger
                        CBMessenger->>Logger: log("Sending encapsulated response")
                        CBMessenger->>CBServer: sendWorkloadResponse(.responseChunk(encryptedPayload:responseMessage,isFinal))
                        activate CBServer
                        CBServer-->>CBMessenger: Void
                         deactivate CBServer
                         CBMessenger->>Metrics: emit(TotalResponseChunksSentCounter)
                         alt isFinal
                             CBMessenger->>CBMessenger: receivedFinal = true
                         end
                    end
                 CBMessenger->>CBMessenger: bufferedResponses = []
                  CBMessenger->>Metrics: emit(TotalResponseChunksInBuffer)
             else bufferedResponses is empty
                 CBMessenger->>CBMessenger: responseEncapsulator.encapsulate(chunk, final:isFinal)
                  activate CBMessenger
                    CBMessenger-->>CBMessenger:responseMessage
                    deactivate CBMessenger
                    CBMessenger->>Logger: log("Sending encapsulated response")
                    CBMessenger->>CBServer: sendWorkloadResponse(.responseChunk(encryptedPayload:responseMessage,isFinal))
                     activate CBServer
                     CBServer-->>CBMessenger: Void
                      deactivate CBServer
                     CBMessenger->>Metrics: emit(TotalResponseChunksSentCounter)
                     alt isFinal
                         CBMessenger->>CBMessenger: receivedFinal = true
                      end
             end
        end
        deactivate CBMessenger
    end

    Note over CBMessenger: responseStream ended
    CBMessenger->>EncodedRequestContinuation: finish()
    alt !receivedFinal
        CBMessenger->>Logger: log("Encoded response stream finished without final response chunk")
    else receivedFinal
        CBMessenger->>Logger: log("Finished encoded response stream")
    end

    Note over CBJobHelper,CBMessenger: Key Rotation
    CBAttestation-->>CBMessenger: keyRotated(newKeySet)
    activate CBMessenger
    CBMessenger->>CBMessenger: Array(keySet: newKeySet, laContext)
    activate CBMessenger
    CBMessenger->>Keychain: fetchKey(persistentRef)
    activate Keychain
    Keychain-->>CBMessenger: secKey
    deactivate Keychain
    CBMessenger->>SecureEnclave: Curve25519.KeyAgreement.PrivateKey(from:,authenticationContext:)
    activate SecureEnclave
    SecureEnclave-->>CBMessenger: privateKey
    deactivate SecureEnclave
    CBMessenger-->>CBMessenger: [CachedAttestedKey]
    deactivate CBMessenger
    CBMessenger->>CBMessenger: self.ohttpKeyState = .available(newKeys)
    deactivate CBMessenger
    
    Note over CBJobHelper,CBMessenger: Teardown Process
    CBJobHelper->>CBMessenger: teardown()
    activate CBMessenger
    CBMessenger->>EncodedRequestContinuation: yield(.teardown)
    deactivate CBMessenger

    Note over CBJobHelper,CBMessenger: Surprise Disconnect
    CBAttestation-->>CBMessenger: surpriseDisconnect()
    activate CBMessenger
    CBMessenger->>Logger: log("Unexpectedly disconnected from cb_attestationd")
    deactivate CBMessenger

    Note over CBMessenger: Deinit called.
    CBMessenger->>CBMessenger: Check ohttpKeyState.awaitingKeys
    alt .awaitingKeys
        CBMessenger->>CBMessenger: promise.fail(with: CloudBoardMessengerError.runNeverCalled)
    end

```