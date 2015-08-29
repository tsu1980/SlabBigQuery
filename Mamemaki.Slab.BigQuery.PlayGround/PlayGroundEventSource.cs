using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mamemaki.Slab.BigQuery.PlayGround
{
    [EventSource(Name = "Mamemaki-SlabBigQuery-PlayGround")]
    class PlayGroundEventSource : EventSource
    {
        private static readonly Lazy<PlayGroundEventSource> Instance = 
            new Lazy<PlayGroundEventSource>(() => new PlayGroundEventSource());

        private PlayGroundEventSource()
        {
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="PlayGroundEventSource"/>.
        /// </summary>
        /// <value>The singleton instance.</value>
        public static PlayGroundEventSource Log
        {
            get { return Instance.Value; }
        }

        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Trace, Message = "Message: {0}")]
        public void Trace(string message)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, message);
            }
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Trace, Message = "Count: {0}")]
        public void Count(int cnt)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(2, cnt);
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
            public const EventKeywords Trace = (EventKeywords)0x0001;
        }
    }
}
