open System
open Suave
open Suave.Http
open Suave.Http.Writers
open Suave.Types
open Suave.Http.Applicatives
open FSharp.Data
open FsUnit
open NUnit.Framework
open Newtonsoft.Json
open Microsoft.FSharp.Reflection

let inline ToJSON o = JsonConvert.SerializeObject(o)

// Data dictionary for API requests, HTTP level.
// `GET_*` fields will be parsed from the URL query string.
// `POST_*` fields are parsed as a discriminated union type, with the type itself taken from the URL.
type APIEndpoint =
    | GET_add of AddRequestGET
    | GET_subtract of SubtractRequestGET
    | GET_concatenate of ConcatenateRequestGET
    | POST_multiply of MultiplyRequestPOST
    | POST_divide of DivideRequestPOST
    | POST_repeat of RepeatRequestPOST
    | JSON_parse_error of string
    static member FromJSON(json : string) =
        try JsonConvert.DeserializeObject<APIEndpoint> json with :? JsonException -> JSON_parse_error json
and AddRequestGET = {
    a : int
    b : int
}
and SubtractRequestGET = {
    a : int
    b : int
}
and ConcatenateRequestGET = {
    p : string
    q : string
}
and MultiplyRequestPOST = {
    x : int
    y : int
}
and DivideRequestPOST = {
    x : int
    y : int
}
and RepeatRequestPOST = {
    s : string
    n : int
}

// Data dictionary for API responses, HTTP level.
type APIResponse =
    | BadRequest of BadRequest
    | AddResponse of AddResponse
    | SubtractResponse of SubtractResponse
    | ConcatenateResponse of ConcatenateResponse
    | MultiplyResponse of MultiplyResponse
    | DivideResponse of DivideResponse
    | RepeatResponse of RepeatResponse
and BadRequest = {
    error : string
}
and AddResponse = {
    sum : int
}
and SubtractResponse = {
    result : int
}
and ConcatenateResponse = {
    concatenated : string
}
and MultiplyResponse = {
    product : int
}
and DivideResponse = {
    result : double
}
and RepeatResponse = {
    result : string
}

// Business logic.
let run_api_call (data : APIEndpoint) : APIResponse =
    match data with
    | GET_add x -> AddResponse { sum = (x.a + x.b) }
    | GET_subtract x -> SubtractResponse { result = (x.a - x.b) }
    | GET_concatenate x -> ConcatenateResponse { concatenated = (x.p + x.q) }
    | POST_multiply x -> MultiplyResponse { product = (x.x * x.y) }
    | POST_divide x ->
        if x.y <> 0
        then DivideResponse { result = (double(x.x) / double(x.y)) }
        else BadRequest { error = "Parameter 'y' should not be zero." }
    | POST_repeat x ->
        if x.n >= 1
        then RepeatResponse { result = Seq.map (fun _ -> x.s) [1 .. x.n] |> Seq.reduce (+) }
        else BadRequest { error = "Parameter 'n' should be positive." }
    | _ -> BadRequest { error = "Not implemented." }

let format_api_response (response : APIResponse) =
    match response with
    | BadRequest error -> RequestErrors.BAD_REQUEST <| ToJSON error
    // All responses of specific types are returned as plain JSON-s of their underlying data.
    | AddResponse x -> Successful.OK <| ToJSON x
    | ConcatenateResponse x -> Successful.OK <| ToJSON x
    | SubtractResponse x -> Successful.OK <| ToJSON x
    | MultiplyResponse x -> Successful.OK <| ToJSON x
    | DivideResponse x -> Successful.OK <| ToJSON x
    | RepeatResponse x -> Successful.OK <| ToJSON x
    // The final route matches any `APIResponse`, will return it with `{"Case":"...","Fields":[...]}`.
    // It is commented out now, since all possible response types are handled above.
    // | response -> Successful.OK <| ToJSON response

// Server configuration.
let port = 8083

type ParsedField = IntField of string | StringField of string | Error
type ParsedFieldsList = ParsedField list

