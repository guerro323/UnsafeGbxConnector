# UnsafeGbxConnector

A not so unsafe gbx remote!

## Goals
- Performance
  - Low memory usage
  - Batches calls automatically
  - Sending and receiving doesn't create GC garbage.
  - Threading support.
  - No reflection (**But a source generator will be added!**)
- Safety
  - The remote automatically make sure to not send too much requests and multi-calls to not crash the server.
  - Thread safe.

## Usage
A source generator will be added to simplify this:
```csharp
var instance = new GbxConnection();

instance.Connect(IPEndPoint.Parse("127.0.0.1:5000"));
instance.OnCallback += msg =>
{
    // Print the method call
    Console.WriteLine($"{msg.ToString()}");

    var reader = msg.Reader;
    if (msg.Match("ManiaPlanet.PlayerChat"))
    {
        var playerId = reader[0].ReadInt();
        var login = reader[1].ReadString();
        var text = reader[2].ReadString();

        Console.WriteLine($"{playerId} {login}: {text}");
    }
};

// You can queue packets from a struct or...
instance.Queue(new AuthenticatePacket {Login = "SuperAdmin", Password = "SuperAdmin"});

// ... create it from a GbxWriter
var writer = new GbxWriter("EnableCallbacks");
writer.WriteBool(true);
instance.Queue(writer); //< don't use the same writer instance after calling .Queue()

writer = new GbxWriter("SetApiVersion");
writer.WriteString("2013-04-16");

instance.Queue(writer);

var getVersionOption = await instance.QueueAsync(new GetVersionPacket());
if (!getVersionOption.TryGetResult(out var versionPacket, out var getVersionError))
    Console.WriteLine(getVersionError.ToString());

Console.WriteLine($"{versionPacket.Name}");

struct AuthenticatePacket : IGbxPacket
{
    public string Login, Password;

    public string GetMethodName()
    {
        return "Authenticate";
    }

    public void Read(in GbxReader reader)
    {
    }

    public void Write(in GbxWriter writer)
    {
        writer.WriteString(Login);
        writer.WriteString(Password);
    }
}

struct GetVersionPacket : IGbxPacket
{
    public string GetMethodName()
    {
        return "GetVersion";
    }

    public void Write(in GbxWriter writer)
    {
    }

    public string Name;

    public void Read(in GbxReader reader)
    {
        var gbxStruct = reader[0].ReadStruct();
        Name = gbxStruct["Name"].ReadString();
    }
}
```