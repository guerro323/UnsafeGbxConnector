using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using U8Xml;

namespace UnsafeGbxConnector.Serialization.Readers
{
    public readonly struct GbxMemberReader
    {
        public readonly XmlNode Xml;

        public GbxMemberReader(XmlNode xmlValue)
        {
            Xml = xmlValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RawString GetRaw()
        {
            return Xml.FirstChild.Value.InnerText;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            return GetRaw().ToString();
        }

        public byte[] ReadBase64()
        {
            return GetRaw().ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
            return GetRaw().ToInt32();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            var raw = GetRaw();
            if (raw.TryToUInt8(out var b))
                return b > 0;

            if (Utf8Parser.TryParse(raw.AsSpan(), out bool result, out _))
                return result;

            throw new InvalidOperationException("Couldn't get bool");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GbxReader ReadArray()
        {
            if (Xml.Name != "array")
                throw new InvalidOperationException($"not an array ({Xml.Name})");

            // FirstChild = data, <array><data>...
            return new GbxReader(Xml.FirstChild.Value, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GbxStructReader ReadStruct()
        {
            if (Xml.Name != "struct")
                throw new InvalidOperationException($"not a struct ({Xml.Name})");

            return new GbxStructReader(Xml);
        }
    }
}