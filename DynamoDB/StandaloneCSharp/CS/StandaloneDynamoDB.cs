// Requires the `AWSSDK.DynamoDBv2` package.

using System;
using System.Collections.Generic;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace CS
{
    class DynamoDB
    {
        // Use credentials from environmental variables, and "N. California",
        // which is `Amazon.RegionEndpoint.USWest1`, is our default. -- D.K.
        private static AWSCredentials credentials = new EnvironmentVariablesAWSCredentials();
        private AmazonDynamoDBClient client = new AmazonDynamoDBClient(credentials, Amazon.RegionEndpoint.USWest1);

        private string tableName;

        public DynamoDB(string tableName) {
            this.tableName = tableName;
        }

        public void CommandCreate() {
            try {
                // A necessary clarification: DynamoDB schema requires the primary key, which is
                // either just a hash field, or a hash field plus a range field.
                // Since we only care about the range field, the hash field is just zero for us,
                // but we still have to create it.
                client.CreateTable(new CreateTableRequest
                {
                    TableName = tableName,
                    AttributeDefinitions = new List<AttributeDefinition>()
                    {
                        new AttributeDefinition()
                        {
                            AttributeName = "H",  // "H" for an unused Hash key.
							AttributeType = "N"   // Of type "Number", which will always be 0.
						},
                        new AttributeDefinition()
                        {
                            AttributeName = "K",  // "K" for Key,
							AttributeType = "N"   // Of type "Number".
						},
						// The "V" attribute for Value is not the key, it appears later by itself.
					},
                    KeySchema = new List<KeySchemaElement>()
                    {
                        new KeySchemaElement()
                        {
                            AttributeName = "H",
                            KeyType = "HASH"  // The first key in DynamoDB must be hash. Reminder: always 0.
						},
                        new KeySchemaElement()
                        {
                            AttributeName = "K",
                            KeyType = "RANGE"  // The second key in DynamoDB must can be range to order by.
						},
                    },
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 5,
                        WriteCapacityUnits = 5
                    }
                });

                WaitTillTableCreated(client, tableName);
                Console.WriteLine("Table created.");
            }
            catch (ResourceInUseException) {
                Console.WriteLine("Table already exists.");
            }
        }

        private void WaitTillTableCreated(AmazonDynamoDBClient client, string tableName) {
            string status;

            Console.Write("Creating the table: ");
            do {
                System.Threading.Thread.Sleep(50);
                try {
                    var res = client.DescribeTable(new DescribeTableRequest
                    {
                        TableName = tableName
                    });
                    if (res.Table.TableStatus == "ACTIVE") {
                        break;
                    }
                    if (res.Table.TableStatus == "CREATING") {
                        Console.Write(".");
                    }
                    else {
                        Console.Write("[{0}]", res.Table.TableStatus);
                    }

                    status = res.Table.TableStatus;
                }
                catch (ResourceNotFoundException) {
                    Console.Write("?");  // Okay to appear, since the consistency is eventual.
                    status = "EXCEPTION";
                }
            } while (status != "ACTIVE");
            Console.WriteLine(" Done.");
        }

        public void CommandDelete() {
            try {
                client.DeleteTable(new DeleteTableRequest() { TableName = tableName });
                WaitTillTableDeleted(client, tableName);
                Console.WriteLine("Table deleted.");
            }
            catch (ResourceNotFoundException) {
                Console.WriteLine("No such table.");
            }
        }

        private void WaitTillTableDeleted(AmazonDynamoDBClient client, string tableName) {
            Console.Write("Deleting the table: ");
            string status;
            try {
                do {
                    System.Threading.Thread.Sleep(50);

                    var res = client.DescribeTable(new DescribeTableRequest
                    {
                        TableName = tableName
                    });
                    if (res.Table.TableStatus == "DELETING") {
                        Console.Write(".");
                    }
                    else {
                        Console.Write("[{0}]", res.Table.TableStatus);
                    }
                    status = res.Table.TableStatus;
                }
                while (status == "DELETING");
            }
            catch (ResourceNotFoundException) {
                Console.WriteLine(" Done.");
            }
        }

        public void CommandFill() {
            Table table = Table.LoadTable(client, tableName);
            for (int i = 1; i <= 5; ++i) {
                var document = new Document();
                document["H"] = 0;
                document["K"] = i;
                document["V"] = "Number " + i;
                table.PutItem(document);
            }
        }

        public void CommandPublish() {
            Table table = Table.LoadTable(client, tableName);
            Console.WriteLine("Keep typing string to be added to the table. An empty string will end it.");
            while (true) {
                Console.Write("> ");
                string contents = Console.ReadLine();
                if (contents == "") {
                    break;
                }

                // Milliseconds since epoch, as JavaScript's `Date.now()` does.
                var now = (Int64)((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);

                var document = new Document();
                document["H"] = 0;
                document["K"] = now;
                document["V"] = contents;
                table.PutItem(document);

                Console.WriteLine("OK, K={0}.", now);
            }
        }

        public void CommandScan() {
            Table replyTable = Table.LoadTable(client, tableName);
            Search search = replyTable.Scan(new ScanFilter());

            List<Document> documentList = new List<Document>();
            do {
                documentList = search.GetNextSet();
                foreach (var document in documentList) {
                    Console.WriteLine("{0} : {1}", document["K"], document["V"]);
                }

            } while (!search.IsDone);
        }

        public void CommandSubscribe() {
            IDisposable stream = DoSubscribe().Subscribe(
                x => Console.WriteLine("{0}: {1}", x.Item1, x.Item2),
                ex => Console.WriteLine("Error: {0}", ex.Message),
                () => Console.WriteLine("Done."));
            Console.ReadLine();
            stream.Dispose();
        }

        private IObservable<Tuple<Int64, string>> DoSubscribe() {
            return DoSubscribeAsEnumerable().ToObservable();
        }

        private IEnumerable<Tuple<Int64, string>> DoSubscribeAsEnumerable() {
            Table replyTable = Table.LoadTable(client, tableName);

            Console.WriteLine("Listening to the events continuously.");

            Int64 last_key = 0;
            while (true) {
                var scan_filter = new ScanFilter();
                if (last_key > 0) {
                    scan_filter.AddCondition("K", ScanOperator.GreaterThan, last_key);
                }
                Search search = replyTable.Scan(scan_filter);

                List<Document> documentList = new List<Document>();
                do {
                    documentList = search.GetNextSet();
                    foreach (var document in documentList) {
                        yield return new Tuple<Int64, string>((Int64)document["K"], document["V"]);
                        Int64 current_key = Int64.Parse(document["K"].AsString());
                        if (!(current_key > last_key)) {
                            Console.WriteLine("ERROR: {0} should be > {1}.", current_key, last_key);
                        }
                        else {
                            last_key = current_key;
                        }
                    }

                } while (!search.IsDone);

                // TODO(dkorolev): Do something smarter here, and wrap the whole thing into an IObservable<>.
                System.Threading.Thread.Sleep(50);
            }
        }
    }

    class StandaloneDynamoDB
    {
        private static string DefaultTableName = "StandaloneDynamoDBTestTable";

        public static void Main(string[] args) {
            Console.WriteLine("A simple standalone C# DynamoDB test. Needs two environmental variables set:");
            Console.WriteLine("* 'AWS_ACCESS_KEY_ID'");
            Console.WriteLine("* 'AWS_SECRET_ACCESS_KEY'");

            if (args.Length < 1) {
                Console.WriteLine("");
                Console.WriteLine("Usage: `StandaloneDynamoDB <command> [table_name]`, where `command` is one of:");
                Console.WriteLine("* `create`    : Create the table.");
                Console.WriteLine("* `delete`    : Delete the table.");
                Console.WriteLine("* `recreate`  : Delete the table and immediately create it again.");
                Console.WriteLine("* `fill`      : Add [1 .. 5] to the table.");
                Console.WriteLine("* `scan`      : Show table contents.");
                Console.WriteLine("* `publish`   : Read strings from the terminal and keep publishing them.");
                Console.WriteLine("* `subscribe` : Scan the table continuously.");
                Console.WriteLine("And `table_name` defaults to '{0}'.", DefaultTableName);
            }

            Console.WriteLine("");

            var command = (args.Length >= 1 ? args[0].ToLower() : "");
            while (command == "") {
                Console.Write("As you didn't provide a command from command line, enter it now: ");
                command = Console.ReadLine().ToLower();
            }

            var table_name = args.Length >= 2 ? args[1] : DefaultTableName;

            try {
                var runner = new DynamoDB(table_name);
                switch (command) {
                    case "create":
                        runner.CommandCreate();
                        break;
                    case "delete":
                        runner.CommandDelete();
                        break;
                    case "recreate":
                        runner.CommandDelete();
                        runner.CommandCreate();
                        break;
                    case "fill":
                        runner.CommandFill();
                        break;
                    case "scan":
                        runner.CommandScan();
                        break;
                    case "publish":
                        runner.CommandPublish();
                        break;
                    case "subscribe":
                        runner.CommandSubscribe();
                        break;
                    default:
                        Console.WriteLine("Unrecognized command.");
                        break;
                }
            }
            catch (Exception e) {
                // Most likely, the required environmental variables are not set.
                Console.WriteLine("Exception: {0}", e);
            }
        }
    }
}
