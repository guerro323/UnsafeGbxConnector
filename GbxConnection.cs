using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Utilities;

namespace UnsafeGbxConnector
{
    /// <summary>
    /// A connection to a (Track)(Shoot)(Quest)Mania game.
    /// </summary>
    /// <remarks>
    /// All calls will be batched into multicall.
    /// </remarks>
    public partial class GbxConnection : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly CancellationTokenSource _ccs;

        private readonly ArrayPool<Action<GbxResponse>?> _multiThreadTcsPool;
        private readonly Dictionary<uint, Action<GbxResponse>?[]> _outboundMultiCalls;

        private readonly List<QueuedMessage> _queuedMessages;

        private SynchronizationManager _createSynchronization;
        private SynchronizationManager _dictionarySynchronization;

        private byte[] _encodingBuffer;

        private uint _handle; // decrement, not increment

        private IPEndPoint _lastEp;

        private BinaryReader _reader;

        private Thread _receiveThread;
        private Thread _sendThread;
        private TcpClient _tcpClient;

        public GbxConnection()
        {
            _queuedMessages = new List<QueuedMessage>();

            _encodingBuffer = new byte[8192];
            _ccs = new CancellationTokenSource();
            _outboundMultiCalls = new Dictionary<uint, Action<GbxResponse>?[]>();

            _multiThreadTcsPool = ArrayPool<Action<GbxResponse>?>.Shared;
            
            _dictionarySynchronization = new SynchronizationManager();
            _createSynchronization = new SynchronizationManager();
        }

        /// <summary>
        ///     Whether or not the receiving part will use the default thread implementation
        /// </summary>
        public bool EnableReceiveThread { get; init; } = true;

        /// <summary>
        ///     Whether or not the sending part will use the default thread implementation
        /// </summary>
        public bool EnableSendThread { get; init; } = true;

        /// <summary>
        /// Dispose the current instance, this will terminate the connection
        /// </summary>
        public void Dispose()
        {
            if (Logger.IsTraceEnabled) Logger.Trace("Disposing Remote {endPoint}", _lastEp);

            _tcpClient.Dispose();

            _ccs.Cancel();
        }

        public event Action<GbxCallback> OnCallback;

        /// <summary>
        /// Initiate the connection to a server.
        /// </summary>
        /// <param name="ep">The endpoint</param>
        /// <exception cref="Exception">Handshake has failed</exception>
        public void Connect(IPEndPoint ep)
        {
            if (Logger.IsTraceEnabled) Logger.Trace("Connecting to {endPoint}", ep);

            _lastEp = ep;

            _tcpClient = new TcpClient();
            _tcpClient.Connect(ep.Address, ep.Port);

            var stream = _tcpClient.GetStream();
            _reader = new BinaryReader(stream);

            if (HandShake() == false)
                throw new Exception("HandShake has failed.");

            if (EnableReceiveThread)
            {
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.Start();
            }

            if (Logger.IsTraceEnabled) Logger.Trace("Custom thread for receiving: {boolean}", EnableReceiveThread);

            if (EnableSendThread)
            {
                _sendThread = new Thread(SendLoop);
                _sendThread.Start();
            }

            if (Logger.IsTraceEnabled) Logger.Trace("Custom thread for sending: {boolean}", EnableSendThread);

            if (Logger.IsTraceEnabled) Logger.Trace("Connected");
        }

        private bool HandShake()
        {
            var size = _reader.ReadUInt32();
            var handShakeBuffer = _reader.ReadBytes((int) size);

            return Encoding.UTF8.GetString(handShakeBuffer) == "GBXRemote 2";
        }
    }
}