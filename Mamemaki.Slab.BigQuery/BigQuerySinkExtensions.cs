using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery
{
    public static class BigQuerySinkExtensions
    {
        public static SinkSubscription<BigQuerySink> LogToBigQuery(
            this IObservable<EventEntry> eventStream,
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
            TimeSpan? bufferingFlushAllTimeout = null,
            int? maxBufferSize = null)
        {
            var sink = new BigQuerySink(
                projectId,
                datasetId,
                tableId,
                authMethod,
                serviceAccountEmail,
                privateKeyFile,
                privateKeyPassphrase,
                autoCreateTable,
                tableSchemaFile,
                insertIdFieldName,
                bufferingInterval,
                bufferingCount,
                bufferingFlushAllTimeout,
                maxBufferSize);

            var subscription = eventStream.Subscribe(sink);

            return new SinkSubscription<BigQuerySink>(subscription, sink);
        }
    }
}
