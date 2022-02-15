using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using UnsafeGbxConnector.Utilities;

namespace UnsafeGbxConnector.Serialization.Writers
{
    public struct GbxWriter
    {
        private readonly StringBuilder _stringBuilder;

        public GbxWriter(string methodName, int capacity = 0)
        {
            _stringBuilder = StringBuilderPool.Get();

            _stringBuilder.Append(GbxWriterInternal.BeginWrite);
            _stringBuilder.Append(methodName);
            _stringBuilder.Append(GbxWriterInternal.EndWrite);

            Length = -1;
        }

        public int Length { get; private set; }

        public readonly void WriteInt(int val)
        {
            var p = new ValueParam("int", _stringBuilder);
            _stringBuilder.Append(val);
            p.Dispose();
        }

        public readonly void WriteString(string val)
        {
            // We could have 'val' as a span, but WebUtility.HtmlEncode use string
            // (but in the back it support ReadOnlySpan<char> so why???)
            
            var p = new ValueParam("string", _stringBuilder);
            _stringBuilder.Append(WebUtility.HtmlEncode(val));
            p.Dispose();
        }
        
        public readonly void WriteBase64(ReadOnlySpan<byte> val)
        {
            // no need to optimize this for GC usage
            // most of the time, val will be really big that GC.Collect will be called
            
            var p = new ValueParam("base64", _stringBuilder);
            _stringBuilder.Append(Convert.ToBase64String(val));
            p.Dispose();
        }

        public readonly void WriteBase64(string val)
        {
            // no need to optimize this for GC usage
            // most of the time, val will be really big that GC.Collect will be called
            
            var p = new ValueParam("base64", _stringBuilder);
            _stringBuilder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(val)));
            p.Dispose();
        }

        public readonly void WriteBool(bool val)
        {
            var p = new ValueParam("boolean", _stringBuilder);
            _stringBuilder.Append(val ? 1 : 0);
            p.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly GbxArrayWriter BeginArray()
        {
            return new GbxArrayWriter(_stringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly GbxStructWriter BeginStruct()
        {
            return new GbxStructWriter(_stringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            if (Length >= 0)
                throw new InvalidOperationException("Complete() called twice");

            _stringBuilder.Append(GbxWriterInternal.EndToMessageWrite);
            Length = _stringBuilder.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringBuilder GetResult()
        {
            return _stringBuilder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            StringBuilderPool.Recycle(_stringBuilder);
            this = default;
        }

        public static GbxWriter From<T>(T packet)
            where T : IGbxPacket
        {
            var writer = new GbxWriter(packet.GetMethodName());
            packet.Write(in writer);
            return writer;
        }

        internal struct ValueParam : IDisposable
        {
            private readonly StringBuilder _stringBuilder;
            private readonly string _type;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ValueParam(string type, StringBuilder sb)
            {
                _type = type;

                _stringBuilder = sb;
                _stringBuilder.Append("<value><");
                _stringBuilder.Append(type);
                _stringBuilder.Append('>');
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _stringBuilder.Append("</");
                _stringBuilder.Append(_type);
                _stringBuilder.Append("></value>");
            }
        }
    }
}