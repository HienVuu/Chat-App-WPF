using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;


namespace ChatServer
{
    public class ClientInfo
    {
        public string Username { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
        public required StreamWriter Writer { get; set; }
    }

    class Server
    {
        private static readonly Dictionary<TcpClient, ClientInfo> connectedClients = new Dictionary<TcpClient, ClientInfo>();
        private static readonly object _lock = new object();
        private static readonly List<string> userColors = new List<string> { "#D32F2F", "#1976D2", "#388E3C", "#F57C00", "#7B1FA2", "#0288D1" };
        private static int colorIndex = 0;

        static async Task Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);
            server.Start();
            Console.WriteLine("Chat Server (Final Version) started on port 8888.");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            client.ReceiveBufferSize = 2 * 1024 * 1024 + 1024; // 2MB+ buffer
            string username = "unknown";
            ClientInfo? clientInfo = null;

            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                username = await reader.ReadLineAsync() ?? "Guest";
                clientInfo = new ClientInfo { Username = username, ColorHex = GetNextColor(), Writer = writer };

                lock (_lock) { connectedClients.Add(client, clientInfo); }
                Console.WriteLine($"[+] {clientInfo.Username} connected. Total clients: {connectedClients.Count}");

                // Gửi thông báo và cập nhật danh sách người dùng cho tất cả mọi người
                await BroadcastMessageAsync($"--- {clientInfo.Username} has joined the chat. ---", null);
                await BroadcastUserListAsync();

                string? message;
                while ((message = await reader.ReadLineAsync()) != null)
                {
                    // Chuyển tiếp tin nhắn đến tất cả client
                    await BroadcastMessageAsync(message, client);
                }
            }
            catch (Exception ex)
            {
                // Chỉ ghi log lỗi nếu không phải là lỗi do client chủ động ngắt kết nối
                if (!(ex is IOException || ex is ObjectDisposedException))
                {
                    Console.WriteLine($"[!] Error handling client '{username}': {ex.Message}");
                }
            }
            finally
            {
                // Logic dọn dẹp khi client ngắt kết nối
                bool removed = false;
                lock (_lock)
                {
                    if (connectedClients.TryGetValue(client, out clientInfo))
                    {
                        removed = connectedClients.Remove(client);
                    }
                }
                if (removed && clientInfo != null)
                {
                    Console.WriteLine($"[-] {clientInfo.Username} disconnected. Total clients: {connectedClients.Count}");
                    // Gửi thông báo và cập nhật lại danh sách người dùng cho những người còn lại
                    await BroadcastMessageAsync($"--- {clientInfo.Username} has left the chat. ---", null);
                    await BroadcastUserListAsync();
                }
                client.Close();
            }
        }

        // Gửi danh sách người dùng online đến tất cả mọi người
        static async Task BroadcastUserListAsync()
        {
            List<string> usernames;
            lock (_lock)
            {
                usernames = connectedClients.Values.Select(c => c.Username).OrderBy(name => name).ToList();
            }

            string userListPayload = string.Join(",", usernames);
            // Định dạng: _userlist_|user1,user2,user3
            string formattedMessage = $"_userlist_|{userListPayload}";

            await BroadcastSignalAsync(formattedMessage);
        }

        // Gửi một tin nhắn (chat, file, reply) hoặc thông báo hệ thống
        static async Task BroadcastMessageAsync(string message, TcpClient? sender)
        {
            string formattedMessage;
            ClientInfo? senderInfo = null;
            if (sender != null)
            {
                lock (_lock) { connectedClients.TryGetValue(sender, out senderInfo); }
            }

            var messageType = message.Split('|')[0];

            if (sender == null) // Tin nhắn hệ thống
            {
                formattedMessage = $"_system_|{message}";
            }
            else if (messageType == "_reply_")
            {
                var parts = message.Split(new[] { '|' }, 4);
                formattedMessage = $"_reply_|{senderInfo?.Username}|{senderInfo?.ColorHex}|{parts[1]}|{parts[2]}|{parts[3]}";
            }
            else if (messageType == "_file_")
            {
                var parts = message.Split(new[] { '|' }, 3);
                formattedMessage = $"_file_|{senderInfo?.Username}|{senderInfo?.ColorHex}|{parts[1]}|{parts[2]}";
            }
            else // Tin nhắn thường
            {
                formattedMessage = $"_user_|{senderInfo?.Username}|{senderInfo?.ColorHex}|{message}";
            }

            await BroadcastSignalAsync(formattedMessage);
        }

        // Hàm chung để gửi bất kỳ tín hiệu nào đến tất cả client
        static async Task BroadcastSignalAsync(string signal)
        {
            List<ClientInfo> clientsCopy;
            lock (_lock) { clientsCopy = connectedClients.Values.ToList(); }

            var sendTasks = clientsCopy.Select(client =>
            {
                try
                {
                    return client.Writer.WriteLineAsync(signal);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Failed to send signal to {client.Username}: {ex.Message}");
                    return Task.CompletedTask;
                }
            });
            await Task.WhenAll(sendTasks);
        }

        static string GetNextColor()
        {
            lock (userColors)
            {
                string color = userColors[colorIndex];
                colorIndex = (colorIndex + 1) % userColors.Count;
                return color;
            }
        }
    }
}