let app =
    // Demo endpoints, playing around Suave.
    let demo_endpoints = [
        path "/demo_get" >>= choose [
            GET >>= Successful.OK "OK"
            RequestErrors.METHOD_NOT_ALLOWED "METHOD_NOT_ALLOWED"
        ]
        GET >>= pathScan "/demo_add/%d/%d" (fun (a, b) -> Successful.OK (sprintf "%d+%d=%d" a b (a + b)))
        path "/demo_post" >>= choose [
            POST >>= request (fun r ->
                if r.rawForm.Length > 0
                then
                    let body =
                        sprintf
                            "LENGTH=%d, BODY='%s'"
                            r.rawForm.Length
                            (System.Text.Encoding.ASCII.GetString(r.rawForm))
                    Successful.OK body
                else
                    never)
            RequestErrors.METHOD_NOT_ALLOWED "METHOD_NOT_ALLOWED"
        ]
        request (fun r -> 
            match r.queryParam "demo_url_parameter" with
            | Choice1Of2 foo -> Successful.OK ("demo_url_parameter=" + foo)
            | _ -> never)
    ]
  
    // API endpoints.
    let send_api_response (request : APIEndpoint) =
        setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
        >>= setHeader "Pragma" "no-cache"
        >>= setHeader "Expires" "0"
        >>= setMimeType "application/json; charset=utf-8"
        >>= format_api_response (run_api_call request)

    // Populate the list of GET and POST endpoints by reflecting on the `APIEndpoint` type.
    // `GET_*` cases become GET endpoints, parsing `int` and `string` parameters from URL query string,
    // `POST_*` cases become POST endpoints, parsing the BODY as a JSON of the respective type.
    let api_endpoints = Array.toList <| (FSharpType.GetUnionCases typeof<APIEndpoint> |> Array.map (fun case ->
        match case.Name with
        | "JSON_parse_error" -> [], []
        | _ -> 
                let extract_fields (property : Reflection.PropertyInfo) : ParsedField =
                    match property.PropertyType.ToString() with
                    | "System.Int32"  -> IntField    property.Name
                    | "System.String" -> StringField property.Name
                    | _  ->
                        printfn
                            "Expecting 'int' or 'string' for '%s.%s', got '%A'."
                            case.Name property.Name
                            property.PropertyType
                        System.Environment.Exit -1
                        Error
                let PrettyPrintField field =
                    match field with
                    | IntField    field -> printfn "  '%s' : int" field
                    | StringField field -> printfn "  '%s' : string" field
                    | Error             -> printfn ""
                let fields =
                    case.GetFields()
                    |> Seq.map (fun field -> FSharpType.GetRecordFields field.PropertyType |> Array.map extract_fields)
                    |> Seq.collect id
                    |> Seq.toList
                let (|HasPrefix|_|) (prefix : string) (s : string) =
                    if s.StartsWith(prefix)
                    then Some (s.Substring(prefix.Length))
                    else None
                match case.Name with
                | "JSON_parse_error" -> [], []
                | HasPrefix "GET_" route ->
                    printfn "GET /%s" route
                    List.map PrettyPrintField fields |> ignore
                    let handler =
                        request (fun r ->
                            let extract_field_name field =
                                match field with
                                | IntField y -> [ y ]
                                | StringField y -> [ y ]
                                | _ -> []
                            let field_names = List.reduce (@) (List.map extract_field_name fields)
                            let populate_field field =
                                match r.queryParam field with
                                | Choice1Of2 value -> setUserData field value
                                | _ -> never
                            let populate_all_fields = List.reduce (>>=) <| List.map populate_field field_names
                            populate_all_fields >>= context (fun ctx ->
                                // The trick with string manipulation ensures static typing of the parameters, since
                                // they are now parsed as a JSON representation of a specific discriminated union.
                                let csv a b = a + "," + b
                                let as_json_field f = sprintf "\"%s\":\"%s\"" f (ctx.userState.[f].ToString())
                                let json_fields = field_names |> List.map as_json_field |> List.reduce csv
                                let json = "{\"Case\":\"GET_" + route + "\",\"Fields\":[{" + json_fields + "}]}"
                                let data = APIEndpoint.FromJSON json
                                send_api_response data))
                    [path ("/" + route) >>= handler], []
                | HasPrefix "POST_" route ->
                    printfn "POST /%s" route
                    List.map PrettyPrintField fields |> ignore
                    let request_handler r =
                        if r.rawForm.Length > 0
                        then
                            // The trick with string manipulation ensures static typing of JSON body,
                            // since it is now being parsed as a discriminated union of the corresponding type.
                            let original_body = System.Text.Encoding.ASCII.GetString(r.rawForm)
                            let augmented_body =
                                """{"Case":"POST_""" + route + """","Fields":[""" + original_body + "]}"
                            let data = APIEndpoint.FromJSON augmented_body
                            send_api_response data
                        else never
                    [], [path ("/" + route) >>= request request_handler]
                | _   ->
                    printfn "Expected 'GET_*' or 'POST_*', got '%s'." case.Name
                    System.Environment.Exit -1
                    [], []
        ))

    let (api_get_routes, api_post_routes) = List.reduce (fun (a, b) (c, d) -> (a @ c, b @ d)) api_endpoints
    List.reduce (@) [ api_get_routes; api_post_routes; demo_endpoints ] |> choose

