#r @".\packages\AWSSDK.Core.3.1.2.1\lib\net45\AWSSDK.Core.dll"
#r @".\packages\AWSSDK.DynamoDBv2.3.1.1.1\lib\net45\AWSSDK.DynamoDBv2.dll"
#r @".\packages\FSharp.Control.AsyncSeq.2.0.1\lib\net45\FSharp.Control.AsyncSeq.dll"

#load "dynamoDb.fs"
#load "dynamoEventStore.fs"

open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.DocumentModel
open Amazon.DynamoDBv2.Model
open Amazon.Runtime
open RealtyShares
open RealtyShares.DynamoDb
open RealtyShares.DynamoEventStore

let awsAccessKey = ""
let awsSecretKey = ""
let tableName = "scratchpad-table"

async {
    let client = new AmazonDynamoDBClient(awsAccessKey, awsSecretKey, Amazon.RegionEndpoint.USWest1)
  
    printfn "Creating table, %s..." tableName
    let! res = createTable client tableName
    let! status = waitForStatus client tableName TableStatus.ACTIVE
    printfn "Table created!"

    let table = Table.LoadTable(client, tableName)

    let sw = new System.Diagnostics.Stopwatch()
    sw.Start()
    
    printfn "inserting %d entries..." 1000
    try 
        async {
            let inserts = 
                [0..1000]
                |> List.mapi (fun index version -> save table index version (sprintf "version %d" version))
            for entry in inserts do 
                let! _ = entry
                ()
        } |> Async.RunSynchronously |> ignore
    with e -> printfn "error: %s" (string e)

    sw.Stop()

    let writeSpeed = 1000.0 / sw.Elapsed.TotalMilliseconds * 1000.00
    printfn "wrote %d entries at %f writes/sec" 1000 writeSpeed

    let sw2 = new System.Diagnostics.Stopwatch()
    sw2.Start()
    
    load table "0"  
    |> Async.RunSynchronously
    |> (fun events -> printfn "read %d events" events.Length)

    sw2.Stop()

    let readSpeed = 1000.0 / sw2.Elapsed.TotalMilliseconds * 1000.00
    printfn "read %d entries at %f reads/sec" 1000 readSpeed

    printfn "Deleting table, %s..." tableName
    let! del = deleteTable client tableName
    //let! status = DynamoDb.waitForStatus tableName TableStatus.
        
    printfn "Table deleted!"
} |> Async.RunSynchronously
