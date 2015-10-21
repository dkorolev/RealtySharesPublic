namespace RealtyShares

module DynamoEventStore =
    open Amazon.DynamoDBv2
    open Amazon.DynamoDBv2.DocumentModel
    open Amazon.DynamoDBv2.Model
    open Amazon.Runtime
    open FSharp.Control
    open DynamoDb

    type IEventStore =
       abstract member load: string -> Async<Event array>
       abstract member save : string -> int -> string -> Async<Document> 
    and Event = {
        Id : string
        Version : int
        Body : string
    }

    let save (table:Table) id version body = 
        table.PutItemAsync(doc ["H", entry id
                                "K", entry version
                                "V", entry body ]) 
        |> Async.AwaitTask

    let load (table:Table) (id:string) =
        table.Query(new QueryFilter(attributeName = "H", op = QueryOperator.Equal, values = [|entry 0|]))
        |> readResults (fun doc -> 
            { Id = id; Version = fromEntry<int> doc.["K"]; Body  = fromEntry<string> doc.["V"] })
            
    let makeEventStore (table:Table) =
        { new IEventStore with 
            member this.load id = load table id
            member this.save id version body = save table id version body }
