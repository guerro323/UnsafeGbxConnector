using UnsafeGbxConnector.Serialization.Readers;

namespace UnsafeGbxConnector.Serialization
{
    public readonly struct GbxResponseOption<TPacket>
        where TPacket : struct, IGbxPacket
    {
        public readonly TPacket? Result;
        public readonly GbxError? Error;

        public bool HasResult => Result.HasValue;
        public bool IsError => Error.HasValue;

        public GbxResponseOption(TPacket packet)
        {
            Result = packet;
            Error = null;
        }

        public GbxResponseOption(GbxError error)
        {
            Result = null;
            Error = error;
        }

        public bool TryGetResult(out TPacket packet)
        {
            packet = Result.GetValueOrDefault();
            return Result.HasValue;
        }

        public bool TryGetResult(out TPacket packet, out GbxError error)
        {
            packet = Result.GetValueOrDefault();
            error = Error.GetValueOrDefault();
            return Result.HasValue;
        }
    }

    public readonly struct GbxResponse
    {
        public readonly GbxReader Reader;

        public readonly GbxError Error;

        public bool IsError => Error.Code > 0;

        private GbxResponse(GbxReader reader, GbxError error)
        {
            Reader = reader;
            Error = error;
        }

        public GbxResponse(GbxReader reader) : this(reader, GbxError.None)
        {
        }
        
        public GbxResponse(GbxError error) : this(default, error)
        {
        }
    }
}