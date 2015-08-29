using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery
{
    [EventSource(Name = "Mamemaki-SlabBigQuery")]
    public sealed class BigQuerySinkEventSource : EventSource
    {
        private static readonly Lazy<BigQuerySinkEventSource> Instance = new Lazy<BigQuerySinkEventSource>(() => new BigQuerySinkEventSource());

        private BigQuerySinkEventSource()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="BigQuerySinkEventSource"/>.
        /// </summary>
        /// <value>The singleton instance.</value>
        public static BigQuerySinkEventSource Log
        {
            get { return Instance.Value; }
        }

        [Event(1, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "Unexpected error occurred: {0}")]
        public void SinkUnexpectedError(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Sink, Message = "Sink started: {0}")]
        public void SinkStarted(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, message);
            }
        }

        [Event(3, Level = EventLevel.Informational, Keywords = Keywords.Sink, Message = "Sink ended: {0}")]
        public void SinkEnded(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(3, message);
            }
        }

        [Event(4, Level = EventLevel.Error, Keywords = Keywords.Sink, Message = "Failed to load schema: {0}")]
        public void SinkLoadSchemaFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(4, message);
            }
        }

        [Event(100, Level = EventLevel.Error, Keywords = Keywords.BigQuery, Message = "Insert fault occurred: {0}")]
        public void BigQueryInsertFault(string message, int retryCount)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(100, message);
            }
        }

        [Event(102, Level = EventLevel.Critical, Keywords = Keywords.BigQuery, Message = "Retry over: {0}")]
        public void BigQueryRetryOver(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(102, message);
            }
        }

        [Event(103, Level = EventLevel.Informational, Keywords = Keywords.BigQuery, Message = "Table created: {0}")]
        public void BigQueryTableCreated(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(103, message);
            }
        }

        [Event(104, Level = EventLevel.Error, Keywords = Keywords.BigQuery, Message = "Failed to create table: {0}")]
        public void BigQueryTableCreatFailed(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(104, message);
            }
        }

        [Event(111, Level = EventLevel.Verbose, Keywords = Keywords.BigQuery, Message = "Insert began")]
        public void BigQueryInsertBegan(int rowsCount)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(111, rowsCount);
            }
        }

        [Event(112, Level = EventLevel.Verbose, Keywords = Keywords.BigQuery, Message = "Inserted successfully")]
        public void BigQueryInserted(int rowsCount)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(112, rowsCount);
            }
        }

        /// <summary>
        /// Custom defined event keywords.
        /// </summary>
        public static class Keywords
        {
            /// <summary>
            /// Keyword for sink.
            /// </summary>
            public const EventKeywords Sink = (EventKeywords)0x0001;

            /// <summary>
            /// Keyword for Google BigQuery.
            /// </summary>
            public const EventKeywords BigQuery = (EventKeywords)0x0002;
        }
    }
}
