module Wallet.Tests.SerializationTests

open Consensus
open Hash
open Types
open Transaction
open NUnit.Framework
open FsCheck
open FsCheck.NUnit
open Wallet.Types
open Wallet.Address
open AddressDB.Types
open AddressDB.Serialization
open FsUnit
open Serialization

type AddressDBGenerators =
    static member DBOutputGenerator() =
        gen {
            let! address = Arb.generate<Address>
            let! spend = Arb.generate<Spend>
            let! lock =
                Arb.generate<Lock>
                |> Gen.filter (function
                | HighVLock (identifier, _) -> identifier > 7u // last reserved identifier
                | _ -> true)
            let! outpoint = Arb.generate<Outpoint>
            let! status = Arb.generate<Status>
            let! confirmationStatus = Arb.generate<ConfirmationStatus>

            return
                {
                    address=address
                    spend=spend
                    lock=lock
                    outpoint=outpoint
                    status=status
                    confirmationStatus=confirmationStatus
                }
        }
        |> Arb.fromGen


[<OneTimeSetUp>]
let setup = fun () ->
    Arb.register<Consensus.Tests.ConsensusGenerator>() |> ignore
    Arb.register<AddressDBGenerators>() |> ignore

[<Property(EndSize=10000,MaxTest=1000)>]
let ``Address serialization round trip produces same result`` (value:Address) =
    value
    |> Address.serialize
    |> Address.deserialize = Some value

[<Property(EndSize=10000,MaxTest=1000)>]
let ``DBOutput serialization round trip produces same result`` (value:DBOutput) =
    value
    |> Output.serialize
    |> Output.deserialize = Some value
    
[<Property(EndSize=10000,MaxTest=1000)>]
let ``Account serialization round trip produces same result`` (value:Account) =
    value
    |> Account.serialize
    |> Account.deserialize = Some value    