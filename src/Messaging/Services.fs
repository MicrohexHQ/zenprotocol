module Messaging.Services

open Consensus
open Types
open Hash
open Zen.Types.Data
open Infrastructure.ServiceBus.Client

module Blockchain =
    let serviceName = "blockchain"

    type ActiveContract = {
        contractHash:Hash
        code:string
        expiry:uint32
    }

    type BlochChainInfo = {
        chain:string
        blocks:uint32
        headers:uint32
        difficulty:float
        medianTime:uint64
        initialBlockDownload:bool
    }

    type Command =
        | ValidateTransaction of Types.Transaction
        | RequestMemPool of peerId:byte[]
        | RequestTransaction of peerId:byte[] * txHash:Hash
        | RequestBlock of peerId:byte[] * blockHash:Hash
        | RequestTip of peerId:byte[]
        | HandleMemPool of peerId:byte[] * Hash list
        | HandleTip of peerId:byte[] * Types.BlockHeader
        | ValidateNewBlockHeader of peerId:byte[] * Types.BlockHeader
        | ValidateBlock of peerId:byte[] * Types.Block
        | ValidateMinedBlock of Types.Block
        | RequestHeaders of peerId:byte[] * startBlockHashes:Hash list * endBlockHash :Hash
        | HandleHeaders of peerId:byte[] * BlockHeader list
        | HandleNewTransaction of peerId:byte[] * txHash:Hash

    type Request =
        | ExecuteContract of Hash * string * Crypto.PublicKey option * data option * TxSkeleton.T
        | GetBlockTemplate of pkHash:Hash
        | GetTip
        | GetBlock of Hash
        | GetBlockHeader of Hash
        | GetActiveContracts
        | GetBlockChainInfo
        | GetHeaders
        | GetMempool

    type Response = unit

    let validateTransaction client tx =
        Command.send client serviceName (ValidateTransaction tx)

    let requestMemPool client peerId =
        Command.send client serviceName (RequestMemPool peerId)

    let requestTransaction client peerId txHash =
        Command.send client serviceName (RequestTransaction (peerId,txHash))

    let handleMemPool client peerId txHashes =
        Command.send client serviceName (HandleMemPool (peerId,txHashes))

    let executeContract client cHash command sender data txSkeleton =
        ExecuteContract (cHash,command, sender, data, txSkeleton)
        |> Request.send<Request, Result<Transaction,string>> client serviceName

    let validateBlock client peerId block =
        ValidateBlock (peerId, block)
        |> Command.send client serviceName

    let validateMinedBlock client block =
        ValidateMinedBlock block
        |> Command.send client serviceName

    let handleTip client peerId header =
        HandleTip (peerId, header)
        |> Command.send client serviceName

    let validateNewBlockHeader client peerId header =
        ValidateNewBlockHeader (peerId,header)
        |> Command.send client serviceName

    let requestBlock client peerId blockHash =
        RequestBlock (peerId,blockHash)
        |> Command.send client serviceName

    let requestTip client peerId =
        RequestTip peerId
        |> Command.send client serviceName

    let getBlockTemplate client pkHash =
        GetBlockTemplate pkHash
        |> Request.send<Request,Block> client serviceName

    let getBlockHeader client blockHash =
        Request.send<Request,BlockHeader option> client serviceName (GetBlockHeader blockHash)

    let getBlock client blockHash =
        Request.send<Request,Block option> client serviceName (GetBlock blockHash)

    let getTip client =
        Request.send<Request,(Hash*BlockHeader) option> client serviceName GetTip

    let getActiveContracts client =
        Request.send<Request,ActiveContract list> client serviceName GetActiveContracts

    let getBlockChainInfo client =
        Request.send<Request,BlochChainInfo> client serviceName GetBlockChainInfo

    let requestHeaders client peerId startBlockHash endBlockHash=
        RequestHeaders (peerId,startBlockHash,endBlockHash) |> Command.send client serviceName

    let handleHeaders client peerId headers =
        HandleHeaders (peerId,headers) |> Command.send client serviceName

    let getHeaders client =
        GetHeaders |> Request.send<Request, BlockHeader list> client serviceName

    let getMempool client =
        GetMempool |> Request.send<Request, (Hash.Hash * Transaction) list> client serviceName

    let handleNewTransaction client peerId txHash =
        HandleNewTransaction (peerId,txHash)
        |> Command.send client serviceName

module Network =
    type Command =
        | SendMemPool of peerId:byte[] * Hash list
        | SendTransaction of peerId:byte[] * Transaction
        | SendTip of peerId:byte[] * BlockHeader
        | SendBlock of peerId:byte[] * Block
        | GetTransaction of peerId:byte[] * Hash
        | GetBlock of Hash
        | GetBlockFrom of peerId:byte[] * Hash
        | PublishBlock of BlockHeader
        | GetHeaders of peerId:byte[] * from:Hash list * toHash:Hash
        | SendHeaders of peerId:byte[] * BlockHeader list
        | DisconnectPeer of peerId:byte[]
        | GetTipFromAllPeers

    type Request =
        GetConnectionCount

    type Response = unit

    let serviceName = "network"

    let getConnectionCount client =
        Request.send<Request, uint32> client serviceName GetConnectionCount

module Wallet =
    type BalanceResponse = Map<Asset,uint64>
    type TransactionsResponse = List<Hash*Map<Asset,int64>>
    type ActivateContractResponse = Transaction * Hash

    type Command =
        | Resync

    type Request =
        | GetAddressPKHash
        | GetAddress
        | GetTransactions
        | GetBalance
        | ImportSeed of string list * password:string
        | Send of Hash * Spend * password:string
        | ActivateContract of string * uint32 * password:string
        | ExtendContract of Hash * uint32 * password:string
        | ExecuteContract of Hash * string * data option * provideReturnAddress:bool * sign:string option * Map<Asset, uint64> * password:string
        | AccountExists
        | CheckPassword of password:string
        | GetPublicKey of string * password:string
        | GetMnemonicPhrase of password:string

    let serviceName = "wallet"

    //TODO: apply same convention to other services
    let private send<'a> = Request.send<Request, Result<'a,string>>

    let getBalance client =
        send<BalanceResponse> client serviceName GetBalance

    let getAddressPKHash client =
        send<Hash> client serviceName GetAddressPKHash

    let getAddress client =
        send<string> client serviceName GetAddress

    let createTransaction client address spend password =
        send<Transaction> client serviceName (Send (address, spend, password))

    let activateContract client code numberOfBlocks password =
        send<ActivateContractResponse> client serviceName (ActivateContract (code, numberOfBlocks, password))

    let extendContract client address numberOfBlocks password =
        send<Transaction> client serviceName (ExtendContract (address, numberOfBlocks, password))

    let executeContract client address command data provideReturnAddress sign spends password =
        send<Transaction> client serviceName (ExecuteContract (address, command, data, provideReturnAddress, sign, spends, password))

    let importSeed client words password =
        send<unit> client serviceName (ImportSeed (words, password))

    let getTransactions client =
        send<TransactionsResponse> client serviceName GetTransactions

    let accountExists client =
        send<bool> client serviceName AccountExists

    let checkPassword client password =
        send<bool> client serviceName (CheckPassword password)

    let resyncAccount client =
        Command.send client serviceName Resync

    let getPublicKey client path password =
        Request.send<Request, Result<Crypto.PublicKey,string>> client serviceName (GetPublicKey (path, password))

    let getMnemonicPhrase client password =
        Request.send<Request, Result<string, string>> client serviceName (GetMnemonicPhrase password)