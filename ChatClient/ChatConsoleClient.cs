using HelloChattyProtocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ChatClient
{

    class ChatConsoleClient
    {
        private readonly IHelloChattyClient _client;

        public ChatConsoleClient(HelloChatty.HelloChattyClient client)
        {
            _client = new HelloChattyClient(client);
        }

        public async Task PerformSessionAsync()
        {
            try
            {
                await GreetServer();
                _client.OnNewMessage += OnNewMessage;
                _client.OnDisconnect += OnDisconnect;
                JoinChat();
                await AcceptMessagesFromUserLoop();
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}");
            }

            Console.WriteLine("\nEnd of session.");
        }

        private void OnNewMessage(object sender, BroadcastedMessage message)
        {
            var output = $"\n{message.Sender}: {message.Text}";
            Console.WriteLine(output);
        }
        private void OnDisconnect(object sender, string message)
        {
            Console.WriteLine($"DISCONNECTED: {message}");
        }

        private async Task GreetServer()
        {
            Console.WriteLine("Enter your name for the chat: ");
            var user = Console.ReadLine();

            Console.WriteLine("Greeting...");

            try
            {
                var reply = await _client.Greet(user);
                Console.WriteLine($"Greeting received: \n { reply.Motd }\n");
                Console.WriteLine($"Names in the chat: \n { reply.NamesInChat }\n");
            }
            catch (Exception e)
            {
                throw new Exception($"FAILED to greet! Error: {e.Message} \n {e.InnerException?.Message}");
            }
        }

        private void JoinChat()
        {
            _client.Join("default");
        }

        private async Task AcceptMessagesFromUserLoop()
        {
            Console.WriteLine("You can now send messages: type and press enter to send.");
            string text = string.Empty;
            while (!text.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                text = Console.ReadLine();
                try
                {
                    await _client.Send(text);
                }
                catch (Exception e)
                {
                    throw new Exception($"FAILED to send message! Error: {e.Message} \n {e.InnerException?.Message}");
                }
            }
        }
    }
}
