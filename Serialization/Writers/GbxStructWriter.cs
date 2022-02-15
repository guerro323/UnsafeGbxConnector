using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace UnsafeGbxConnector.Serialization.Writers
{
    public struct GbxStructWriter : IDisposable
    {
        private readonly StringBuilder _stringBuilder;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GbxStructWriter(StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
            _stringBuilder.Append("<value><struct>");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _stringBuilder.Append("</struct></value>");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMember(string name)
        {
        }

        public readonly void WriteInt(string member, int val)
        {
            var p = new MemberParam(member, "int", _stringBuilder);
            _stringBuilder.Append(val);
            p.Dispose();
        }

        public readonly void WriteString(string member, string val)
        {
            var p = new MemberParam(member, "string", _stringBuilder);
            _stringBuilder.Append(WebUtility.HtmlEncode(val));
            p.Dispose();
        }
        
        public readonly void WriteBase64(string member, string val)
        {
            var p = new MemberParam(member, "base64", _stringBuilder);
            _stringBuilder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(val)));
            p.Dispose();
        }

        public readonly void WriteBool(string member, bool val)
        {
            var p = new MemberParam(member, "boolean", _stringBuilder);
            _stringBuilder.Append(val ? 1 : 0);
            p.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly GbxStructArrayWriter BeginArray(string member)
        {
            return new GbxStructArrayWriter(member, _stringBuilder);
        }

        private struct MemberParam
        {
            private readonly StringBuilder _stringBuilder;
            private GbxWriter.ValueParam _valueParam;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public MemberParam(string name, string type, StringBuilder stringBuilder)
            {
                _stringBuilder = stringBuilder;

                _stringBuilder.Append("<member><name>");
                _stringBuilder.Append(name);
                _stringBuilder.Append("</name>");
                
                _valueParam = new GbxWriter.ValueParam(type, stringBuilder);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _valueParam.Dispose();
                _stringBuilder.Append("</member>");
            }
        }
    }
}