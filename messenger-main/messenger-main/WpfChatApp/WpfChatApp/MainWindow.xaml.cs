using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace WpfChatApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Lắng nghe sự kiện thay đổi của danh sách tin nhắn để tự động cuộn
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.Messages.CollectionChanged += (s, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        // Đảm bảo cuộn xuống tin nhắn mới nhất
                        MessagesScrollViewer.ScrollToEnd();
                    }
                };
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Gọi hàm dọn dẹp trong ViewModel khi đóng cửa sổ
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.DisconnectAndCleanup();
            }
        }
    }
}
