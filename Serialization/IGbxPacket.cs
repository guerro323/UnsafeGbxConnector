using UnsafeGbxConnector.Serialization.Readers;
using UnsafeGbxConnector.Serialization.Writers;

namespace UnsafeGbxConnector.Serialization
{
    public interface IGbxPacket
    {
        string GetMethodName();

        void Write(in GbxWriter writer);
        void Read(in GbxReader reader);
    }
}