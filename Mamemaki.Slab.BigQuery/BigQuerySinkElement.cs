using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using System.Xml.Linq;

namespace Mamemaki.Slab.BigQuery
{
    public class BigQuerySinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("bigQuerySink", Constants.Namespace);

        public bool CanCreateSink(XElement element)
        {
            if (element == null) throw new ArgumentNullException("element");
            return element.Name == sinkName;
        }

        public IObserver<EventEntry> CreateSink(XElement element)
        {
            if (element == null) throw new ArgumentNullException("element");

            return new BigQuerySink(
                (string)element.Attribute("projectId"),
                (string)element.Attribute("datasetId"),
                (string)element.Attribute("tableId"),
                (string)element.Attribute("authMethod"),
                (string)element.Attribute("serviceAccountEmail"),
                (string)element.Attribute("privateKeyFile"),
                (string)element.Attribute("privateKeyPassphrase"),
                (bool?)element.Attribute("autoCreateTable"),
                (string)element.Attribute("tableSchemaFile"),
                (string)element.Attribute("insertIdFieldName"),
                element.Attribute("bufferingIntervalInSeconds").ToTimeSpan(),
                (int?)element.Attribute("bufferingCount"),
                element.Attribute("bufferingFlushAllTimeoutInSeconds").ToTimeSpan(),
                (int?)element.Attribute("maxBufferSize"));
        }
    }
}
