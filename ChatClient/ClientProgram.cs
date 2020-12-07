
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using HelloChattyProtocol;

namespace ChatClient
{
    public delegate void OnNewMessageDelegate(BroadcastedMessage message);

    class ChatConsoleClient : IDisposable
    {
        private string _serverAddress;
        private HelloChatty.HelloChattyClient _rpcClient;
        private Channel _rpcChannel;
        private CancellationTokenSource _clientCancellation;

        public OnNewMessageDelegate OnNewMessage { get; set; }

        public ChatConsoleClient(string serverAdddress)
        {
            _serverAddress = serverAdddress;
        }

        public async Task PerformSessionAsync()
        {
            try
            {
                await Connect();
                await GreetChat();
                OnNewMessage += OnNewMessageHandler;
                SubscribeToMessages();
                await AcceptMessagesFromUserLoop();
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}");
            }

            await _rpcChannel?.ShutdownAsync();
            Console.WriteLine("End of session.");
        }

        private void OnNewMessageHandler(BroadcastedMessage message)
        {
            var output = $"\n{message.Sender}: {message.Text}";
            Console.WriteLine(output);
        }

        private async Task<bool> Connect()
        {
            Console.WriteLine("Connecting...");

            _rpcChannel = new Channel(_serverAddress, ChannelCredentials.Insecure);
            _rpcClient = new HelloChatty.HelloChattyClient(_rpcChannel);

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
                Console.WriteLine($"Greeting received: \n { reply.Motd }\n");
                Console.WriteLine($"Names in the chat: \n { reply.NamesInChat }\n");
            }
            catch (Exception e)
            {
                throw new Exception($"FAILED to greet! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        private void SubscribeToMessages()
        {
            try
            {
                var incomingMessagesStream = _rpcClient.SubscribeToMessages(new RequestedChatInfo
                {
                    ChatName = "default" // not expecting chats management for now
                });

                _clientCancellation = new CancellationTokenSource();
                ProcessIncomingMessagesUntilEndOfStream(incomingMessagesStream, _clientCancellation.Token);
            }
            catch (Exception e)
            {
                _clientCancellation.Cancel();
                throw new Exception($"FAILED to subscribe! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        private async Task AcceptMessagesFromUserLoop()
        {
            Console.WriteLine("You can now send messages: type and press enter to send.");
            while (!(_clientCancellation?.IsCancellationRequested ?? true))
            {
                var text = Console.ReadLine();
                try
                {
                    var sent = await _rpcClient.NewMessageAsync(
                        new NewMessageContent
                        {
                            Text = text,
                        }
                    );
                    if (!sent.Success)
                    {
                        throw new Exception(sent.Error);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"FAILED to send message! Error: {e.Message} \n {e.InnerException?.Message}");
                }
            }
        }

        private async Task ProcessIncomingMessagesUntilEndOfStream(AsyncServerStreamingCall<BroadcastedMessage> incomingMessagesStream, CancellationToken token)
        {
            var endReason = "Stream ended";
            while (await incomingMessagesStream.ResponseStream.MoveNext())
            {
                if (token.IsCancellationRequested)
                {
                    endReason = "Client-side end request";
                    break;
                }
                var newMessage = incomingMessagesStream.ResponseStream.Current;
                InvokeNewMessageHandler(newMessage);
            }

            if (!_clientCancellation.IsCancellationRequested)
            {
                _clientCancellation.Cancel();
            }

            InvokeNewMessageHandler(new BroadcastedMessage
            {
                Sender = null, // the sender is not a user
                Text = $"DISCONNECTED: {endReason}",
                FiletimeUtc = DateTime.Now.ToFileTimeUtc(),
            });
        }

        private void InvokeNewMessageHandler(BroadcastedMessage message)
        {
            try
            {
                OnNewMessage?.Invoke(message);
            }
            catch (Exception e)
            {
                throw new Exception($"Error thrown by OnNewMessage handler! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        public void Dispose()
        {
            if (_clientCancellation != null)
            {
                try
                {
                    _clientCancellation.Cancel();
                }
                catch (ObjectDisposedException e)
                {
                    Console.Error.WriteLine($"ERROR trying to unsubscribe from messages - cancellation source got disposed: {e.Message} \n {e.InnerException?.Message}");
                }
                catch (AggregateException e)
                {
                    Console.Error.WriteLine($"ERROR trying to unsubscribe from messages - AggregateException: {e.Message} \n {e.InnerException?.Message}");
                }
            }
        }
    }


    class Program
    {
        public static int Main(string[] args)
        {
            int result = 0;
            var client = new ChatConsoleClient("127.0.0.1:50051");

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

            Console.WriteLine("Press enter to exit...");
            Console.Read();
            return result;
        }
    }
}
