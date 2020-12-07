using Grpc.Core;
using HelloChattyProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HelloChattyProtocol.HelloChatty;

namespace ChatServer
{
    class ClientInfo
    {
        public string Name { get; set; }
        public IServerStreamWriter<BroadcastedMessage> MessagesWriter { get; set; }
        public bool Dead { get; set; } = false;
    }

    public class HelloChatty : HelloChattyBase
    {
        private const int MessageQueueMaxLengh = 10;

        private string _messageOfTheDay;

        private enum UserStateFieldName
        {
            Name,
        }
        private Dictionary<string, ClientInfo> _clientsByPort;

        private Queue<BroadcastedMessage> _newcomerMessagesHistory;

        public HelloChatty(string messageOfTheDay)
        {
            _messageOfTheDay = messageOfTheDay;
            _clientsByPort = new Dictionary<string, ClientInfo>();
            _newcomerMessagesHistory = new Queue<BroadcastedMessage>(MessageQueueMaxLengh);
        }

        public override async Task<HelloReply> SayHello(HelloRequest clientInfo, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(clientInfo.Name))
            {
                return new HelloReply
                {
                    Motd = "Your name can't be empty!"
                };
            }

            if (_clientsByPort.Values.Any(ci => ci.Name.Equals(clientInfo.Name)))
            {
                var msg = "This name is already in use, please pick another!";
                return new HelloReply
                {
                    Motd = msg,
                };
            }

            // context.UserState[UserStateFieldName.Name.ToString()] = clientInfo.Name; // there should be a way to store some session info, but this doesn't seem to be one
            var port = GetPeerPort(context);
            _clientsByPort.Add(port, new ClientInfo() { Name = clientInfo.Name });

            return new HelloReply
            {
                Motd = _messageOfTheDay,
                NamesInChat = string.Join(',', _clientsByPort.Values.Select(v => v.Name)),
            };
        }

        public override async Task<NewMessageSendingResult> NewMessage(NewMessageContent message, ServerCallContext context)
        {
            var port = GetPeerPort(context);
            if (!_clientsByPort.ContainsKey(port))
            {
                return new NewMessageSendingResult
                {
                    Success = false,
                    Error = "You haven't been connected, please (re)try greeting the server"
                };
            }

            var userInfo = _clientsByPort[port];
            var senderName = userInfo.Name;

            var newMessage = new BroadcastedMessage
            {
                Sender = senderName,
                Text = message.Text,
                FiletimeUtc = DateTime.Now.ToFileTimeUtc(),
            };

            var success = false;
            string error = "";
            try
            {
                await BroadcastMessage(newMessage);
                success = true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"FAILED to broadcast! Error: {e.Message} \n {e.InnerException?.Message}");
                error = "Internal error while broadcasting the message";
            }

            if (success) AddMessageToNewcomerQueue(newMessage);

            return new NewMessageSendingResult
            {
                Success = success,
                Error = error,
            };
        }

        // not really maintaining more than one chat for now
        public override async Task SubscribeToMessages(RequestedChatInfo requestedChat, IServerStreamWriter<BroadcastedMessage> responseStream, ServerCallContext context)
        {
            var previousMessages = _newcomerMessagesHistory.ToList();
            foreach (var prevMsg in previousMessages)
            {
                await responseStream.WriteAsync(prevMsg);
            }
            var userInfo = _clientsByPort[GetPeerPort(context)];
            userInfo.MessagesWriter = responseStream;
        }

        private static string GetPeerPort(ServerCallContext context)
        {
            return context.Peer.Split(':')[2];
        }
        private void AddMessageToNewcomerQueue(BroadcastedMessage messsage)
        {
            _newcomerMessagesHistory.TrimExcess();
            _newcomerMessagesHistory.Enqueue(messsage);
        }
        private async Task BroadcastMessage(BroadcastedMessage message)
        {
            var clientsToBroadcastTo = _clientsByPort.Values
                .Where(v => v.Name != message.Sender && v.MessagesWriter != null && !v.Dead)
                .ToList();

            foreach (var client in clientsToBroadcastTo)
            {
                try
                { // todo optimize by moving into a wrapping task to send them all simultaneously
                    await client.MessagesWriter.WriteAsync(message);
                }
                catch (Exception e)
                {
                    client.Dead = true; // todo might want to save some info about the death
                }
            }

            RemoveDeadClients();
        }

        private void RemoveDeadClients()
        {
            var newClientsByPort = new Dictionary<string, ClientInfo>(
                _clientsByPort.Where(kvp => !kvp.Value.Dead)
            );
            _clientsByPort = newClientsByPort;
        }
    }
}
