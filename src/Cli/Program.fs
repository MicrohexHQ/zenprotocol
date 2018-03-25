﻿open Argu
open System
open FSharp.Data
open Api.Types

type ImportArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] Import_Arguments of mnemonicSentence:string list
    interface IArgParserTemplate with
        member arg.Usage = ""

type SpendArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] Spend_Arguments of asset:string * assetType:string * amount:int64 * address:string
    interface IArgParserTemplate with
        member arg.Usage = ""

type ActivateContractArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] ActivateContract_Arguments of file:string * numberOfBlocks:int
    interface IArgParserTemplate with
        member arg.Usage = ""

type ExecuteContractArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] ExecuteContract_Arguments of address:string * command:string * data:string * asset:string * assetType:string * amount:int64
    interface IArgParserTemplate with
        member arg.Usage = ""

type PublishBlockArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] PublishBlock_Arguments of block:string
    interface IArgParserTemplate with
        member arg.Usage = ""

type AccountExistsArgs =
    | [<MainCommand("COMMAND");ExactlyOnce>] AccountExists_Arguments
    interface IArgParserTemplate with
        member arg.Usage = ""

type NoArgs =
    | [<Hidden>] NoArg
    interface IArgParserTemplate with
        member arg.Usage = "get address"

type Arguments =
    | [<UniqueAttribute>] Port of port:uint16
    | [<AltCommandLine("-l1")>] Local1
    | [<AltCommandLine("-l2")>] Local2
    | [<CliPrefix(CliPrefix.None)>] Balance of ParseResults<NoArgs>
    | [<CliPrefix(CliPrefix.None)>] History of ParseResults<NoArgs>
    | [<CliPrefix(CliPrefix.None)>] Address of ParseResults<NoArgs>
    | [<CliPrefix(CliPrefix.None)>] Import of ParseResults<ImportArgs>
    | [<CliPrefix(CliPrefix.None)>] Spend of ParseResults<SpendArgs>
    | [<CliPrefix(CliPrefix.None)>] Activate of ParseResults<ActivateContractArgs>
    | [<CliPrefix(CliPrefix.None)>] Execute of ParseResults<ExecuteContractArgs>
    | [<CliPrefix(CliPrefix.None)>] PublishBlock of ParseResults<PublishBlockArgs>
    | [<CliPrefix(CliPrefix.None)>] AccountExists of ParseResults<NoArgs>
    interface IArgParserTemplate with
        member arg.Usage =
            match arg with
            | Port _ -> "port of zen-node API"
            | Local1 -> "use port of local1 testing node"
            | Local2 -> "use port of local2 testing node"
            | Balance _ -> "get wallet balance"
            | History _ -> "get wallet transactions"
            | Address _ -> "get wallet address"
            | Import _ -> "import wallet seed from mnemonic sentence"
            | Spend _ -> "send asset to an address"
            | Activate _ -> "activate contract"
            | Execute _ -> "execute contract"
            | PublishBlock _ -> "publish block to the network"
            | AccountExists _ -> "check for an existing account" 

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Arguments>(programName = "zen-ctl", errorHandler = errorHandler)

    let results = parser.ParseCommandLine argv

    let mutable port = 31567us

    List.iter (fun arg ->
        match arg with
        | Port p -> port <- p
        | Local1 -> port <- 36000us
        | Local2 -> port <- 36001us
        | _ -> ()
        ) (results.GetAllResults())

    printfn ""

    let getUri =
        sprintf "http://127.0.0.1:%d/%s" port

    match results.TryGetSubCommand() with
    | Some (Spend args) ->
        let asset,assetType,amount,address = args.GetResult <@ Spend_Arguments @>
        let send = new SpendRequestJson.Root(address, new SpendRequestJson.Spend(asset, assetType, amount))

        let response = send.JsonValue.Request (getUri "wallet/spend")

        match response.StatusCode, response.Body with
        | 200,_ -> printfn "Success"
        | code, HttpResponseBody.Text text -> printfn "Failed %d %s" code text
        | code,_ -> printfn "Failed %d with binary response" code
    | Some (Balance _) ->
        let balance =
            BalanceResponseJson.Load(getUri "wallet/balance")

        printfn "Asset\t\t| Balance"
        printfn "============================"

        Array.iter (fun (assertBalance:BalanceResponseJson.Root) ->
            printfn " %s %s\t| %d" assertBalance.Asset assertBalance.AssetType assertBalance.Balance) balance
    | Some (History _) ->
        let transactions =
            TransactionsResponseJson.Load(getUri "wallet/transactions")

        printfn "TxHash\t| Asset\t| Amount"
        printfn "=========================================="

        Array.iter (fun (transaction:TransactionsResponseJson.Root) ->
            printfn "\n%s" transaction.TxHash

            Array.iter (fun (amount:TransactionsResponseJson.Delta) ->
                printfn "\t| %s %s\t| %d" amount.Asset amount.AssetType amount.Amount
            ) transaction.Deltas  
            
        ) transactions
    | Some (Address _) ->
        let address =
            AddressJson.Load (getUri "wallet/address")

        printfn "%s" address.Address
    | Some (Import args) ->
        let words = args.GetResult <@ Import_Arguments @>
        let send = new ImportSeedJson.Root(words |> List.toArray)
        
        let response = send.JsonValue.Request (getUri "wallet/import")

        match response.StatusCode, response.Body with
        | 200,_ -> printfn "Success"
        | code, HttpResponseBody.Text text -> printfn "Failed %d %s" code text
        | code,_ -> printfn "Failed %d with binary response" code
    | Some (Activate args) ->
        let file,numberOfBlocks = args.GetResult <@ ActivateContract_Arguments @>

        match System.IO.File.Exists file with
            | false ->
                printfn "File not found: %s" file
            | true ->
                let code = System.IO.File.ReadAllText file
                let activate = new ContractActivateRequestJson.Root(code,numberOfBlocks)

                let response = activate.JsonValue.Request (getUri "wallet/contract/activate")

                match response.StatusCode, response.Body with
                    | 200,_ -> printfn "Success %A" response.Body
                    | code, HttpResponseBody.Text text -> printfn "Failed %d %s" code text
                    | code,_ -> printfn "Failed %d with binary response" code
    | Some (Execute args) ->
        let address,command,data,asset,assetType,amount = args.GetResult <@ ExecuteContract_Arguments @>
        let execute = new ContractExecuteRequestJson.Root(address,command,data, [| new ContractExecuteRequestJson.Spend(asset, assetType, amount) |])

        let response = execute.JsonValue.Request (getUri "wallet/contract/execute")

        match response.StatusCode, response.Body with
            | 200,_ -> printfn "Success"
            | code, HttpResponseBody.Text text -> printfn "Failed %d %s" code text
            | code,_ -> printfn "Failed %d with binary response" code
    | Some (PublishBlock args) ->
        let block = args.GetResult <@ PublishBlock_Arguments @>
        let publishBlock = new PublishBlockJson.Root(block)
        let response = publishBlock.JsonValue.Request (getUri "block/publish")

        match response.StatusCode, response.Body with
        | 200,_ -> printfn "Success"
        | code, HttpResponseBody.Text text -> printfn "Failed %d %s" code text
        | code,_ -> printfn "Failed %d with binary response" code
    | Some (AccountExists _) ->
        let result =
            AccountExistsResponseJson.Load(getUri "wallet/exists")

        if result.AccountExists then
            printfn "Account is initialized"
        else
            printfn "Account is not initialized"
    | _ -> ()

    printfn ""

    0 // return an integer exit code
