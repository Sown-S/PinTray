# Thêm hỗ trợ cho một thiết bị mới

## 1. Người có thiết bị: tạo báo cáo

1. Tải `PinTray.exe` ở mục **Releases** rồi chạy nó.
2. Chuột phải vào biểu tượng PinTray → **Tạo báo cáo thiết bị (để thêm hỗ trợ)...**. Có thể chạy thẳng bằng lệnh `PinTray.exe --report`.
3. Trong 20 giây đang ghi:
   - Bật thiết bị ở chế độ không dây (2.4G hoặc Bluetooth), dùng nó bình thường.
   - Nếu được, mở app hoặc web driver của hãng để nó hiện % pin.
   - **Đừng gõ mật khẩu** hay nội dung riêng tư.
4. File `PinTray-report-....txt` sẽ nằm trên Desktop. Mở một **Issue** mới, đính kèm file, ghi tên mẫu thiết bị và % pin mà app của hãng đang báo.

PinTray chỉ đọc và không gửi lệnh nào xuống thiết bị. Báo cáo không ghi kênh bàn phím/chuột thông thường (nơi chứa nội dung bạn gõ). Nó chỉ ghi kênh vendor và kênh pin.

## 2. Người phát triển: viết driver

Mỗi hãng nằm trong một file `src/Drivers/<Hãng>.cs`, cài đặt interface `IDriver` (`src/Device.cs`):

```csharp
interface IDriver
{
    void Start();                    // chạy thread nền riêng
    List<DeviceStatus> Snapshot();   // gọi mỗi giây từ UI, không được chặn
}
```

Sau đó thêm driver vào `Drivers.All()` trong `src/Device.cs`.

`DeviceStatus` cần điền:

| Trường | Ý nghĩa |
|---|---|
| `Key` | id cố định, ví dụ `"mchose-xxxx"`, dùng để lưu cài đặt |
| `Name`, `Kind` | tên hiển thị, loại (Mouse / Keyboard / Headset) |
| `Present` | receiver hoặc thiết bị đang cắm |
| `Wired` | đang cắm cáp sạc. Biểu tượng hiện ⚡ |
| `HasReading`, `Last`, `LastFrame` | mức pin, trạng thái, thời điểm thiết bị trả lời gần nhất. Im lặng quá 10 giây được coi là đang ngủ |
| `RawInputMatch` | ví dụ `"VID_1D57&PID_FA60"`. Dùng để phát hiện receiver bị kẹt. Để `null` nếu không cần |
| `VendorApp` | đường dẫn app của hãng (nếu có) |

Tiện ích có sẵn trong `src/Hid.cs`: `Hid.Find(pattern)` liệt kê các collection kèm usage page và độ dài report, `Hid.OpenRead(path, ...)` mở để đọc ở chế độ chia sẻ, `Hid.CanOpen(path)` kiểm tra thiết bị còn cắm không.

### Nguyên tắc

- **Ưu tiên chỉ đọc.** Nếu thiết bị phải được hỏi mới trả lời, chỉ dùng đúng lệnh hỏi pin lấy từ app hoặc web driver của hãng. Không gửi lệnh đoán mò.
- Mở thiết bị ở chế độ chia sẻ (`Hid.OpenRead`), để app của hãng vẫn chạy song song được.
- Thêm test cho hàm giải mã gói tin trong `tests/Tests.cs`, dùng gói thật lấy từ báo cáo.
- Ghi rõ nguồn của giao thức trong comment đầu file. Không chép code từ dự án dùng license không tương thích với MIT (ví dụ GPL).

### Mẹo tìm giao thức

- Nhiều hãng (MCHOSE, VXE/VGN, Aula…) có **web driver** dùng WebHID. Mở DevTools của trình duyệt, xem các lệnh `sendReport` / `sendFeatureReport` và cách trang đó đọc `inputreport` khi hiện % pin.
- So sánh các gói ở nhiều mức pin khác nhau (ví dụ 80% và 70%) để tìm byte chứa mức pin.
