using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace UnsafeGbxConnector.Serialization.Writers
{
    public struct GbxStructArrayWriter : IDisposable
    {
        private readonly StringBuilder _stringBuilder;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GbxStructArrayWriter(string member, StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
            _stringBuilder.Append("<member><name>");
            {
                _stringBuilder.Append(member);
            }
            _stringBuilder.Append("</name>");
            _stringBuilder.Append("<value><array><data>");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _stringBuilder.Append("</data></array></value></member>");
        }

        public readonly void AddInt(int val)
        {
            var p = new GbxWriter.ValueParam("int", _stringBuilder);
            _stringBuilder.Append(val);
            p.Dispose();
        }

        public readonly void AddString(string val)
        {
            var p = new GbxWriter.ValueParam("string", _stringBuilder);
            _stringBuilder.Append(WebUtility.HtmlEncode(val));
            p.Dispose();
        }
        
        public readonly void AddBase64(string val)
        {
            var p = new GbxWriter.ValueParam("base64", _stringBuilder);
            _stringBuilder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(val)));
            p.Dispose();
        }

        public readonly void AddBool(bool val)
        {
            var p = new GbxWriter.ValueParam("boolean", _stringBuilder);
            _stringBuilder.Append(val ? 1 : 0);
            p.Dispose();
        }
    }
}