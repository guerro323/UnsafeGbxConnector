using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            var instance = new GbxConnection();
            
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
                Thread.Sleep(10);
            }
            
            instance.Dispose();
        }

        private static async Task DoAsync(GbxConnection instance)
        {
            instance.Connect(IPEndPoint.Parse("127.0.0.1:5000"));
            instance.OnCallback += msg =>
            {
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
            
            Console.WriteLine($"{versionPacket.Name}");

            writer = new GbxWriter("GetMapList");
            writer.WriteInt(4);
            writer.WriteInt(0);

            await instance.QueueAsync(writer, message => { Console.WriteLine("ye"); });

            instance.Queue(new ChatSendServerMessagePacket {Text = "Hello"});

            writer = new GbxWriter("SendChatSerporpropr");
            writer.WriteString("hello");

            await instance.QueueAsync(writer, gbx =>
            {
                if (gbx.IsError)
                {
                    
                }
                Console.WriteLine($"{gbx.Error}");
            });

            writer = new GbxWriter("WriteFile");
            writer.WriteString("file.txt");
            writer.WriteBase64("j'aime le fromage");
            
            await instance.QueueAsync(writer, gbx =>
            {
                Console.WriteLine(gbx.Error);
            });
            
            for (var i = 0; i < 1000; i++)
            {
                var sw = new Stopwatch();
                sw.Start();
                for (var x = 0; x < 256; x++)
                {
                    var w = new GbxWriter("ChatSendServerMessage");
                    w.WriteString("Hello World!");

                    instance.Queue(w);
                }
                sw.Stop();

                await Task.Delay(100);

                Console.WriteLine(sw.Elapsed.TotalMilliseconds + "ms");
            }
        }
    }
}