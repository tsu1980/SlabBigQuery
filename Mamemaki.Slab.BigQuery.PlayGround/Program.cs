using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery.PlayGround
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Mamemaki.Slab.BigQuery.PlayGround.exe <projectId> <datasetId> <tableId> <serviceAccountEmail> <serviceAccountCertFile>");
                return;
            }
            var projectId = args[0];
            var datasetId = args[1];
            var tableId = args[2];
            var serviceAccountEmail = args[3];
            var serviceAccountCertFile = args[4];
            var tableSchemaFile = args[5];

            using (var listenerConsole = new ObservableEventListener())
            using (var listener = new ObservableEventListener())
            {
                var formatterConsole = new EventTextFormatter(
                    "+=========================================+");

                // Setup listener for debug
                listenerConsole.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.LogAlways);
                listenerConsole.EnableEvents(BigQuerySinkEventSource.Log, EventLevel.LogAlways);
                listenerConsole.LogToConsole(formatterConsole);

                // Setup listener for playgrond
                listener.EnableEvents(PlayGroundEventSource.Log, EventLevel.LogAlways);
                listener.LogToConsole(formatterConsole);
                listener.LogToBigQuery(
                    projectId: projectId,
                    datasetId: datasetId,
                    tableId: tableId,
                    authMethod: "private_key",
                    serviceAccountEmail: serviceAccountEmail,
                    serviceAccountCertFile: serviceAccountCertFile,
                    serviceAccountCertPassphrase: "notasecret",
                    autoCreateTable: true,
                    tableSchemaFile: tableSchemaFile,
                    insertIdFieldName: "%uuid%",
                    bufferingInterval: TimeSpan.FromSeconds(1),
                    bufferingCount: 3,
                    onCompletedTimeout: Constants.DefaultBufferingFlushAllTimeout,
                    maxBufferSize: 30000);

                InsertRows(3);
                System.Threading.Thread.Sleep(100);
                InsertRows(3);
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        static void InsertRows(int count)
        {
            var rows = Enumerable.Range(0, count);
            foreach (var item in rows)
            {
                PlayGroundEventSource.Log.Count(item);
            }
        }
    }
}
