using Grpc.Core;
using System;
using System.Collections.Generic;

namespace ChatServer
{
    class Program
    {
        const int Port = 50051;
        const string MOTD = "Welcome to Hello Chatty server!";

        static void Main(string[] args)
        {
            Server server = new Server (
                new List<ChannelOption> {
                    // https://github.com/grpc/grpc/blob/master/doc/keepalive.md
                    new ChannelOption ( "grpc.GRPC_ARG_KEEPALIVE_TIME_MS", 20*1000 ), // keepalive ping every X ms
                    new ChannelOption ( "grpc.GRPC_ARG_HTTP2_MAX_PINGS_WITHOUT_DATA", 0 ), // max amount of subsequent keepalive pings without data transfer. 0 - inf
                    new ChannelOption ( "grpc.keepalive_permit_without_calls", 1 ) // allow keepalive without calls at all
                }
            )
            {
                Services = { HelloChattyProtocol.HelloChatty.BindService(new HelloChattyImpl(MOTD)) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine($"{nameof(HelloChattyImpl)} server is listening on port {Port}");
            Console.WriteLine("Press enter to stop the server...");
            Console.Read();

            server.ShutdownAsync().Wait();
        }
    }
}
