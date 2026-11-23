# PinTray

Hiện % pin chuột và bàn phím không dây ngay trên khay hệ thống Windows, cạnh đồng hồ: mỗi thiết bị một biểu tượng, con số đổi màu theo trạng thái.

- **Nhỏ, không cần cài thêm gì:** một file `.exe`, chạy trên .NET Framework có sẵn trong Windows 10/11.
- **Chỉ đọc:** không gửi lệnh nào xuống thiết bị, có thể chạy song song với app của hãng.
- **Không cần mạng, không cần quyền admin.**

## Thiết bị hỗ trợ

| Thiết bị | Ghi chú |
|---|---|
| Attack Shark R1 (receiver 2.4G `1D57:FA60`) | Đã thử. R1 báo pin theo nấc 10% |
| Attack Shark X1, X11 và các mẫu khác dùng cùng receiver | Theo giao thức đã biết, chưa thử trên máy thật |

Thiết bị của bạn chưa có? Xem [docs/ADDING_A_DEVICE.md](docs/ADDING_A_DEVICE.md). Chỉ cần tạo báo cáo bằng menu, không cần biết lập trình.

## Cài đặt

1. Tải `PinTray.exe` ở mục [**Releases**](../../releases/latest). File được GitHub Actions build từ mã nguồn, kèm mã SHA-256 để kiểm tra.
2. Đặt file vào một thư mục cố định, ví dụ `%LOCALAPPDATA%\Programs\PinTray`, rồi chạy.
3. Chuột phải vào biểu tượng → **Khởi động cùng Windows**.

File không được ký số, nên Windows có thể hiện cảnh báo SmartScreen: chọn *More info* → *Run anyway*.

## Biểu tượng

| Hiển thị | Ý nghĩa |
|---|---|
| Viên pin đầy trắng / đen | Pin 100% |
| Số trắng / đen (theo màu taskbar) | Pin 60–90% |
| Số vàng | Pin 30–50% |
| Số đỏ | Pin 10–20%. Có thông báo ở mức 20% và 10% |
| Số xám | Thiết bị đang ngủ hoặc PinTray vừa mở. Hiện mức pin đã lưu |
| Viên pin có tia sét, lõi xanh dâng dần | Đang cắm cáp sạc |
| Viên pin đầy xanh lá | Đang cắm cáp, ước tính đã sạc đầy (có thông báo) |
| `!` cam | Receiver bị kẹt: thiết bị vẫn chạy nhưng không có gói pin. Rút ra rồi cắm lại receiver |
| `?` | Chưa từng nhận được mức pin |
| `–` | Không thấy receiver |

## Menu chuột phải

- Trạng thái, **thời gian còn lại** (ước tính) và lần cập nhật cuối.
- Mở app của hãng (nếu đã cài).
- **Nhắc rút sạc sau** 1 / 1,5 / 2 / 3 giờ hoặc Tắt.
- **Mở lịch sử pin:** file CSV mở bằng Excel được.
- **Tạo báo cáo thiết bị:** để thêm hỗ trợ thiết bị mới.
- Khởi động cùng Windows, Thoát.

## Cách các tính năng hoạt động

- **Nhớ mức pin:** lưu trong `HKCU\Software\PinTray`. Sau khi khởi động lại máy, PinTray hiện ngay mức cũ (màu xám) cho đến khi thiết bị trả lời.
- **Lịch sử:** `%LOCALAPPDATA%\PinTray\history.csv` ghi mỗi lần mức pin đổi, mỗi lần cắm / rút cáp và mỗi lần receiver bị kẹt.
- **Ước tính thời gian sạc đầy:** khi cắm cáp, chuột không báo % pin hay trạng thái đầy. PinTray học từ các lần sạc trước: mức pin trước khi cắm, thời gian cắm, và mức pin ngay sau khi rút. Lần sạc đầu tiên chưa có dữ liệu nên tạm tính bằng thời gian *Nhắc rút sạc* (mặc định 2 giờ). Đèn trên chuột vẫn là chỉ báo chính xác.
- **Ước tính thời gian còn lại:** chỉ tính thời gian thiết bị *thức*. Với thiết bị báo pin theo nấc 10%, phải đợi pin tụt hai nấc mới có số. Mỗi lần sạc, ước tính bắt đầu lại.
- **Phát hiện receiver kẹt:** khi receiver im lặng, PinTray dùng Raw Input để xem *riêng* thiết bị đó có đang được dùng không. Nó chỉ xem tên thiết bị, không xem nội dung gõ. Phần này chỉ bật lúc receiver im lặng.

## Build từ mã nguồn

```powershell
.\build.ps1          # tạo dist\PinTray.exe
.\build.ps1 -Test    # build và chạy test
```

Dùng trình biên dịch C# có sẵn trong Windows (`csc.exe` của .NET Framework 4.x), không cần Visual Studio.

| Thư mục | Nội dung |
|---|---|
| `src/` | Lõi: khay hệ thống (`App`, `DeviceTray`), HID, lưu trữ, ước tính, báo cáo |
| `src/Drivers/` | Mỗi hãng một file |
| `tests/` | Test giải mã gói tin và ước tính |
| `.github/workflows/` | Build + test mỗi lần push; tạo release khi push tag `v*` |

## Phát hành phiên bản mới

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions sẽ build, chạy test rồi tạo release kèm `PinTray.exe` và `SHA256SUMS.txt`.

## License

[MIT](LICENSE)
