using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfChatApp
{
    public class MainWindowViewModel : BaseViewModel
    {
        #region Private Fields
        private TcpClient? _client;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private string? _username;
        private ChatMessage? _replyingToMessage;
        private const long MAX_FILE_SIZE = 2 * 1024 * 1024; // 2 MB
        #endregion

        #region Public Properties for Data Binding
        private string _nickname = "Guest";
        public string Nickname
        {
            get => _nickname;
            set { _nickname = value; OnPropertyChanged(); }
        }

        private string _serverIp = "127.0.0.1";
        public string ServerIp
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(); }
        }

        private string _port = "8888";
        public string Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); }
        }

        private string _currentMessage = string.Empty;
        public string CurrentMessage
        {
            get => _currentMessage;
            set { _currentMessage = value; OnPropertyChanged(); }
        }

        private Visibility _connectionPanelVisibility = Visibility.Visible;
        public Visibility ConnectionPanelVisibility
        {
            get => _connectionPanelVisibility;
            set { _connectionPanelVisibility = value; OnPropertyChanged(); }
        }

        private Visibility _replyPreviewVisibility = Visibility.Collapsed;
        public Visibility ReplyPreviewVisibility
        {
            get => _replyPreviewVisibility;
            set { _replyPreviewVisibility = value; OnPropertyChanged(); }
        }

        private string _replyPreviewUsername = string.Empty;
        public string ReplyPreviewUsername
        {
            get => _replyPreviewUsername;
            set { _replyPreviewUsername = value; OnPropertyChanged(); }
        }

        private string _replyPreviewContent = string.Empty;
        public string ReplyPreviewContent
        {
            get => _replyPreviewContent;
            set { _replyPreviewContent = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<string> OnlineUsers { get; } = new ObservableCollection<string>();
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ReplyCommand { get; }
        public ICommand CancelReplyCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand EmojiClickCommand { get; }
        public ICommand ExportChatCommand { get; }
        #endregion

        public MainWindowViewModel()
        {
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync());
            SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(CurrentMessage));
            AttachFileCommand = new RelayCommand(async _ => await AttachFileAsync());
            SaveFileCommand = new RelayCommand(async fileMessage => await SaveFileAsync(fileMessage as ChatMessage));
            ReplyCommand = new RelayCommand(message => ReplyToMessage(message as ChatMessage));
            CancelReplyCommand = new RelayCommand(_ => CancelReply());
            CopyCommand = new RelayCommand(message => CopyMessage(message as ChatMessage));
            EmojiClickCommand = new RelayCommand(emoji => AddEmoji(emoji as string));
            ExportChatCommand = new RelayCommand(_ => ExportChatHistory());
        }

        #region Command Methods
        private async Task ConnectAsync()
        {
            try
            {
                IPAddress ip = IPAddress.Parse(ServerIp);
                int port = int.Parse(Port);
                _username = Nickname;

                if (string.IsNullOrWhiteSpace(_username)) { MessageBox.Show("Vui lòng nhập tên người dùng."); return; }

                _client = new TcpClient { SendBufferSize = (int)(MAX_FILE_SIZE + 1024) };
                await _client.ConnectAsync(ip, port);

                var stream = _client.GetStream();
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                _reader = new StreamReader(stream, Encoding.UTF8);

                await _writer.WriteLineAsync(_username);
                ConnectionPanelVisibility = Visibility.Collapsed;
                _ = ListenForMessagesAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Lỗi kết nối đến server: {ex.Message}"); }
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentMessage) || _writer == null) return;

            try
            {
                string formattedMessage;
                if (_replyingToMessage != null)
                {
                    string repliedToUser = _replyingToMessage.SenderName ?? "không rõ";
                    string repliedToText = _replyingToMessage.IsFile ? $"Tệp: {_replyingToMessage.FileName}" : _replyingToMessage.Content;
                    formattedMessage = $"_reply_|{repliedToUser}|{repliedToText}|{CurrentMessage}";
                }
                else
                {
                    formattedMessage = CurrentMessage;
                }

                await SendSignalAsync(formattedMessage);
                CurrentMessage = string.Empty;
                CancelReply();
            }
            catch { AddSystemMessage("Không thể gửi tin nhắn."); }
        }

        private async Task AttachFileAsync()
        {
            if (_client == null || !_client.Connected) { MessageBox.Show("Vui lòng kết nối đến phòng chat trước."); return; }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                FileInfo fileInfo = new FileInfo(filePath);

                if (fileInfo.Length > MAX_FILE_SIZE) { MessageBox.Show($"File quá lớn. Kích thước tối đa là {MAX_FILE_SIZE / (1024 * 1024)} MB.", "Lỗi kích thước file", MessageBoxButton.OK, MessageBoxImage.Error); return; }

                try
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    string base64Content = Convert.ToBase64String(fileBytes);
                    await SendSignalAsync($"_file_|{fileInfo.Name}|{base64Content}");
                }
                catch (Exception ex) { MessageBox.Show($"Lỗi đọc file: {ex.Message}"); }
            }
        }

        private async Task SaveFileAsync(ChatMessage? fileMessage)
        {
            if (fileMessage == null || !fileMessage.IsFile) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog { FileName = fileMessage.FileName };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    byte[] fileBytes = Convert.FromBase64String(fileMessage.FileContentBase64 ?? "");
                    await File.WriteAllBytesAsync(saveFileDialog.FileName, fileBytes);
                    MessageBox.Show("Lưu file thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Lỗi lưu file: {ex.Message}"); }
            }
        }

        private void ReplyToMessage(ChatMessage? messageToReply)
        {
            if (messageToReply == null || messageToReply.SenderName == "System") return;

            _replyingToMessage = messageToReply;
            ReplyPreviewUsername = $"Đang trả lời {messageToReply.SenderName}";
            ReplyPreviewContent = messageToReply.IsFile ? $"Tệp: {messageToReply.FileName}" : messageToReply.Content;
            ReplyPreviewVisibility = Visibility.Visible;
        }

        private void CancelReply()
        {
            _replyingToMessage = null;
            ReplyPreviewVisibility = Visibility.Collapsed;
        }

        private void CopyMessage(ChatMessage? message)
        {
            if (message != null && !message.IsFile)
            {
                Clipboard.SetText(message.Content);
            }
        }

        private void AddEmoji(string? emoji)
        {
            if (string.IsNullOrEmpty(emoji)) return;
            CurrentMessage += emoji;
        }

        private void ExportChatHistory()
        {
            if (!Messages.Any())
            {
                MessageBox.Show("Không có tin nhắn nào để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = $"ChatHistory_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                Filter = "Text File (*.txt)|*.txt",
                Title = "Lưu lịch sử cuộc trò chuyện"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        writer.WriteLine($"--- Lịch sử cuộc trò chuyện lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss} ---");
                        foreach (var message in Messages)
                        {
                            string formattedLine;
                            if (message.SenderName == "System") formattedLine = $"[{message.SentTime:HH:mm}] *** {message.Content} ***";
                            else if (message.IsFile) formattedLine = $"[{message.SentTime:HH:mm}] {message.SenderName} đã gửi một tệp tin: {message.FileName}";
                            else if (message.IsReply) formattedLine = $"[{message.SentTime:HH:mm}] {message.SenderName} (trả lời {message.RepliedToUsername}): {message.Content}";
                            else formattedLine = $"[{message.SentTime:HH:mm}] {message.SenderName}: {message.Content}";
                            writer.WriteLine(formattedLine);
                        }
                    }
                    MessageBox.Show("Lịch sử chat đã được lưu thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Đã xảy ra lỗi khi lưu file: {ex.Message}"); }
            }
        }
        #endregion

        #region Private Helper Methods
        private async Task ListenForMessagesAsync()
        {
            try
            {
                if (_reader != null && _client != null)
                {
                    while (_client.Connected)
                    {
                        string? message = await _reader.ReadLineAsync();
                        if (message == null) break;

                        var parts = message.Split(new[] { '|' }, 2);
                        var messageType = parts[0];

                        switch (messageType)
                        {
                            case "_user_":
                                var userParts = message.Split('|');
                                if (userParts.Length == 4) AddMessage(userParts[3], userParts[1], userParts[2], userParts[1].Equals(_username, StringComparison.OrdinalIgnoreCase));
                                break;
                            case "_reply_":
                                var replyParts = message.Split('|');
                                if (replyParts.Length == 6) AddReplyMessage(replyParts[5], replyParts[1], replyParts[2], replyParts[3], replyParts[4], replyParts[1].Equals(_username, StringComparison.OrdinalIgnoreCase));
                                break;
                            case "_file_":
                                var fileParts = message.Split('|');
                                if (fileParts.Length == 5) AddFileMessage(fileParts[1], fileParts[2], fileParts[3], fileParts[4], fileParts[1].Equals(_username, StringComparison.OrdinalIgnoreCase));
                                break;
                            case "_system_":
                                AddSystemMessage(parts[1]);
                                break;
                            case "_userlist_":
                                if (parts.Length == 2) UpdateOnlineUsers(parts[1]);
                                break;
                        }
                    }
                }
            }
            catch (IOException) { }
            finally
            {
                if (_client != null)
                {
                    AddSystemMessage("Mất kết nối với máy chủ.");
                    DisconnectAndCleanup();
                }
            }
        }

        private async Task SendSignalAsync(string signal)
        {
            if (_writer != null) await _writer.WriteLineAsync(signal);
        }

        private void AddMessage(string content, string senderName, string senderColor, bool isSentByMe)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage { Content = content, SenderName = senderName, SenderColor = senderColor, IsSentByMe = isSentByMe, SentTime = DateTime.Now });
                if (!isSentByMe && senderName != "System") PlayNotificationSound();
            });
        }

        private void AddReplyMessage(string content, string senderName, string senderColor, string repliedToUsername, string repliedToContent, bool isSentByMe)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage { Content = content, SenderName = senderName, SenderColor = senderColor, IsSentByMe = isSentByMe, SentTime = DateTime.Now, IsReply = true, RepliedToUsername = repliedToUsername, RepliedToContent = repliedToContent });
                if (!isSentByMe) PlayNotificationSound();
            });
        }

        private void AddFileMessage(string senderName, string senderColor, string fileName, string fileContentBase64, bool isSentByMe)
        {
            var chatMessage = new ChatMessage { SenderName = senderName, SenderColor = senderColor, IsSentByMe = isSentByMe, SentTime = DateTime.Now, IsFile = true, FileName = fileName, FileContentBase64 = fileContentBase64 };
            if (IsImageFile(fileName))
            {
                try
                {
                    byte[] imageBytes = Convert.FromBase64String(fileContentBase64);
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = ms;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                        chatMessage.IsImagePreview = true;
                        chatMessage.PreviewImageSource = bitmapImage;
                    }
                }
                catch { chatMessage.IsImagePreview = false; }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(chatMessage);
                if (!isSentByMe) PlayNotificationSound();
            });
        }

        private void AddSystemMessage(string content)
        {
            AddMessage(content, "System", "#808080", false);
        }

        private void UpdateOnlineUsers(string userListPayload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnlineUsers.Clear();
                var users = userListPayload.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var user in users) { OnlineUsers.Add(user); }
            });
        }

        public void DisconnectAndCleanup()
        {
            try { _writer?.Close(); _reader?.Close(); _client?.Close(); }
            catch { }
            finally
            {
                _client = null; _writer = null; _reader = null;
                Application.Current.Dispatcher.Invoke(() => {
                    ConnectionPanelVisibility = Visibility.Visible;
                    OnlineUsers.Clear();
                    Messages.Clear();
                });
            }
        }

        private bool IsImageFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp";
        }

        private void PlayNotificationSound()
        {
            try
            {
                string relativePath = "Assets\\music.mp3";
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (File.Exists(fullPath))
                {
                    var player = new MediaPlayer(); player.Open(new Uri(fullPath)); player.Play();
                }
            }
            catch { }
        }
        #endregion
    }
}
