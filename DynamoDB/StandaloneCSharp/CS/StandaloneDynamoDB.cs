// Depends on the `AWSSDK.DynamoDBv2` package.

using System;
using System.Collections.Generic;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace CS
{
	class DynamoDB
	{
		// Use credentials from environmental variables.
		// `Amazon.RegionEndpoint.USWest1`, "N. California", is our default preference. -- D.K.
		private static AWSCredentials credentials = new EnvironmentVariablesAWSCredentials();
		private AmazonDynamoDBClient client = new AmazonDynamoDBClient(credentials, Amazon.RegionEndpoint.USWest1);

		private string tableName;

		public DynamoDB(string tableName)
		{
			this.tableName = tableName;
		}

		public void CommandCreate() {
			try {
			client.CreateTable(new CreateTableRequest
				{
					TableName = tableName,
					AttributeDefinitions = new List<AttributeDefinition>()
					{
						new AttributeDefinition()
						{
							AttributeName = "H",  // "H" for an unused Hash key.
							AttributeType = "N"   // Of type "Number", always 0.
						},
						new AttributeDefinition()
						{
							AttributeName = "K",  // "K" for Key,
							AttributeType = "N"   // Of type "Number".
						},
						// The "V" attribute for Value is implicit.
					},
					KeySchema = new List<KeySchemaElement>()
					{
						new KeySchemaElement()
						{
							AttributeName = "H",
							KeyType = "HASH"  // The first key in DynamoDB must be hash.
						},
						new KeySchemaElement()
						{
							AttributeName = "K",
							KeyType = "RANGE"  // The second key in DynamoDB must can be range to sort by.
						},
					},
					ProvisionedThroughput = new ProvisionedThroughput
					{
						ReadCapacityUnits = 1,
						WriteCapacityUnits = 1
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

			Console.Write ("Creating the table: ");
			do {
				System.Threading.Thread.Sleep (50);
				try {
					var res = client.DescribeTable (new DescribeTableRequest {
						TableName = tableName
					});
					if (res.Table.TableStatus == "ACTIVE") {
						break;
					}
					if (res.Table.TableStatus == "CREATING") {
						Console.Write(".");
					} else {
						Console.Write("[{0}]", res.Table.TableStatus);
					}
						
					status = res.Table.TableStatus;
				}
				catch (ResourceNotFoundException) {
					Console.Write("?");  // Okay to appear, since the consistency is eventual.
					status = "EXCEPTION";
				}
			} while (status != "ACTIVE");
			Console.WriteLine (" Done.");
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
			Console.Write ("Deleting the table: ");
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
					} else {
						Console.Write("[{0}]", res.Table.TableStatus);
					}
					status = res.Table.TableStatus;
				}
				while (status == "DELETING");
			}
			catch (ResourceNotFoundException) {
				Console.WriteLine (" Done.");
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
	}

	class StandaloneDynamoDB
	{
		private static string DefaultTableName = "StandaloneDynamoDBTestTable";

		public static void Main (string[] args)
		{
			Console.WriteLine ("A simple standalone C# DynamoDB test. Needs two environmental variables set:");
			Console.WriteLine ("* 'AWS_ACCESS_KEY_ID'");
			Console.WriteLine ("* 'AWS_SECRET_ACCESS_KEY'");

			if (args.Length < 1)
			{
				Console.WriteLine ("");
				Console.WriteLine ("Usage: `StandaloneDynamoDB <command> [table_name]`, where `command` is one of:");
				Console.WriteLine ("* `create` : Create the table.");
				Console.WriteLine ("* `delete` : Delete the table.");
				Console.WriteLine ("* `recreate` : Create the table.");
				Console.WriteLine ("And `table_name` defaults to '{0}'.", DefaultTableName);

			}

			Console.WriteLine ("");

			var command = (args.Length >= 1 ? args [0].ToLower () : "");
			while (command == "")
			{
				Console.Write ("Now, enter a command: ");
				command = Console.ReadLine ().ToLower ();
			}

			var table_name = args.Length >= 2 ? args [1] : DefaultTableName;
			var runner = new DynamoDB (table_name);
			switch (command)
			{
			case "create":
				runner.CommandCreate ();
				break;
			case "delete":
				runner.CommandDelete ();
				break;
			case "recreate":
				runner.CommandDelete ();
				runner.CommandCreate ();
				break;
			case "fill":
				runner.CommandFill ();
				break;
			case "scan":
				runner.CommandScan ();
				break;
			default:
				Console.WriteLine ("Unrecognized command.");
				break;
			}
		}
	}
}
