# Mamemaki.Slab.BigQuery

Mamemaki.Slab.BigQuery is [SLAB]\(Semantic Logging Application Block) sink for [Google BigQuery]. Receive log messages from [ETW] and push them to [Google BigQuery].

## Installation

`PM> Install-Package Mamemaki.Slab.BigQuery`

## Basic Usage

```cs
    class Program
    {
        static void Main(string[] args)
        {
            var projectId = "xxxxxxx-0000";
            var datasetId = "xxxxxxx";
            var tableId = "hello{yyyyMM01}";
            var serviceAccountEmail = "000000000000-xxxxxxxxxxxxx@developer.gserviceaccount.com";
            var privateKeyFile = @"/path/to/xxxx-000000000000.p12";
            var tableSchemaFile = @"/path/to/hello.json";
            using (var listener = new ObservableEventListener())
            {
                listener.EnableEvents(MyEventSource.Log, EventLevel.LogAlways);
                listener.LogToConsole();
                listener.LogToBigQuery(
                    projectId: projectId,
                    datasetId: datasetId,
                    tableId: tableId,
                    serviceAccountEmail: serviceAccountEmail,
                    privateKeyFile: privateKeyFile,
                    autoCreateTable: true,
                    tableSchemaFile: tableSchemaFile);

                MyEventSource.Log.Start();
                Thread.Sleep(1);
                MyEventSource.Log.Hello("World");
                Thread.Sleep(1);
                MyEventSource.Log.End();
            }
        }
    }

    [EventSource(Name = "My")]
    class MyEventSource : EventSource
    {
        private static readonly MyEventSource Instance = new MyEventSource();
        private MyEventSource() {}
        public static MyEventSource Log { get { return Instance; } }

        [Event(1, Level = EventLevel.Verbose, Message = "Start")]
        public void Start()
        {
            if (this.IsEnabled())
                this.WriteEvent(1);
        }

        [Event(2, Level = EventLevel.Verbose, Message = "End")]
        public void End()
        {
            if (this.IsEnabled())
                this.WriteEvent(2);
        }

        [Event(3, Level = EventLevel.Informational, Message = "Hello {0}!")]
        public void Hello(string to)
        {
            if (this.IsEnabled())
                this.WriteEvent(3, to);
        }
    }
```
hello.json
```json
[
  {
    "name": "Timestamp",
    "type": "TIMESTAMP",
    "mode": "REQUIRED"
  },
  {
    "name": "FormattedMessage",
    "type": "STRING",
    "mode": "REQUIRED"
  },
  {
    "name": "to",
    "type": "STRING"
  }
]
```

## Configuration

### BigQuerySink parameters

