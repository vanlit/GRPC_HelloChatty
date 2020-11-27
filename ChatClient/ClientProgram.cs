
using System;
using System.Threading.Tasks;
using Grpc.Core;
using HelloChattyProtocol;

namespace ChatClient
{

    class ChatClient
    {
        string _serverAddress;
        HelloChatty.HelloChattyClient _rpcClient;
        Channel _rpcChannel;

        public ChatClient(string serverAdddress)
        {
            _serverAddress = serverAdddress;
        }

        public async Task PerformSessionAsync()
        {
            _rpcChannel = new Channel(_serverAddress, ChannelCredentials.Insecure);
            _rpcClient = new HelloChatty.HelloChattyClient(_rpcChannel);

            await Connect();
            await GreetChat();
            //await SubscribeToMessages();
            //await AcceptMessagesFromUserLoop();

            await _rpcChannel.ShutdownAsync();
        }

        private async Task<bool> Connect()
        {
            Console.WriteLine("Connecting...");

            try
            {
                await _rpcChannel.ConnectAsync();
            }
            catch (Exception e)
            {
                throw new Exception($"FAILED to connect! Error: {e.Message} \n {e.InnerException?.Message}");
            }

            if (_rpcChannel.State == ChannelState.Ready)
            {
                Console.WriteLine("Connected!" + Environment.NewLine);
                return true;
            }
            return false;
        }

        private async Task GreetChat()
        {
            Console.WriteLine("Enter your name for the chat: ");
            var user = Console.ReadLine();

            Console.WriteLine("Greeting...");

            try
            {
                var reply = await _rpcClient.SayHelloAsync(new HelloRequest { Name = user });
                Console.WriteLine($"Greeting received: \n + { reply.Motd }");
                Console.WriteLine($"Names in the chat: \n + { reply.NamesInChat }");
            }
            catch (Exception e)
            {
                throw new Exception($"FAILED to greet! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }
    }


    class Program
    {
        public static int Main(string[] args)
        {
            int result = 0;
            var client = new ChatClient("127.0.0.1:50051");

            try
            {
                client.PerformSessionAsync().Wait();
            }
            catch (Exception e)
            {
                result = 1;
                Console.Error.WriteLine(e.Message + Environment.NewLine + e.InnerException?.Message);
                Console.WriteLine("An error occured during the session, see output above.");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return result;
        }
    }
}
