using Grpc.Core;
using HelloChattyProtocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient
{
    public interface IHelloChattyClient : IDisposable
    {
        Task<bool> Connect();
        Task<HelloReply> Greet(string userName);
        void Join(string chatName);
        Task Send(string text);
        EventHandler<BroadcastedMessage> OnNewMessage { get; set; }
        EventHandler<string> OnDisconnect { get; set; }
    }


    public delegate void OnNewMessageDelegate(BroadcastedMessage message);

    class HelloChattyClient : IHelloChattyClient
    {
        private string _serverAddress;

        private Channel _rpcChannel;
        private HelloChatty.HelloChattyClient _rpcClient;

        public EventHandler<BroadcastedMessage> OnNewMessage { get; set; }
        public EventHandler<string> OnDisconnect { get; set; }

        private CancellationTokenSource _subscriptionCancellation;

        public HelloChattyClient(string serverAddress)
        {
            _serverAddress = serverAddress;
        }

        public async Task<bool> Connect()
        {
            _rpcChannel = new Channel(_serverAddress, ChannelCredentials.Insecure,
                new List<ChannelOption> {
                    // https://github.com/grpc/grpc/blob/master/doc/keepalive.md
                    new ChannelOption ( "grpc.keepalive_time_ms", 1000 ), // keepalive ping every X ms
                    new ChannelOption ( "grpc.keepalive_timeout_ms", 10 * 1000 ), // peer must reply to our ping within this
                    new ChannelOption ( "grpc.keepalive_permit_without_calls", 1 ), // allow keepalive without calls at all
                    // new ChannelOption ( "grpc.http2.max_pings_without_data", 0 ), // server-only setting
                    // new ChannelOption ( "grpc.http2.min_ping_interval_without_data_ms", 1000), // server-only setting
                    new ChannelOption ( "grpc.http2.max_ping_strikes", 1),
                }
            );
            _rpcClient = new HelloChatty.HelloChattyClient(_rpcChannel);

            await _rpcChannel.ConnectAsync();

            return _rpcChannel.State == ChannelState.Ready;
        }

        public async Task<HelloReply> Greet(string userName)
        {
            return await _rpcClient.SayHelloAsync(new HelloRequest { Name = userName });
        }

        public void Join(string chatName)
        {
            try
            {
                var incomingMessagesStream = _rpcClient.SubscribeToMessages(new RequestedChatInfo
                {
                    ChatName = chatName // not expecting chats management for now
                });
                _subscriptionCancellation = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                ProcessIncomingMessagesUntilEndOfStream(incomingMessagesStream, _subscriptionCancellation.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            catch (Exception e)
            {
                _subscriptionCancellation.Cancel();
                throw new Exception($"FAILED to subscribe! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        public async Task Send(string text)
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

        public void Dispose()
        {
            try
            {
                _rpcChannel?.ShutdownAsync();
                _subscriptionCancellation?.Cancel();
                OnNewMessage = null;
                OnDisconnect = null;
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

            InvokeDisconnectHandler(endReason);
        }

        private void InvokeNewMessageHandler(BroadcastedMessage message)
        {
            try
            {
                OnNewMessage?.Invoke(this, message);
            }
            catch (Exception e)
            {
                throw new Exception($"Error thrown by OnNewMessage handler! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        private void InvokeDisconnectHandler(string message)
        {
            try
            {
                OnDisconnect?.Invoke(this, message);
            }
            catch (Exception e)
            {
                throw new Exception($"Error thrown by OnDisconnect handler! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }
    }
}
