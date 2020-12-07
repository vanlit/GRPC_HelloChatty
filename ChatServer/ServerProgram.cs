using System;
using Grpc.Core;

namespace ChatServer
{
    class Program
    {
        const int Port = 50051;
        const string MOTD = "Welcome to Hello Chatty server!";

        static void Main(string[] args)
        {
            Server server = new Server
            {
                Services = { HelloChattyProtocol.HelloChatty.BindService(new HelloChatty(MOTD)) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine($"{nameof(HelloChatty)} server is listening on port {Port}");
            Console.WriteLine("Press enter to stop the server...");
            Console.Read();

            server.ShutdownAsync().Wait();
        }
    }
}
