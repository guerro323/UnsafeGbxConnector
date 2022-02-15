using System;
using System.Runtime.CompilerServices;
using U8Xml;

namespace UnsafeGbxConnector.Serialization.Readers
{
    public readonly struct GbxReader : IDisposable
    {
        private readonly XmlObject? _xml;

        public readonly XmlNode Root;

        private readonly bool _paramDepth;

        public GbxReader(ReadOnlySpan<char> xml, bool paramDepth = false)
        {
            _xml = XmlParser.Parse(xml);
            Root = _xml.Root;

            _paramDepth = paramDepth;
        }

        public GbxReader(XmlNode root, bool paramDepth = false)
        {
            _xml = null;
            Root = root;

            _paramDepth = paramDepth;
        }

        public bool TryReadAt(out GbxElementReader element, int index)
        {
            if (!Root.FirstChild.TryGetValue(out var xmlNode))
            {
                element = default;
                return false;
            }

            for (var i = 0; i <= index; i++)
            {
                if (i == index)
                {
                    if (_paramDepth)
                        xmlNode = xmlNode.FirstChild.Value;

                    element = new GbxElementReader(xmlNode);
                    return true;
                }

                if (!xmlNode.TryNextSibling(out xmlNode))
                    break;
            }

            element = default;
            return false;
        }

        public GbxMemberReader this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!TryReadAt(out var response, index))
                    throw new IndexOutOfRangeException($"no data found at {index}");

                return new GbxMemberReader(response.Xml);
            }
        }

        public void Dispose()
        {
            _xml?.Dispose();
        }
    }
}