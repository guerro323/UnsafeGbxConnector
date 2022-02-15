using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Serialization.Writers;

namespace UnsafeGbxConnector
{
    public partial class GbxConnection
    {
        // The real limit is 512, but we limit to 400 here by default
        /// <summary>
        /// How many calls can be batched per <see cref="SendCore"/>
        /// </summary>
        public int MaximumCallsPerBatch { get; init; } = 400;

        // how much calls can be made in SendCore()
        private int _queueableCallLimit = -1;

        // queued messages for next SendCore() call
        private readonly List<QueuedMessage> _frameMessages = new();
        
        // used for reducing gc stress
        private char[] _chars = Array.Empty<char>();

        private bool SendCore()
        {
            // Copy messages into a process collection to not lock up other threads when queuing
            using (_createSynchronization.Synchronize())
            {
                _frameMessages.Clear();

                _queueableCallLimit = Math.Min(_queueableCallLimit, MaximumCallsPerBatch);

                var queuedMessageSpan = CollectionsMarshal.AsSpan(_queuedMessages);
                var length = Math.Min(queuedMessageSpan.Length, _queueableCallLimit);
                if (length <= 0) return true;

                // We can only send up to 512 methods in a multi-call
                foreach (var message in queuedMessageSpan[..length])
                    _frameMessages.Add(message);

                _queueableCallLimit -= length;

                _queuedMessages.RemoveRange(0, length);
                // Don't use _queuedMessages anymore after synchronization in the method.
            }

            if (_frameMessages.Count == 0)
                return true;

            Stopwatch sw = null;
            if (Logger.IsTraceEnabled)
            {
                sw = new Stopwatch();
                sw.Start();
            }

            // ugly, but it's performant (we could do something like GbxWriterInternal)
            const string messageHeader = @"<?xml version=""1.0"" encoding=""utf-8"" ?>";
            const string openMethod =
                "<methodCall><methodName>system.multicall</methodName><params><param><value><array><data>";
            const string closeMethod = "</data></array></value></param></params></methodCall>";
            const string openStruct = "<value><struct>";
            const string closeStruct = "</struct></value>";

            var allocateSize = messageHeader.Length
                               + openMethod.Length + closeMethod.Length
                               + (openStruct.Length + closeStruct.Length) * _frameMessages.Count;

            // Possible optimization:
            // - Get the start index of each messages,
            //   and then create the multicall in parallel. Since we will have the index it will be 100% safe and fast.
            //   But before, I will need to reimplement BatchRunner into RevGhost (to have GC free parallelization)
            //   
            //   It's not yet needed since sending a lot of messages at the same time is rare, and it's already quite
            //   performant

            // Finalize writers
            foreach (ref var queued in CollectionsMarshal.AsSpan(_frameMessages))
            {
                ref var writer = ref queued.Gbx;
                writer.Complete();

                allocateSize += writer.Length;
            }

            try
            {
                if (allocateSize > _chars.Length)
                    Array.Resize(ref _chars, allocateSize);

                var actionArray = _multiThreadTcsPool.Rent(_frameMessages.Count);

                var charSpan = _chars.AsSpan();
                // Copy the header at the beginning
                messageHeader.AsSpan().CopyTo(charSpan);
                charSpan = charSpan[messageHeader.Length..];

                // Copy <array>
                openMethod.AsSpan().CopyTo(charSpan);
                charSpan = charSpan[openMethod.Length..];

                var span = CollectionsMarshal.AsSpan(_frameMessages);
                for (var i = 0; i < span.Length; i++)
                {
                    ref var queued = ref span[i];

                    var message = queued.Gbx.GetResult();
                    // Put queued tcs into another array
                    actionArray[i] = queued.Action;

                    // Copy <struct>
                    openStruct.AsSpan().CopyTo(charSpan);
                    charSpan = charSpan[openStruct.Length..];

                    // Copy message content
                    message.CopyTo(0, charSpan, message.Length);
                    charSpan = charSpan[message.Length..];

                    // Copy </struct>
                    closeStruct.AsSpan().CopyTo(charSpan);
                    charSpan = charSpan[closeStruct.Length..];
                }

                // Copy </array>
                closeMethod.AsSpan().CopyTo(charSpan);
                charSpan = charSpan[closeMethod.Length..];
                
                // Dispose now
                foreach (ref var queued in CollectionsMarshal.AsSpan(_frameMessages))
                {
                    ref var writer = ref queued.Gbx;
                    writer.Dispose();
                }

                SendMessage(_chars.AsSpan(0, allocateSize), actionArray);
            }
            catch (Exception ex)
            {
                if (Logger.IsFatalEnabled)
                    Logger.Fatal(ex, "Multicall failed!");
                else
                    throw;
            }

            if (Logger.IsTraceEnabled)
            {
                var stopWatch = sw!;

                stopWatch.Stop();

                Logger.Trace("Took {time}ms to send {packetCount} batched packets",
                    stopWatch.Elapsed.TotalMilliseconds,
                    _frameMessages.Count);
            }

            return true;
        }

