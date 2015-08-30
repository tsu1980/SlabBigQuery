﻿using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.Apis.Services;
using Google.Apis.Util;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery
{
    public class BigQuerySink : IObserver<EventEntry>, IDisposable
    {
        private TimeSpan _onCompletedTimeout;
        private BufferedEventPublisher<EventEntry> _bufferedPublisher;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private BigqueryService _BQSvc;
        private IBackOff _BackOff;
        private BigQueryRowDataMap _BigQueryRowDataMap;

        public string ProjectId { get; private set; }
        public string DatasetId { get; private set; }
        public string TableId { get; private set; }

        public bool AutoCreateTable { get; private set; }
        public string TableIdExpanded
        {
            get
            {
                return _TableIdExpanded;
            }
            set
            {
                if (String.Compare(_TableIdExpanded, value) != 0)
                {
                    _TableIdExpanded = value;
                    _NeedCheckTableExists = false;
                }
            }
        }
        private string _TableIdExpanded;
        private bool _TableIdExpandable;
        private bool _NeedCheckTableExists;

        public string TableSchemaFile { get; private set; }
        public TableSchema TableSchema { get; private set; }

        /// <summary>
        /// Specified field value use for InsertId(<see cref="https://cloud.google.com/bigquery/streaming-data-into-bigquery#dataavailability"/>).
        /// If set "%uuid%" then generate uuid each time.
        /// </summary>
        public string InsertIdFieldName { get; private set; }

        /// <summary>
        /// Create sink
        /// </summary>
        /// <param name="projectId">Project id</param>
        /// <param name="datasetId">Dataset id</param>
        /// <param name="tableId">Table id. Expand through DateTime.Format(). e.g. "accesslogyyyyMMdd" => accesslog20150101 <see cref="https://msdn.microsoft.com/en-us/library/vstudio/zdtaw1bw(v=vs.100).aspx"/></param>
        /// <param name="authMethod">private_key</param>
        /// <param name="serviceAccountEmail">000000000000-xxxxxxxxxxxxxxxxxxxxxx@developer.gserviceaccount.com</param>
        /// <param name="privateKeyFile">/path/to/xxxx-000000000000.p12</param>
        /// <param name="privateKeyPassphrase">notasecret</param>
        /// <param name="autoCreateTable">Create table if it does not exists</param>
        /// <param name="tableSchemaFile">Json file path that bigquery table schema</param>
        /// <param name="insertIdFieldName">The field name of InsertId</param>
        /// <param name="bufferingInterval"></param>
        /// <param name="bufferingCount"></param>
        /// <param name="onCompletedTimeout"></param>
        /// <param name="maxBufferSize"></param>
        public BigQuerySink(
            string projectId,
            string datasetId,
            string tableId,
            string authMethod = null,
            string serviceAccountEmail = null,
            string privateKeyFile = null,
            string privateKeyPassphrase = null,
            bool? autoCreateTable = null,
            string tableSchemaFile = null,
            string insertIdFieldName = null,
            TimeSpan? bufferingInterval = null,
            int? bufferingCount = null,
            TimeSpan? onCompletedTimeout = null,
            int? maxBufferSize = null)
        {
            if (authMethod == null)
                authMethod = "private_key";
            if (authMethod != "private_key")
                throw new NotSupportedException("authMethod must be 'private_key'");
            if (String.IsNullOrEmpty(serviceAccountEmail))
                throw new ArgumentException("serviceAccountEmail");
            if (String.IsNullOrEmpty(privateKeyFile))
                throw new ArgumentException("privateKeyFile");
            if (privateKeyPassphrase == null)
                privateKeyPassphrase = "notasecret";
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentException("projectId");
            if (String.IsNullOrEmpty(datasetId))
                throw new ArgumentException("datasetId");
            if (String.IsNullOrEmpty(tableId))
                throw new ArgumentException("tableId");
            if (bufferingInterval == null)
                bufferingInterval = Constants.DefaultBufferingInterval;
            if (bufferingCount == null)
                bufferingCount = Constants.DefaultBufferingCount;
            if (onCompletedTimeout == null)
                onCompletedTimeout = Constants.DefaultBufferingFlushAllTimeout;
            if (maxBufferSize == null)
                maxBufferSize = Constants.DefaultMaxBufferSize;
            this.ProjectId = projectId;
            this.DatasetId = datasetId;
            this.TableId = tableId;
            this.AutoCreateTable = autoCreateTable ?? false;
            this.TableSchemaFile = tableSchemaFile;
            this.InsertIdFieldName = insertIdFieldName;

            // Load table schema file
            if (TableSchemaFile != null)
                TableSchema = LoadTableSchema(TableSchemaFile);
            if (TableSchema == null)
                throw new Exception("table schema not set");
            _BigQueryRowDataMap = new BigQueryRowDataMap(TableSchema, InsertIdFieldName);
            // Expand table id 1st time within force mode
            ExpandTableIdIfNecessary(force: true);
            // configure finished
            BigQuerySinkEventSource.Log.SinkStarted("TableId: " + TableIdExpanded);

            // Setup bigquery client
            var certificate = new X509Certificate2(
                privateKeyFile, 
                privateKeyPassphrase, 
                X509KeyStorageFlags.Exportable);

            ServiceAccountCredential credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer(serviceAccountEmail)
            {
                Scopes = new[] { BigqueryService.Scope.Bigquery,
                    BigqueryService.Scope.BigqueryInsertdata,
                BigqueryService.Scope.CloudPlatform,
                BigqueryService.Scope.DevstorageFullControl}
            }.FromCertificate(certificate));

            _BQSvc = new BigqueryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
            });
            _BackOff = new ExponentialBackOff();
            _onCompletedTimeout = onCompletedTimeout.Value;
            _bufferedPublisher = BufferedEventPublisher<EventEntry>.CreateAndStart(
                "BigQuery", 
                PublishEventsAsync, 
                bufferingInterval.Value,
                bufferingCount.Value, 
                maxBufferSize.Value, 
                _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
            if (_bufferedPublisher != null)
            {
                _bufferedPublisher.Dispose();
                _bufferedPublisher = null;
            }
            if (_BQSvc != null)
            {
                _BQSvc.Dispose();
                _BQSvc = null;
            }
        }

        public void OnCompleted()
        {
            FlushSafe();
            Dispose();
        }

        public void OnError(Exception error)
        {
            FlushSafe();
            Dispose();
        }

        /// <summary>
        /// Causes the buffer to be written immediately.
        /// </summary>
        /// <returns>The Task that flushes the buffer.</returns>
        public Task FlushAsync()
        {
            return _bufferedPublisher.FlushAsync();
        }

        private void FlushSafe()
        {
            try
            {
                FlushAsync().Wait(_onCompletedTimeout);
            }
            catch (AggregateException ex)
            {
                // Flush operation will already log errors. Never expose this exception to the observable.
                ex.Handle(e => e is FlushFailedException);
            }
        }

        public void OnNext(EventEntry value)
        {
            if (value == null)
                return;

            _bufferedPublisher.TryPost(value);
        }

        internal async Task<int> PublishEventsAsync(IEnumerable<EventEntry> collection)
        {
            try
            {
                var rows = CreateRows(collection);
                await InsertDataAsync(rows);
                return rows.Count;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                BigQuerySinkEventSource.Log.SinkUnexpectedError(
                    "Failed to write events to Bq: " + ex);
                throw;
            }
        }

        private IList<TableDataInsertAllRequest.RowsData> CreateRows(IEnumerable<EventEntry> collection)
        {
            return collection
                .Select(s => _BigQueryRowDataMap.CreateRowData(s)).ToList();
        }

        public async Task InsertDataAsync(IList<TableDataInsertAllRequest.RowsData> rows)
        {
            ExpandTableIdIfNecessary();
            await EnsureTableExistsAsync();

            var req = new TableDataInsertAllRequest
            {
                Rows = rows
            };
            var rowsCount = req.Rows.Count;
            var retry = 1;
            while (retry < _BackOff.MaxNumOfRetries)
            {
                try
                {
                    BigQuerySinkEventSource.Log.BigQueryInsertBegan(rowsCount);

                    var response = await _BQSvc.Tabledata.InsertAll(req,
                            ProjectId, DatasetId, TableIdExpanded).ExecuteAsync();
                    if (response.InsertErrors == null || !response.InsertErrors.Any())
                    {
                        BigQuerySinkEventSource.Log.BigQueryInserted(rowsCount);
                        return;
                    }

                    var messages = response.InsertErrors
                        .Zip(req.Rows, (x, r) => x.Errors.Select(e => new { x, r, e }).ToArray())
                        .SelectMany(xs => xs)
                        .Where(x => x.e.Reason != "stopped")
                        .Select(x =>
                        {
                            return string.Format(@"Index:{0}
DebugInfo:{1}
ETag:{2}
Location:{3}
Message:{4}
Reason:{5}
PostRawJSON:{6}", 
                                x.x.Index, x.e.DebugInfo, x.e.ETag, x.e.Location, 
                                x.e.Message, x.e.Reason, 
                                JsonConvert.SerializeObject(x.r.Json, Formatting.None));
                        });
                    BigQuerySinkEventSource.Log.BigQueryInsertFault(messages.ToString(), retry);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (GoogleApiException ex)
                {
                    BigQuerySinkEventSource.Log.BigQueryInsertFault(ex.ToString(), retry);
                    if (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        BigQuerySinkEventSource.Log.BigQueryInsertFault(ex.ToString(), retry);
                        return; // something wrong in authentication. no retry
                    }
                }
                catch (Exception ex)
                {
                    BigQuerySinkEventSource.Log.BigQueryInsertFault(ex.ToString(), retry);
                    return;
                }

                retry++;
                await Task.Delay(_BackOff.GetNextBackOff(retry));
            }

            BigQuerySinkEventSource.Log.BigQueryRetryOver(req.ToString());
        }

        async Task EnsureTableExistsAsync()
        {
            if (!AutoCreateTable)
                return;
            if (!_NeedCheckTableExists)
            {
                if (!await IsTableExistsAsync())
                {
                    await CreateTableAsync();
                }
                _NeedCheckTableExists = true;
            }
        }

        async Task<bool> IsTableExistsAsync()
        {
            try
            {
                var table = await _BQSvc.Tables.Get(ProjectId, DatasetId, TableIdExpanded).ExecuteAsync();
                return true;
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound &&
                    ex.Message.Contains("Not found: Table"))
                {
                    return false;
                }
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        async Task CreateTableAsync()
        {
            try
            {
                var table = new Table
                {
                    TableReference = new TableReference
                    {
                        ProjectId = ProjectId,
                        DatasetId = DatasetId,
                        TableId = TableIdExpanded
                    },
                    Schema = TableSchema,
                };

                await _BQSvc.Tables.Insert(table, ProjectId, DatasetId).ExecuteAsync();
                BigQuerySinkEventSource.Log.BigQueryTableCreated(TableIdExpanded);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == System.Net.HttpStatusCode.Conflict && 
                    ex.Message.Contains("Already Exists: Table"))
                {
                    return;
                }
                BigQuerySinkEventSource.Log.BigQueryTableCreatFailed(ex.ToString());
            }
            catch (Exception ex)
            {
                BigQuerySinkEventSource.Log.BigQueryTableCreatFailed(ex.ToString());
                throw;
            }
        }

        TableSchema LoadTableSchema(string schemaFile)
        {
            try
            {
                var strJson = File.ReadAllText(schemaFile);
                var schema = new TableSchema();
                schema.Fields = JsonConvert.DeserializeObject<List<TableFieldSchema>>(strJson);
                return schema;
            }
            catch (Exception ex)
            {
                BigQuerySinkEventSource.Log.SinkLoadSchemaFailed(ex.ToString());
                throw;
            }
        }

        void ExpandTableIdIfNecessary(bool force = false)
        {
            if (force || _TableIdExpandable)
            {
                this.TableIdExpanded = Regex.Replace(TableId, @"(\{.+})", delegate (Match m) {
                    var pattern = m.Value.Substring(1, m.Value.Length - 2);
                    _TableIdExpandable = true;
                    return DateTime.UtcNow.ToString(pattern);
                });
            }
        }

        class BigQueryRowDataMap
        {
            public TableSchema TableSchema { get; private set; }
            public string InsertIdFieldName { get; private set; }

            class MapEntry
            {
                public string Name { get; set; }
                public Func<EventEntry, object> GetValFunc { get; set; }
            }

            private List<MapEntry> MapEntries;
            private Func<string> GetInsertIdFunc { get; set; }
            private Func<EventEntry, object> GetValFuncForGetInsertIdFunc { get; set; }

            public BigQueryRowDataMap(TableSchema tableSchema, string insertIdFieldName)
            {
                this.TableSchema = tableSchema;
                this.InsertIdFieldName = insertIdFieldName;
            }

            public TableDataInsertAllRequest.RowsData CreateRowData(EventEntry eventEntry)
            {
                if (MapEntries == null)
                {
                    CreateMap(eventEntry);
                }

                var properties = new Dictionary<string, object>();
                foreach (var entry in MapEntries)
                {
                    properties[entry.Name] = entry.GetValFunc(eventEntry);
                }

                string insertId = null;
                if (GetInsertIdFunc != null)
                {
                    insertId = GetInsertIdFunc();
                }

                return new TableDataInsertAllRequest.RowsData
                {
                    InsertId = insertId,
                    Json = properties
                };
            }

            private void CreateMap(EventEntry eventEntry)
            {
                MapEntries = new List<MapEntry>();
                foreach (var fieldSchema in TableSchema.Fields)
                {
                    var entry = new MapEntry();
                    entry.Name = fieldSchema.Name;
                    entry.GetValFunc = GetGetValueFuncByBigQueryFieldName(eventEntry, fieldSchema.Name);
                    MapEntries.Add(entry);
                }

                GetInsertIdFunc = null;
                if (InsertIdFieldName != null)
                {
                    if (InsertIdFieldName == "%uuid%")
                    {
                        GetInsertIdFunc = () => Guid.NewGuid().ToString();
                    }
                    else
                    {
                        GetValFuncForGetInsertIdFunc = GetGetValueFuncByBigQueryFieldName(
                            eventEntry, InsertIdFieldName);
                        GetInsertIdFunc = () => GetValFuncForGetInsertIdFunc(eventEntry).ToString();
                    }
                }
            }

            /// <summary>
            /// Get function that return value that corresponds to BigQuery field
            /// </summary>
            /// <param name="eventEntry"></param>
            /// <param name="fieldName"></param>
            /// <returns>string, int, DateTime</returns>
            private Func<EventEntry, object> GetGetValueFuncByBigQueryFieldName(
                EventEntry eventEntry, string fieldName)
            {
                // First, find from payloads
                for (var i = 0; i < eventEntry.Payload.Count; i++)
                {
                    if (fieldName == eventEntry.Schema.Payload[i])
                    {
                        return (e) => e.Payload[i];
                    }
                }

                // Second, find from built-in EventEntry properties
                switch (fieldName)
                {
                    case "EventId": return (e) => e.EventId;
                    case "EventName": return (e) => e.Schema.EventName;
                    case "Level": return (e) => (int)e.Schema.Level;
                    case "FormattedMessage": return (e) => e.FormattedMessage;
                    case "Keywords": return (e) => (long)e.Schema.Keywords;
                    case "KeywordsDescription": return (e) => e.Schema.KeywordsDescription;
                    case "Task": return (e) => (int)e.Schema.Task;
                    case "TaskName": return (e) => e.Schema.TaskName;
                    case "Opcode": return (e) => (int)e.Schema.Opcode;
                    case "OpcodeName": return (e) => e.Schema.OpcodeName;
                    case "Timestamp": return (e) => e.Timestamp;
                    case "ProcessId": return (e) => e.ProcessId;
                    case "ThreadId": return (e) => e.ThreadId;
                    case "ProviderId": return (e) => e.ProviderId.ToString();
                    case "ProviderName": return (e) => e.Schema.ProviderName;
                    case "Version": return (e) => e.Schema.Version;
                    case "ActivityId": return (e) => e.ActivityId.ToString();
                    case "RelatedActivityId": return (e) => e.RelatedActivityId.ToString();
                }

                throw new Exception($"No value for the field({fieldName})");
            }
        }
    }
}
