open Suave

[<EntryPoint>]
let main argv =
    Web.defaultConfig |> Web.startWebServer <| Http.Successful.OK "OK!\n"
    0