Parameter  | Description | Required(default)
------------- | ------------- | -------------
`projectId` | Project id of Google BigQuery. | Yes
`datasetId` | Dataset id of Google BigQuery. | Yes
`tableId` | Table id of Google BigQuery. Expandable through DateTime.Format(). e.g. "accesslog{yyyyMMdd}" => accesslog20150101 (bracket braces needed) | Yes
`authMethod` | Accepts "private_key" only. | No("private_key")
`serviceAccountEmail` | Email address of Google BigQuery [service account]. | Yes if authMethod == "private_key"
`privateKeyFile` | Private key file(*.p12) of Google BigQuery [service account]. | Yes if authMethod == "private_key"
`privateKeyPassphrase` | Private key passphrase of Google BigQuery [service account]. | No("notasecret")
`autoCreateTable` | If set true, check table exsisting and create table dynamically. see [Dynamic table creating](#dynamic_table_creating). | No(false)
`tableSchemaFile` | Json file that define Google BigQuery table schema. | Yes
`insertIdFieldName` | The field name of InsertId. If set `%uuid%` generate uuid each time. if not set InsertId will not set. see [Specifying insertId property](#specifying_insertId_property). | No(null)
`bufferingInterval` | The buffering interval. | No(00:00:30)
`bufferingCount` | The buffering count. | No(200)
`maxBufferSize` | The maximum number of entries that can be buffered before the sink starts dropping entries. | No(30000)
`onCompletedTimeout` | Timeout for data flushing. | No(00:01:00)

See [Quota policy](https://cloud.google.com/bigquery/streaming-data-into-bigquery#quota)
section in the Google BigQuery document.

### Authentication

There is one method supported to fetch access token for the [service account].

1. "private_key" - Public-Private key pair

On this method. You first need to create a service account (client ID), download its private key and deploy the key with your assembly.


### Table id formatting

`tableId` accept [DateTime.ToString()] format to construct table id.
Table ids formatted at runtime using the local time.

For example, with the `tableId` is set to `accesslog{yyyyMM01}`, table ids `accesslog20140801`, `accesslog20140901` and so on.

Note that the timestamp of logs and the date in the table id do not always match,
because there is a time lag between collection and transmission of logs.

### Table schema

There is one method to describe the schema of the target table.

1. Load a schema file in JSON.

On this method, set `tableSchemaFile` to a path to the JSON-encoded schema file which you used for creating the table on BigQuery. see [table schema] for detail information.

Example:
```json
[
  {
    "name": "Timestamp",
    "type": "TIMESTAMP",
    "mode": "REQUIRED"
  },
  {
    "name": "FormattedMessage",
    "type": "STRING",
    "mode": "REQUIRED"
  }
]
```

### <a id="specifying_insertId_property"></a>Specifying insertId property

BigQuery uses `insertId` property to detect duplicate insertion requests (see [data consistency](https://cloud.google.com/bigquery/streaming-data-into-bigquery#dataconsistency) in Google BigQuery documents).
You can set `insertIdFieldName` option to specify the field to use as `insertId` property.

If set `%uuid%` to `insertIdFieldName`, generate uuid each time.

### <a id="dynamic_table_creating"></a>Dynamic table creating

When `autoCreateTable` is set to `true`, check exsiting the table before insertion, then create table if does not exist. When table name changed, rerun this sequence again.

### Field mapping

To find the value corresponding to BigQuery table field from EventEntry. We use below rules.

1. Find matching name from Payloads.
1. Find matching name from built-in [EventEntry] attributes.
1. Error if field mode is "REQUIRED", else will not set the field value.

Suppoeted built-in [EventEntry] attributes:

Name  | Data type
------------- | -------------
`EventId` | INTEGER
`EventName` | STRING
`Level` | INTEGER
`FormattedMessage` | STRING
`Keywords` | INTEGER
`KeywordsDescription` | STRING
`Task` | INTEGER
`TaskName` | STRING
`Opcode` | INTEGER
`OpcodeName` | STRING
`Timestamp` | TIMESTAMP
`ProcessId` | INTEGER
`ThreadId` | INTEGER
`ProviderId` | STRING
`ProviderName` | STRING
`Version` | INTEGER
`ActivityId` | STRING
`RelatedActivityId` | STRING

NOTE: EventEntry value's data type and BigQuery table field's data type must match too.

## References

* [ETW]
* [EventSource]
* [SLAB]
* [Google BigQuery]
* [fluent-plugin-bigquery](https://github.com/kaizenplatform/fluent-plugin-bigquery)
* [.NET アプリから BigQuery に Streaming Insert する方法(JPN)](http://tech.tanaka733.net/entry/streaming-insert-into-bigquery-from-dotnet)

----
Copyright (c) 2015, Tsuyoshi Sumiyoshi and collaborators. All rights reserved

[ETW]: https://msdn.microsoft.com/en-us/library/windows/desktop/bb968803(v=vs.85).aspx
[EventSource]: https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource%28v=vs.110%29.aspx
[SLAB]: https://github.com/mspnp/semantic-logging
[Google BigQuery]: https://cloud.google.com/bigquery/
[service account]: https://developers.google.com/identity/protocols/OAuth2ServiceAccount
[table schema]: https://cloud.google.com/bigquery/docs/reference/v2/tables#schema
[DateTime.ToString()]: https://msdn.microsoft.com/en-us/library/vstudio/zdtaw1bw(v=vs.100).aspx
