namespace RealtyShares

module DynamoDb =
    open Amazon.DynamoDBv2
    open Amazon.DynamoDBv2.DocumentModel
    open Amazon.DynamoDBv2.Model
    open Amazon.Runtime
    open FSharp.Control

    let bclList (lst:'a list) = new System.Collections.Generic.List<'a>(lst)
    let attr = ScalarAttributeType
    let keyType = KeyType
        
    let waitForStatus (client:AmazonDynamoDBClient) tableName status = 
        let rec loop attempt maxretry delay (prevStatus:TableStatus option) = async {
            if attempt > maxretry then
                return prevStatus
            else
                do! Async.Sleep delay
                let res = client.DescribeTable(DescribeTableRequest(TableName = tableName))
                let currStatus = res.Table.TableStatus
                if res.Table.TableStatus = status then
                    return Some res.Table.TableStatus
                else
                    return! loop (attempt + 1) maxretry delay (Some currStatus)
        }
        loop 0 200 50 None
    
    let createTable (client:AmazonDynamoDBClient) tableName = 
        client.CreateTableAsync(
            CreateTableRequest(TableName = tableName,
                                KeySchema = bclList [
                                    KeySchemaElement(AttributeName="H", KeyType= keyType "HASH")
                                    KeySchemaElement(AttributeName="K", KeyType= keyType "RANGE")],
                                AttributeDefinitions = bclList [
                                    AttributeDefinition(AttributeName="H", AttributeType = attr "N")
                                    AttributeDefinition(AttributeName="K", AttributeType = attr "N")],
                                ProvisionedThroughput = 
                                    ProvisionedThroughput(ReadCapacityUnits= int64 5, WriteCapacityUnits = int64 5)))
        |> Async.AwaitTask

    let deleteTable (client:AmazonDynamoDBClient) tableName = 
        client.DeleteTableAsync(DeleteTableRequest(tableName))
        |> Async.AwaitTask

    let readResults map (search:Search) =
        let rec scan() = asyncSeq {
            printfn "scanning..."
            let! doc = search.GetNextSetAsync() |> Async.AwaitTask
            for e in doc do yield map e
            if not search.IsDone 
                then yield! scan()
        }
        scan() |> AsyncSeq.toArrayAsync

    let scanTable map (table:Table) =
        table.Scan(ScanFilter())
        |> readResults map

    let inline entry (el:'a) = Amazon.DynamoDBv2.DynamoDBEntryConversion.V2.ConvertToEntry el
    let inline fromEntry<'a> e = Amazon.DynamoDBv2.DynamoDBEntryConversion.V2.ConvertFromEntry<'a> e

    let doc (attrs:(string*DynamoDBEntry) seq) = 
        let d = System.Collections.Generic.Dictionary<string,DynamoDBEntry>()
        for (k,v) in attrs do
            d.Add(k,v)
        Document(d)