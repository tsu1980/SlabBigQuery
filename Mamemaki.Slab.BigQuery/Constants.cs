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
        /// The default max timeout for flushing all pending events in the buffer.
        /// </summary>
        public static readonly TimeSpan DefaultBufferingFlushAllTimeout = TimeSpan.FromMinutes(1);
    }
}
