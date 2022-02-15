using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using U8Xml;

namespace UnsafeGbxConnector.Serialization.Readers
{
    public readonly struct GbxStructReader
    {
        public readonly XmlNode Xml;

        public GbxStructReader(XmlNode xmlStruct)
        {
            Xml = xmlStruct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(string name, out GbxMemberReader reader)
        {
            foreach (var child in Xml.Children)
                if (child.TryFindChild("name", out var xmlName)
                    && xmlName.InnerText == name)
                {
                    if (!child.TryFindChild("value", out var xmlValue))
                        throw new InvalidOperationException($"expected member '{name}' to have 'value'");

                    reader = new GbxMemberReader(xmlValue);
                    return true;
                }

            reader = default;
            return false;
        }

        public GbxMemberReader this[string name]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGet(name, out var reader))
                    return reader;

                throw new KeyNotFoundException(name);
            }
        }
    }
}