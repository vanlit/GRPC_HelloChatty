
using Grpc.Core;
using HelloChattyProtocol;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatClient
{
    class Program
    {
        private const string ServerAddress = "127.0.0.1:3000";

        const string client_private_key_file = "client_key.pem";
        const string client_certificate_file = "client_cert.crt";
        const string server_certificate_file = "server_cert.crt";

        private static async Task<HelloChatty.HelloChattyClient> ConnectProtoClient(string serverAddress)
        {
            var key = System.IO.File.ReadAllText(client_private_key_file);
            var cert = System.IO.File.ReadAllText(client_certificate_file);
            var serverCert = System.IO.File.ReadAllText(server_certificate_file);

            var creds = new SslCredentials(
                serverCert, new KeyCertificatePair(key, cert)
            );

            var rpcChannel = new Channel(serverAddress, creds,
                new List<ChannelOption> {
                    // docs about keeping grpc connection alive: https://github.com/grpc/grpc/blob/master/doc/keepalive.md
                    // the mappings of the fields are in file grpc/include/grpc/impl/codegen/grpc_types.h in the official grpc core repo https://github.com/grpc/grpc
                    new ChannelOption ( "grpc.keepalive_time_ms", 1000 ), // keepalive ping every X ms
                    new ChannelOption ( "grpc.keepalive_timeout_ms", 10 * 1000 ), // peer must reply to our ping within this
                    new ChannelOption ( "grpc.keepalive_permit_without_calls", 1 ), // allow keepalive without calls at all
                    // new ChannelOption ( "grpc.http2.max_pings_without_data", 0 ), // server-only setting
                    // new ChannelOption ( "grpc.http2.min_ping_interval_without_data_ms", 1000), // server-only setting
                    new ChannelOption ( "grpc.http2.max_ping_strikes", 1),
                }
            );
            var rpcClient = new HelloChatty.HelloChattyClient(rpcChannel);

            await rpcChannel.ConnectAsync();

            if (rpcChannel.State == ChannelState.Ready)
            {
                return rpcClient;
            }
            throw new Exception($@"For unknown reason, the channel state is still {rpcChannel.State}");
        }

        public static async Task<int> Main(string[] args)
        {
            int result = 0;
            var rpcClient = await ConnectProtoClient(ServerAddress);
            var cliClient = new ChatConsoleClient(rpcClient);
            try
            {
                cliClient.PerformSessionAsync().Wait();
            }
            catch (Exception e)
            {
                result = 1;
                Console.Error.WriteLine(e.Message + Environment.NewLine + e.InnerException?.Message);
                Console.WriteLine("\nAn error occured during the session, see output above.");
            }

            Console.WriteLine("Press enter to exit...");
            Console.Read();
            return result;
        }
    }
}
