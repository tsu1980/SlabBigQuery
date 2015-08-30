using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery
{
    public static class Constants
    {
        /// <summary>
        /// The configuration namespace.
        /// </summary>
        public const string Namespace = "urn:mamemaki.slab.bigquerysink";

        /// <summary>
        /// The default buffering interval.
        /// </summary>
        public static readonly TimeSpan DefaultBufferingInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default buffering count.
        /// </summary>
        public const int DefaultBufferingCount = 200;

        /// <summary>
        /// The maximum number of entries that can be buffered while it's sending to server
        /// before the sink starts dropping entries.
        /// </summary>
        public const int DefaultMaxBufferSize = 30000;

        /// <summary>
        /// The default max timeout for flushing all pending events in the buffer.
        /// </summary>
        public static readonly TimeSpan DefaultBufferingFlushAllTimeout = TimeSpan.FromMinutes(1);
    }
}
