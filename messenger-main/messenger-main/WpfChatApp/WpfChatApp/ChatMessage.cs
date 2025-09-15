using System;
using System.Windows;
using System.Windows.Media;

namespace WpfChatApp
{
    /// <summary>
    /// Represents a single message in the chat. This is our "Model".
    /// </summary>
    public class ChatMessage
    {
        public string? SenderName { get; set; }
        public string SenderColor { get; set; } = "#000000";
        public string Content { get; set; } = string.Empty;
        public bool IsSentByMe { get; set; }
        public DateTime SentTime { get; set; }
        public HorizontalAlignment HorizontalAlignment => IsSentByMe ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        // --- Properties for Reply Feature ---
        public bool IsReply { get; set; }
        public string? RepliedToUsername { get; set; }
        public string? RepliedToContent { get; set; }

        // --- Properties for File Transfer Feature ---
        public bool IsFile { get; set; }
        public string? FileName { get; set; }
        public string? FileContentBase64 { get; set; }

        // --- Properties for UI state (e.g., image preview) ---
        public bool IsImagePreview { get; set; }
        public ImageSource? PreviewImageSource { get; set; }
    }
}
