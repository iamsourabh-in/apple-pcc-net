
# Client

To interact with Private Cloud Compute (PCC), the client performs several actions, including authenticating with PCC, submitting requests, and validating attestations. The PCC request flow initiates from the user's device, which serves as the foundation for all security and privacy policies within PCC.



Here's a detailed breakdown of the actions a client performs:

*   **Submitting a request to PCC**: The user's device validates attestation bundles provided by PCC and decides whether to trust the attestations and wrap the encryption key for the request to that PCC node. The client generates a unique symmetric Data Encryption Key (DEK) for each request, which it uses to encrypt the request payload. The client then assembles the request by encrypting the body with the DEK and constructing a plain text envelope with any metadata required to route the request.
*   **Client authentication**: The client authorizes itself and sends a Token Granting Token (TGT) Request to the PCC Identity Service. The client sends the TGT to the Token Granting Service, along with a Batch Request for One-Time Tokens (OTT). For each request to PCC, the client attaches one OTT as proof that it is authorized by the Token Granting Service to use PCC.
*   **Network transport**: The client encrypts requests using Hybrid Public Key Encryption (HPKE) and the public key configuration of Apple’s Oblivious Gateway (OG). The client randomly selects among multiple Oblivious Relays (ORs) operated by distinct third parties.
*   **Node validation**: The client validates attestations provided by the PCC Gateway to verify that the nodes are candidates to handle the request. If any pre-fetched and validated node attestations are available, the client provides the symmetric keys wrapped to those nodes as a sidecar. The client verifies each attestation provided by the PCC Gateway. If valid, the client provides the PCC Gateway with an HPKE-wrapped copy of the DEK via the same connection; otherwise, the client rejects the node.
*   **Transparency**: The keys advertised to clients are published to the transparency log, and clients confirm that the keys they receive have been recorded in the transparency log.
*   **Prefetching attestations**: Often overnight, the user’s device makes a query using Oblivious HTTP to the PCC Gateway to prefetch a set of attestations that can be used later in inference queries. The client uses each attestation returned in a prefetch call only once before requiring a new prefetch call.
*   **Apple Intelligence orchestration**: When the user invokes an Apple Intelligence feature, a prewarming step may be initiated to ensure that the device is ready to handle any intelligence requests. If needed, privatecloudcomputed executes its authentication flow and will prefetch attestations.
*   **Analytics Data Sharing**: For users who opt-in to sharing analytics data with Apple, the PCC client reports statistics that describe the flatness of the distribution of PCC nodes provided by the PCC Gateway.
