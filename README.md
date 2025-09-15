#Chat-App-WPF
Mô tả: Xây dựng một ứng dụng chat nhóm thời gian thực trên nền tảng desktop, sử dụng mô hình Client-Server để cho phép nhiều người dùng kết nối và giao tiếp qua mạng.


Kiến trúc & Công nghệ:


Giao diện người dùng (UI): Được xây dựng bằng WPF (Windows Presentation Foundation), đảm bảo giao diện hiện đại và có khả năng cập nhật tự động.



Kiến trúc: Tuân thủ nghiêm ngặt theo mô hình MVVM (Model - View - ViewModel) để tách biệt logic và giao diện, giúp mã nguồn dễ bảo trì và mở rộng.



Mạng: Sử dụng TCP Socket để thiết lập kết nối ổn định và đáng tin cậy giữa Server và các Client.



Xử lý bất đồng bộ: Toàn bộ tác vụ mạng được xử lý bằng async/await để UI luôn mượt mà và không bị "đóng băng".

Tính năng nổi bật:

Gửi tin nhắn văn bản và 

tệp tin (ảnh, tài liệu).


Trả lời (Reply) một tin nhắn cụ thể.

Hiển thị danh sách người dùng đang 

online.


Xuất lịch sử cuộc trò chuyện ra file .txt.

Công nghệ sử dụng: C#, .NET Framework, WPF, TCP/IP Sockets.
