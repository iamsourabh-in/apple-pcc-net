# Request Flow

The request flow in Private Cloud Compute (PCC) involves several steps, services, and validations to ensure security, privacy, and non-targetability.

**Request Flow Steps:**
*   The Apple Intelligence orchestration layer on the user's device decides to route a request to a server-based model. The modelmanagerd daemon manages Apple Intelligence experiences and uses extensions to service requests for on-device and server-based models. The modelmanagerd is responsible for deciding how to prewarm and execute intelligence requests based on user activity and device context.

*   The user's device authenticates with PCC, and PCC issues the device anonymous access tokens. The client authorizes itself and sends a Token Granting Token (TGT) Request to the PCC Identity Service. The PCC Identity Service verifies that a specific client and device are eligible to use PCC and issues a TGT that serves as proof of eligibility.

*   The user's device submits the request to PCC through the use of a third-party relay and an encrypted request protocol. Apple routes all requests to PCC through a third party to conceal the source IP addresses from the PCC infrastructure. Clients encrypt requests using Hybrid Public Key Encryption (HPKE) and the public key configuration of Apple’s Oblivious Gateway (OG) and send messages to the OG through an Oblivious Relay (OR).

*   Attestations of PCC nodes are validated by the user’s device, which wraps decryption keys for the request to the Request Encryption Key (REK) for these nodes. The PCC Gateway provides the client with attestations, covering the REK, for a set of nodes eligible to serve a request. The client validates the attestations to verify that the nodes are candidates to handle the request.

*   The request is decrypted and handled by a PCC node. CloudBoard manages the node’s lifecycle and incoming requests before passing them off to specific applications that handle LLM processing.

**Services Involved and Their Functions:**

*   **PCC Identity Service:** Verifies client and device eligibility and issues Token Granting Tokens (TGTs).
*   **Token Granting Service (TGS):** Checks TGT validity, up-to-date fraud data, and returns a batch of One-Time Tokens (OTTs) while enforcing rate limits. The TGS uses a Fraud Detection Service (FDS) to make anonymous fraud determinations about transacting entities.
*   **Oblivious Relay (OR):** A third-party service that provides a secure HTTP proxy to conceal the source IP addresses from the PCC infrastructure.
*   **Oblivious Gateway (OG):** Decrypts requests encrypted using Hybrid Public Key Encryption (HPKE).
*   **PCC Gateway:** Provides attestations to the client and routes the request to a specific PCC node. The PCC Gateway searches for an appropriate node to handle the request and determines if client-provided pre-fetched HPKE keys correspond to acceptable nodes.
*   **CloudBoard:** Manages the lifecycle of PCC nodes and incoming requests. CloudBoard creates a SEP-protected Request Encryption Key (REK) pair and uses the CloudAttestation framework to assemble an attestation bundle for this key pair.
*   **The Inference Engine (TIE):** Handles the application-level logic of executing the inference requests that user devices submit to PCC.
*   **CloudAttestation framework:** Assembles attestation bundles and validates the authenticity and integrity of attestations.
*   **Apple Transparency Service:** Provides a transparency log that is publicly auditable and tamper-resistant.

These services work together to ensure requests are handled securely, privately, and verifiably within the PCC framework.
