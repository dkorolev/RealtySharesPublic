module EntryPoint

open PHPModule

open System
open FSharp.Data
open FsUnit
open NUnit.Framework
open Newtonsoft.Json

[<EntryPoint>]
let main argv =
    match phpAdd 2 2 with
    | Some v -> printfn "OK: %d" v.sum
    | None -> printfn "Error."
    0
