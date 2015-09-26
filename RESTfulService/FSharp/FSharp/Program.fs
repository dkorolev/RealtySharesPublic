open System
open Suave
open HttpClient
open FsUnit
open NUnit.Framework

let port = 8083

let service_routes = Http.Successful.OK "OK!\n"

let spawn_server routes = 
    let cancel = new Threading.CancellationTokenSource()
    let config = { Web.defaultConfig with cancellationToken = cancel.Token }
    let run_async async_server = ignore <| Async.StartAsTask(async_server, cancellationToken = cancel.Token)
    Web.startWebServerAsync config routes |> snd |> run_async
    cancel

[<TestFixture>]
type UnitTest() = 
    [<Test>]
    member this.ReturnsOK() =
        let server = spawn_server service_routes
        "OK!\n" |> should equal <| (createRequest Get (sprintf "http://localhost:%d/" port) |> getResponseBody)
        server.Dispose() |> ignore

[<EntryPoint>]
let main argv =
    let server = spawn_server service_routes
    printfn "Server running on port %d. Press enter to stop it." port
    System.Console.ReadLine() |> ignore
    server.Dispose() |> ignore
    0
