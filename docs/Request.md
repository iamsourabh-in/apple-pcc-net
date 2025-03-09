@startuml
actor Client
participant CloudBoardD
participant CloudBoardJobHelper
participant CloudBoardJobAuthD
participant CloudBoardAttestationD
participant CloudBoardAppWorkload
participant launchd
participant Workload

Client -> CloudBoardD: Job Request
activate CloudBoardD
CloudBoardD -> CloudBoardJobHelper: Job Request
activate CloudBoardJobHelper
CloudBoardJobHelper -> CloudBoardJobAuthD: Get Keys
activate CloudBoardJobAuthD
CloudBoardJobAuthD --> CloudBoardJobHelper: Keys
deactivate CloudBoardJobAuthD
CloudBoardJobHelper -> CloudBoardAttestationD: Get Attestation
activate CloudBoardAttestationD
CloudBoardAttestationD --> CloudBoardJobHelper: Attestation
deactivate CloudBoardAttestationD
CloudBoardJobHelper -> CloudBoardAppWorkload: Start Workload
activate CloudBoardAppWorkload
CloudBoardAppWorkload -> launchd: Start Process
activate launchd
launchd -> Workload: Execute
activate Workload
Workload --> CloudBoardAppWorkload: Result
deactivate Workload
deactivate launchd
CloudBoardAppWorkload --> CloudBoardJobHelper: Result
deactivate CloudBoardAppWorkload
CloudBoardJobHelper --> CloudBoardD: Result
deactivate CloudBoardJobHelper
CloudBoardD -> Client: Result
deactivate CloudBoardD
@enduml