let spawn_server routes = 
    let cancel = new Threading.CancellationTokenSource()
    let config = { Web.defaultConfig with cancellationToken = cancel.Token }
    let run_async async_server = ignore <| Async.StartAsTask(async_server, cancellationToken = cancel.Token)
    Web.startWebServerAsync config routes |> snd |> run_async
    cancel
    
// Unit tests.
[<TestFixture>]
type UnitTest() =
    // Test setup.
    [<TestFixtureSetUp>]
    member this.``Bring up the server``() = ignore <| spawn_server app

    // Demo endpoints test.
    [<Test>]
    member this.``DEMO: GET /demo_get``() =
        "OK"
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/demo_get" port)
    [<Test>]
    [<ExpectedException>]
    member this.``DEMO: POST /demo_get not allowed``() =
        should throw typeof<System.Net.WebException>
        <| Http.RequestString((sprintf "http://localhost:%d/demo_get" port), httpMethod="POST")
    [<Test>]
    member this.``DEMO: GET /demo_add/2/2``() =
        "2+2=4"
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/demo_add/2/2" port)
    [<Test>]
    member this.``DEMO: GET /demo_whatever?demo_url_parameter=foo"``() =
        "demo_url_parameter=foo"
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/demo_whatever?demo_url_parameter=foo" port)
    [<Test>]
    member this.``DEMO: POST /demo_post``() =
        "LENGTH=3, BODY='foo'"
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/demo_post" port),
                              httpMethod = "POST",
                              body = TextRequest "foo")

    // API endpoints test.
    [<Test>]
    member this.``API: GET /add``() =
        """{"sum":5}"""
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/add?a=2&b=3" port)
    [<Test>]
    member this.``API: GET /subtract``() =
        """{"result":4}"""
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/subtract?a=5&b=1" port)
    [<Test>]
    member this.``API: GET /concatenate``() =
        """{"concatenated":"foo bar"}"""
        |> should equal
        <| Http.RequestString(sprintf "http://localhost:%d/concatenate?p=foo%s&q=bar" port "%20")
    [<Test>]
    member this.``API: POST /multiply``() =
        """{"product":35}"""
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/multiply" port),
                              httpMethod = "POST",
                              body = TextRequest """{"x":5,"y":7}""")
    [<Test>]
    member this.``API: POST /divide``() =
        """{"result":1.25}"""
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/divide" port),
                              httpMethod = "POST",
                              body = TextRequest """{"x":5,"y":4}""")
    [<Test>]
    member this.``API: POST /divide with invalid data``() =
        """{"error":"Parameter 'y' should not be zero."}"""
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/divide" port),
                              httpMethod = "POST",
                              body = TextRequest """{"x":100,"n":0}""",
                              silentHttpErrors = true)
    [<Test>]
    member this.``API: POST /repeat``() =
        """{"result":"foofoofoo"}"""
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/repeat" port),
                              httpMethod = "POST",
                              body = TextRequest """{"s":"foo","n":3}""")
    [<Test>]
    member this.``API: POST /repeat with invalid data``() =
        """{"error":"Parameter 'n' should be positive."}"""
        |> should equal
        <| Http.RequestString((sprintf "http://localhost:%d/repeat" port),
                              httpMethod = "POST",
                              body = TextRequest """{"s":"bar","n":-3}""",
                              silentHttpErrors = true)

// Application runner.
[<EntryPoint>]
let main argv =
    let server = spawn_server app
    printfn "Server running on port %d. Press enter to stop it." port
    System.Console.ReadLine() |> ignore
    server.Dispose() |> ignore
    0
    
