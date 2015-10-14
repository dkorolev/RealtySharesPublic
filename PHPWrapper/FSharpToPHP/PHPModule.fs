module PHPModule

open System
open FSharp.Data
open Newtonsoft.Json

let port = 8000

type AddRequest = {
    a : int
    b : int
}

type SumResponse = {
    sum : int
}

type ErrorResponse = {
    error : string
}

let phpAdd a b =
    printfn "Adding %d and %d using the PHP server." a b
    let response = (Http.RequestString((sprintf "http://localhost:%d/add" port),
                                       httpMethod = "POST",
                                       body = (TextRequest (JsonConvert.SerializeObject({ a =a ; b = b })))))
    try (Some (JsonConvert.DeserializeObject<SumResponse> response))
    with :? JsonException -> None
