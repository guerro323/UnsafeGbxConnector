using System;
using System.Diagnostics;
using U8Xml;

namespace UnsafeGbxConnector.Serialization.Readers
{
    public readonly struct GbxElementReader
    {
        public readonly XmlNode Xml;

        public GbxElementReader(XmlNode xmlParams)
        {
            Debug.Assert(!xmlParams.IsNull, "!xmlParams.IsNull");
            Xml = xmlParams;
        }

        public bool TryGet(out GbxStructReader gbxStructReader)
        {
            if (Xml.Name == "struct")
            {
                gbxStructReader = new GbxStructReader(Xml);
                return true;
            }

            gbxStructReader = default;
            return false;
        }

        public bool TryGet(out GbxReader gbxStructReader)
        {
            if (Xml.Name == "array")
            {
                // FirstChild = data, <array><data>...
                gbxStructReader = new GbxReader(Xml.FirstChild.Value, true);
                return true;
            }

            gbxStructReader = default;
            return false;
        }

        public GbxStructReader AsStruct()
        {
            if (!TryGet(out GbxStructReader structReader))
                throw new InvalidOperationException("not a struct");

            return structReader;
        }

        public GbxReader AsArray()
        {
            if (!TryGet(out GbxReader structReader))
                throw new InvalidOperationException("not a array");

            return structReader;
        }
    }
}