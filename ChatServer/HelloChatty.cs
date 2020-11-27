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
        public IServerStreamWriter<BroadcastedMessage> MessagesWriter { get; set; }
    }

    public class HelloChatty : HelloChattyBase
    {
        private const int MessageQueueMaxLengh = 10;

        private string _messageOfTheDay;

        private enum UserStateFieldName
        {
            Name,
        }
        private Dictionary<string, ClientInfo> _clients;

        private Queue<BroadcastedMessage> _newcomerMessagesHistory;

        public HelloChatty(string messageOfTheDay)
        {
            _messageOfTheDay = messageOfTheDay;
            _clients = new Dictionary<string, ClientInfo>();
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

            if (_clients.ContainsKey(clientInfo.Name))
            {
                return new HelloReply
                {
                    Motd = "This name is already in use, please pick another!"
                };
            }

            context.UserState[UserStateFieldName.Name.ToString()] = clientInfo.Name;
            _clients.Add(clientInfo.Name, new ClientInfo());

            return new HelloReply
            {
                Motd = _messageOfTheDay,
                NamesInChat = _clients.Keys.Aggregate("", (acc, name) => $"{acc},{name}"),
            };
        }

        public override async Task<NewMessageSendingResult> NewMessage(NewMessageContent message, ServerCallContext context)
        {
            if (!context.UserState.ContainsKey(UserStateFieldName.Name))
            {
                return new NewMessageSendingResult
                {
                    Success = false,
                    Error = "You haven't been connected, please (re)try greeting the server"
                };
            }

            var senderName = context.UserState[UserStateFieldName.Name].ToString();

            var newMessage = new BroadcastedMessage
            {
                Sender = senderName,
                Text = message.Text,
                FiletimeUtc = DateTime.Now.ToFileTimeUtc(),
            };

            var success = false;
            string error = null;
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
            var username = context.UserState[UserStateFieldName.Name].ToString();
            var previousMessages = _newcomerMessagesHistory.ToList();
            previousMessages.ForEach(async m =>
            {
                await responseStream.WriteAsync(m);
            });
        }


        private void AddMessageToNewcomerQueue(BroadcastedMessage messsage)
        {
            _newcomerMessagesHistory.TrimExcess();
            _newcomerMessagesHistory.Enqueue(messsage);
        }
        private async Task BroadcastMessage(BroadcastedMessage message)
        {
            // todo: don't break on broken connections, just throw clients that don't accept messages from the room
            var broadcastingTasks = _clients.AsEnumerable()
                .Where(kvp => kvp.Key != message.Sender && kvp.Value.MessagesWriter != null)
                .Select(kvp => kvp.Value.MessagesWriter.WriteAsync(message)).ToList();

                await Task.WhenAll(broadcastingTasks);
        }
    }
}
