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
        public override Task<NewMessageSendingResult> NewMessage(NewMessageContent message, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<HelloReply> SayHello(HelloRequest clientInfo, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task SubscribeToMessages(RequestedChatInfo requestedChat, IServerStreamWriter<BroadcastedMessage> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }
    }
}
