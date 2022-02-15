using System;
using System.Threading.Tasks;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Serialization.Readers;
using UnsafeGbxConnector.Serialization.Writers;

namespace UnsafeGbxConnector
{
    public static class GbxConnectionExtensions
    {
        /// <summary>
        /// Queue a packet without caring about the result
        /// </summary>
        /// <param name="self">The gbx connection</param>
        /// <param name="packet">Packet data</param>
        /// <typeparam name="T">The type of packet to send</typeparam>
        public static void Queue<T>(this GbxConnection self, T packet)
            where T : IGbxPacket
        {
            self.Queue(GbxWriter.From(packet));
        }

        /// <summary>
        /// Queue a writer data and get the result asynchronously in a packet
        /// </summary>
        /// <param name="self">The gbx connection</param>
        /// <param name="gbx">The writer data</param>
        /// <typeparam name="TOut">The type of the packet for receiving</typeparam>
        /// <returns>An option that indicate either an error or a result of type <see cref="TOut"/></returns>
        public static async Task<GbxResponseOption<TOut>> QueueAsync<TOut>
        (
            this GbxConnection self,
            GbxWriter gbx
        )
            where TOut : struct, IGbxPacket
        {
            return await self.QueueAsync(gbx, msg =>
            {
                if (msg.IsError)
                    return new GbxResponseOption<TOut>(
                        msg.Error
                    );

                var result = default(TOut);
                result.Read(msg.Reader);

                return new GbxResponseOption<TOut>(
                    result
                );
            });
        }

        /// <summary>
        /// Queue a packet and get the result asynchronously
        /// </summary>
        /// <param name="self">The gbx connection</param>
        /// <param name="packet">Packet data</param>
        /// <typeparam name="TIn">The type of the packet for sending</typeparam>
        /// <typeparam name="TOut">The type of the packet for receiving</typeparam>
        /// <returns>An option that indicate either an error or a result of type <see cref="TOut"/></returns>
        public static Task<GbxResponseOption<TOut>> QueueAsync<TIn, TOut>(this GbxConnection self, TIn packet)
            where TIn : struct, IGbxPacket
            where TOut : struct, IGbxPacket
        {
            return QueueAsync<TOut>(self, GbxWriter.From(packet));
        }

        /// <summary>
        /// Queue a packet and get the result asynchronously.
        /// </summary>
        /// <param name="self">The gbx connection</param>
        /// <param name="packet">Packet data</param>
        /// <typeparam name="T">The type of the packet (in/out)</typeparam>
        /// <returns>An option that indicate either an error or a result of type <see cref="T"/></returns>
        public static Task<GbxResponseOption<T>> QueueAsync<T>(this GbxConnection self, T packet)
            where T : struct, IGbxPacket
        {
            return QueueAsync<T, T>(self, packet);
        }
    }
}