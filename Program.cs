using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnsafeGbxConnector.Serialization;
using UnsafeGbxConnector.Serialization.Readers;
using UnsafeGbxConnector.Serialization.Writers;

namespace UnsafeGbxConnector
{
    internal struct AuthenticatePacket : IGbxPacket
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

    internal struct ChatSendServerMessagePacket : IGbxPacket
    {
        public string GetMethodName()
        {
            return "ChatSendServerMessage";
        }

        public string Text;

        public void Write(in GbxWriter writer)
        {
            writer.WriteString(Text);
        }

        public void Read(in GbxReader reader)
        {
        }
    }

    internal struct GetVersionPacket : IGbxPacket
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

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            {
                Console.WriteLine($"{eventArgs.Exception}");
            };

            LoggingConfiguration config = new();
            
            ConsoleTarget logConsole = new("logconsole");

            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);

            LogManager.Configuration = config;
            
            var instance = new GbxConnection();
            instance.OnCallback += msg =>
            {
                Console.WriteLine($"Message: {msg.ToString()}");

                var reader = msg.Reader;
                if (msg.Match("ManiaPlanet.PlayerChat"))
                {
                    var playerId = reader[0].ReadInt();
                    var login = reader[1].ReadString();
                    var text = reader[2].ReadString();

                    //Console.WriteLine($"{playerId} {login}: {text}");
                }
            };
            
            try
            {
                await DoAsync(instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            while (true)
            {
                Thread.Sleep(1000);
                //instance.Queue(new ChatSendServerMessagePacket {Text = "Hello"});
            }
        }

        private static async Task DoAsync(GbxConnection instance)
        {
            instance.Connect(IPEndPoint.Parse("127.0.0.1:5000"));
            instance.Queue(new AuthenticatePacket {Login = "SuperAdmin", Password = "SuperAdmin"});

            var writer = new GbxWriter("EnableCallbacks");
            writer.WriteBool(true);
            instance.Queue(writer);

            writer = new GbxWriter("SetApiVersion");
            writer.WriteString("2013-04-16");

            instance.Queue(writer);

            var getVersionOption = await instance.QueueAsync(new GetVersionPacket());
            if (!getVersionOption.TryGetResult(out var versionPacket, out var getVersionError))
                Console.WriteLine(getVersionError.ToString());
            
            writer = new GbxWriter("GetMapList");
            writer.WriteInt(4);
            writer.WriteInt(0);

            await instance.QueueAsync(writer, message => { Console.WriteLine("ye"); });

            instance.Queue(new ChatSendServerMessagePacket {Text = "Hello"});

            writer = new GbxWriter("WriteFile");
            writer.WriteString("file.txt");
            writer.WriteBase64("j'aime le fromage");
            
            await instance.QueueAsync(writer, gbx =>
            {
                Console.WriteLine(gbx.Error);
            });

            writer = new GbxWriter("TriggerModeScriptEventArray");
            writer.WriteString("XmlRpc.EnableCallbacks");
            using (var array = writer.BeginArray())
            {
                array.AddString("1");
            }
            await instance.QueueAsync(writer, c =>
            {
                Console.WriteLine("err? " + c.Error);
            });

            writer = new GbxWriter("GetPlayerList");
            writer.WriteInt(-1);
            writer.WriteInt(0);
            writer.WriteInt(0);
            await instance.QueueAsync(writer, gbx =>
            {
                var array = gbx.Reader[0].ReadArray();
                for (var i = 0; array.TryReadAt(out var element, i); i++)
                {
                    Console.WriteLine(element.AsStruct()["Login"].ReadString());
                }
                return 0;
            });
        }
    }
}