        public bool SendUpdates()
        {
            if (EnableReceiveThread)
                throw new InvalidOperationException("SendUpdates is already being threaded");

            return SendCore();
        }

        private void SendLoop()
        {
            if (_queueableCallLimit == -1)
                _queueableCallLimit = MaximumCallsPerBatch;

            while (false == _ccs.IsCancellationRequested)
            {
                if (SendCore() == false)
                    return;

                // Maybe revisit the dynamic sleeping time (and default value)
                // The best default value I think is between 2 and 4 milliseconds, since this would mean
                // that a lot of calls should be batched in that short span
                // But having a low sleeping time would mean a lower latency (eg: 1ms).
                // I think that for now 2ms is a good balance.
                //
                // Maybe the sleep time can be decided by last message roundtrip time?
                // (so if we send a call, and we receive the result of it 1ms after, then we can decide to sleep
                //  for a longer time, since we know that the server will sleep for some ms at that point)

                switch (_queueableCallLimit)
                {
                    case <= 0:
                        Thread.Sleep(25);
                        break;
                    case <= 100:
                        Thread.Sleep(5);
                        break;
                    default:
                        Thread.Sleep(2);
                        break;
                }
            }
        }

        /// <summary>
        /// Queue a <see cref="GbxWriter"/> to be sent on the next <see cref="SendCore"/> call.
        /// </summary>
        /// <param name="gbx">The writer</param>
        public void Queue(GbxWriter gbx)
        {
            _createSynchronization.Lock();
            _queuedMessages.Add(new QueuedMessage
            {
                Action = null,
                Gbx = gbx
            });
            _createSynchronization.Unlock();
        }

        /// <summary>
        /// Queue a <see cref="GbxWriter"/> and expect to get a result from the callback
        /// </summary>
        /// <param name="gbx">The writer</param>
        /// <param name="callback">Callback to be called once the result is in</param>
        public void Queue(GbxWriter gbx, Action<GbxResponse> callback)
        {
            using (_createSynchronization.Synchronize())
            {
                _queuedMessages.Add(new QueuedMessage
                {
                    Action = callback,
                    Gbx = gbx
                });
            }
        }

        /// <summary>
        /// Queue a <see cref="GbxWriter"/> and expect to get a result from the callback
        /// </summary>
        /// <param name="gbx">The writer</param>
        /// <param name="callback">Callback to be called once the result is in</param>
        /// <returns>A task that can be used to await result</returns>
        public Task QueueAsync(GbxWriter gbx, Action<GbxResponse> callback)
        {
            var tcs = new TaskCompletionSource();
            using (_createSynchronization.Synchronize())
            {
                _queuedMessages.Add(new QueuedMessage
                {
                    Action = response =>
                    {
                        callback(response);
                        if (response.IsError)
                            tcs.SetException(new InvalidOperationException(response.Error.ToString()));

                        tcs.SetResult();
                    },
                    Gbx = gbx
                });
            }

            return tcs.Task;
        }

        /// <summary>
        /// Queue a <see cref="GbxWriter"/> and expect to get a result from the callback
        /// </summary>
        /// <param name="gbx">The writer</param>
        /// <param name="callback">Callback to be called once the result is in</param>
        /// <returns>A task that can be used to await result</returns>
        public Task<T> QueueAsync<T>(GbxWriter gbx, Func<GbxResponse, T> callback)
        {
            var tcs = new TaskCompletionSource<T>();
            using (_createSynchronization.Synchronize())
            {
                _queuedMessages.Add(new QueuedMessage
                {
                    Action = response =>
                    {
                        tcs.SetResult(callback(response));
                    },
                    Gbx = gbx
                });
            }

            return tcs.Task;
        }
    }
}