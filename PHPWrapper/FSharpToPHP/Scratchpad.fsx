// Forward slashes work on both Linux and Windows. -- D.K.

#I "../packages"
#r "Newtonsoft.Json.7.0.1/lib/net45/Newtonsoft.Json.dll"
#r "FSharp.Data.2.2.5/lib/net40/FSharp.Data.dll"
#load "PHPModule.fs"

open PHPModule

phpAdd 2 3 |> printfn "%A"
