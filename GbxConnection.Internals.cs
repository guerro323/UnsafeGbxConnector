using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Serialization.Writers;

namespace UnsafeGbxConnector
{
    public partial class GbxConnection
    {
        private int CreateArray(in ReadOnlySpan<char> chars, out byte[] callBytes, out uint callHandle)
        {
            var expectedEncodingCount = Encoding.UTF8.GetByteCount(chars);
            if (expectedEncodingCount > _encodingBuffer.Length)
                Array.Resize(ref _encodingBuffer, expectedEncodingCount);

            var bodyLength = Encoding.UTF8.GetBytes(chars, _encodingBuffer);
            if (bodyLength <= 0)
                throw new InvalidOperationException();

            callHandle = --_handle;

            // Body Length (int) -> Handle Id (int) -> Body Data (bytes)
            var callLength = sizeof(int) * 2 + bodyLength;
            callBytes = ArrayPool<byte>.Shared.Rent(callLength);

            var span = callBytes.AsSpan();
            {
                MemoryMarshal.Write(span, ref bodyLength);
                MemoryMarshal.Write(span[sizeof(int)..], ref callHandle);

                _encodingBuffer
                    .AsSpan(0, bodyLength)
                    .CopyTo(span[(sizeof(int) * 2)..]);
            }

            return callLength;
        }

        private void SendMessage(ReadOnlySpan<char> chars, Action<GbxResponse>?[] actionArray)
        {
            if (!_tcpClient.Connected)
                throw new InvalidOperationException("not connected");

            var callLength = CreateArray(chars, out var callBytes, out var callHandle);

            using (_dictionarySynchronization.Synchronize())
            {
                _outboundMultiCalls[callHandle] = actionArray;
            }

            var stream = _tcpClient.GetStream();
            stream.Write(callBytes.AsSpan(0, callLength));
            stream.FlushAsync();

            ArrayPool<byte>.Shared.Return(callBytes);
        }

        private struct QueuedMessage
        {
            public Action<GbxResponse>? Action;
            public GbxWriter Gbx;
        }
    }
}