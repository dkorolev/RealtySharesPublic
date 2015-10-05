open FSharp.Data

type Simple = JsonProvider<"""
{
    "s":"",
    "n":0,
    "as":[""],
    "an":[0],
    "o": {
        "s":"",
        "n":0,
        "as":[""],
        "an":[0]
    }
}
""">

let run (v : Simple) : unit =
    printf "haha!"

[<EntryPoint>]
let main argv = 
    let s = """
{
    "s":"foo",
    "n":42,
    "as":["here","be","dragons"],
    "an":[1,2,3],
    "o": {
        "s":"inner",
        "n":1,
        "as":["two","three"],
        "an":[40,2]
    }
}
"""
    // Type mistmatch.
    // let try_parse_simple (json : string) : Option<Simple> = Some ((Simple.Parse(json)))
    let v = 
        try (Some (Simple.Parse(s)))
        with _ -> None
    // Type mistmatch.
    // match v with
    // | Some value -> run value
    // | None -> ignore
    printfn "%A" v
    0
 