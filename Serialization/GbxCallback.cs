using System;
using U8Xml;
using UnsafeGbxConnector.Serialization.Readers;

namespace UnsafeGbxConnector.Serialization
{
    public readonly struct GbxCallback
    {
        private readonly RawString _methodName;

        public readonly GbxReader Reader;

        public GbxCallback(RawString methodName, GbxReader reader)
        {
            _methodName = methodName;
            Reader = reader;
        }

        public bool Match(ReadOnlySpan<char> method)
        {
            return _methodName == method;
        }

        public bool Match(ReadOnlySpan<char> method1, ReadOnlySpan<char> method2)
        {
            return _methodName == method1 || _methodName == method2;
        }

        public override string ToString()
        {
            return $"GbxCallback ({_methodName.ToString()})";
        }
    }
}