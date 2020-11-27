using Grpc.Core;
using HelloChattyProtocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static HelloChattyProtocol.HelloChatty;

namespace ChatServer
{
    public class HelloChatty : HelloChattyBase
    {
        private string _messageOfTheDay;

        private enum UserStateFieldName {
            Name,
        }
        private Dictionary<string, dynamic> _clients;

        private Queue<string> messagesHistory;

        public HelloChatty(string messageOfTheDay)
        {
            _messageOfTheDay = messageOfTheDay;
            _clients = new Dictionary<string, dynamic>();
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
            return new HelloReply
            {
                Motd = 
            }
        }

        public override async Task<NewMessageSendingResult> NewMessage(NewMessageContent message, ServerCallContext context)
        {
            if (! context.UserState.ContainsKey(UserStateFieldName.Name))
            {
                return new NewMessageSendingResult
                {
                    Success = false,
                    Error = "You haven't been connected, please (re)try greeting the server"
                };
            }

            var name = context.UserState[UserStateFieldName.Name];
        }

        // not really maintaining more than one chat for now
        public override async Task SubscribeToMessages(RequestedChatInfo requestedChat, IServerStreamWriter<BroadcastedMessage> responseStream, ServerCallContext context)
        {
            
            throw new NotImplementedException();
        }
    }
}
