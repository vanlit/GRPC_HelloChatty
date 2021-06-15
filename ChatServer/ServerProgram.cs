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
                    // docs about keeping grpc connection alive: https://github.com/grpc/grpc/blob/master/doc/keepalive.md
                    // the mappings of the fields are in file grpc/include/grpc/impl/codegen/grpc_types.h in the official grpc core repo https://github.com/grpc/grpc
                    new ChannelOption ( "grpc.keepalive_time_ms", 5000 ), // keepalive ping every X ms
                    new ChannelOption ( "grpc.keepalive_timeout_ms", 10 * 1000 ), // peer must reply to our ping within this
                    new ChannelOption ( "grpc.keepalive_permit_without_calls", 1 ), // allow keepalive without calls at all
                    new ChannelOption ( "grpc.http2.max_pings_without_data", 0 ), // allow the client to keep connection alive without activity for N pings from client
                    new ChannelOption ( "grpc.http2.min_ping_interval_without_data_ms", 1000),
                    new ChannelOption ( "grpc.http2.max_ping_strikes", 1),
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
