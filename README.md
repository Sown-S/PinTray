# PinTray

Hiá»‡n % pin chuá»™t vÃ  bÃ n phÃ­m khÃ´ng dÃ¢y ngay trÃªn khay há»‡ thá»‘ng Windows, cáº¡nh Ä‘á»“ng há»“: má»—i thiáº¿t bá»‹ má»™t biá»ƒu tÆ°á»£ng, con sá»‘ Ä‘á»•i mÃ u theo tráº¡ng thÃ¡i.

- **Nhá», khÃ´ng cáº§n cÃ i thÃªm gÃ¬:** má»™t file `.exe`, cháº¡y trÃªn .NET Framework cÃ³ sáºµn trong Windows 10/11.
- **Chá»‰ Ä‘á»c:** khÃ´ng gá»­i lá»‡nh nÃ o xuá»‘ng thiáº¿t bá»‹, cÃ³ thá»ƒ cháº¡y song song vá»›i app cá»§a hÃ£ng.
- **KhÃ´ng cáº§n máº¡ng, khÃ´ng cáº§n quyá»n admin.**

## Thiáº¿t bá»‹ há»— trá»£

| Thiáº¿t bá»‹ | Ghi chÃº |
|---|---|
| Attack Shark R1 (receiver 2.4G `1D57:FA60`) | ÄÃ£ thá»­. R1 bÃ¡o pin theo náº¥c 10% |
| Attack Shark X1, X11 vÃ  cÃ¡c máº«u khÃ¡c dÃ¹ng cÃ¹ng receiver | Theo giao thá»©c Ä‘Ã£ biáº¿t, chÆ°a thá»­ trÃªn mÃ¡y tháº­t |

Thiáº¿t bá»‹ cá»§a báº¡n chÆ°a cÃ³? Xem [docs/ADDING_A_DEVICE.md](docs/ADDING_A_DEVICE.md). Chá»‰ cáº§n táº¡o bÃ¡o cÃ¡o báº±ng menu, khÃ´ng cáº§n biáº¿t láº­p trÃ¬nh.

## CÃ i Ä‘áº·t

1. Táº£i `PinTray.exe` á»Ÿ má»¥c [**Releases**](../../releases/latest). File Ä‘Æ°á»£c GitHub Actions build tá»« mÃ£ nguá»“n, kÃ¨m mÃ£ SHA-256 Ä‘á»ƒ kiá»ƒm tra.
2. Äáº·t file vÃ o má»™t thÆ° má»¥c cá»‘ Ä‘á»‹nh, vÃ­ dá»¥ `%LOCALAPPDATA%\Programs\PinTray`, rá»“i cháº¡y.
3. Chuá»™t pháº£i vÃ o biá»ƒu tÆ°á»£ng â†’ **Khá»Ÿi Ä‘á»™ng cÃ¹ng Windows**.

File khÃ´ng Ä‘Æ°á»£c kÃ½ sá»‘, nÃªn Windows cÃ³ thá»ƒ hiá»‡n cáº£nh bÃ¡o SmartScreen: chá»n *More info* â†’ *Run anyway*.

## Biá»ƒu tÆ°á»£ng

| Hiá»ƒn thá»‹ | Ã nghÄ©a |
|---|---|
| ViÃªn pin Ä‘áº§y tráº¯ng / Ä‘en | Pin 100% |
| Sá»‘ tráº¯ng / Ä‘en (theo mÃ u taskbar) | Pin 60â€“90% |
| Sá»‘ vÃ ng | Pin 30â€“50% |
| Sá»‘ Ä‘á» | Pin 10â€“20%. CÃ³ thÃ´ng bÃ¡o á»Ÿ má»©c 20% vÃ  10% |
| Sá»‘ xÃ¡m | Thiáº¿t bá»‹ Ä‘ang ngá»§ hoáº·c PinTray vá»«a má»Ÿ. Hiá»‡n má»©c pin Ä‘Ã£ lÆ°u |
| âš¡ xanh lÃ¡, Ä‘áº§y dáº§n tá»« dÆ°á»›i lÃªn | Äang cáº¯m cÃ¡p sáº¡c |
| ViÃªn pin Ä‘áº§y xanh lÃ¡ | Äang cáº¯m cÃ¡p, Æ°á»›c tÃ­nh Ä‘Ã£ sáº¡c Ä‘áº§y (cÃ³ thÃ´ng bÃ¡o) |
| `!` cam | Receiver bá»‹ káº¹t: thiáº¿t bá»‹ váº«n cháº¡y nhÆ°ng khÃ´ng cÃ³ gÃ³i pin. RÃºt ra rá»“i cáº¯m láº¡i receiver |
| `?` | ChÆ°a tá»«ng nháº­n Ä‘Æ°á»£c má»©c pin |
| `â€“` | KhÃ´ng tháº¥y receiver |

## Menu chuá»™t pháº£i

- Tráº¡ng thÃ¡i, **thá»i gian cÃ²n láº¡i** (Æ°á»›c tÃ­nh) vÃ  láº§n cáº­p nháº­t cuá»‘i.
- Má»Ÿ app cá»§a hÃ£ng (náº¿u Ä‘Ã£ cÃ i).
- **Nháº¯c rÃºt sáº¡c sau** 1 / 1,5 / 2 / 3 giá» hoáº·c Táº¯t.
- **Má»Ÿ lá»‹ch sá»­ pin:** file CSV má»Ÿ báº±ng Excel Ä‘Æ°á»£c.
- **Táº¡o bÃ¡o cÃ¡o thiáº¿t bá»‹:** Ä‘á»ƒ thÃªm há»— trá»£ thiáº¿t bá»‹ má»›i.
- Khá»Ÿi Ä‘á»™ng cÃ¹ng Windows, ThoÃ¡t.

## CÃ¡ch cÃ¡c tÃ­nh nÄƒng hoáº¡t Ä‘á»™ng

- **Nhá»› má»©c pin:** lÆ°u trong `HKCU\Software\PinTray`. Sau khi khá»Ÿi Ä‘á»™ng láº¡i mÃ¡y, PinTray hiá»‡n ngay má»©c cÅ© (mÃ u xÃ¡m) cho Ä‘áº¿n khi thiáº¿t bá»‹ tráº£ lá»i.
- **Lá»‹ch sá»­:** `%LOCALAPPDATA%\PinTray\history.csv` ghi má»—i láº§n má»©c pin Ä‘á»•i, má»—i láº§n cáº¯m / rÃºt cÃ¡p vÃ  má»—i láº§n receiver bá»‹ káº¹t.
- **Æ¯á»›c tÃ­nh thá»i gian sáº¡c Ä‘áº§y:** khi cáº¯m cÃ¡p, chuá»™t khÃ´ng bÃ¡o % pin hay tráº¡ng thÃ¡i Ä‘áº§y. PinTray há»c tá»« cÃ¡c láº§n sáº¡c trÆ°á»›c: má»©c pin trÆ°á»›c khi cáº¯m, thá»i gian cáº¯m, vÃ  má»©c pin ngay sau khi rÃºt. Láº§n sáº¡c Ä‘áº§u tiÃªn chÆ°a cÃ³ dá»¯ liá»‡u nÃªn táº¡m tÃ­nh báº±ng thá»i gian *Nháº¯c rÃºt sáº¡c* (máº·c Ä‘á»‹nh 2 giá»). ÄÃ¨n trÃªn chuá»™t váº«n lÃ  chá»‰ bÃ¡o chÃ­nh xÃ¡c.
- **Æ¯á»›c tÃ­nh thá»i gian cÃ²n láº¡i:** chá»‰ tÃ­nh thá»i gian thiáº¿t bá»‹ *thá»©c*. Vá»›i thiáº¿t bá»‹ bÃ¡o pin theo náº¥c 10%, pháº£i Ä‘á»£i pin tá»¥t hai náº¥c má»›i cÃ³ sá»‘. Má»—i láº§n sáº¡c, Æ°á»›c tÃ­nh báº¯t Ä‘áº§u láº¡i.
- **PhÃ¡t hiá»‡n receiver káº¹t:** khi receiver im láº·ng, PinTray dÃ¹ng Raw Input Ä‘á»ƒ xem *riÃªng* thiáº¿t bá»‹ Ä‘Ã³ cÃ³ Ä‘ang Ä‘Æ°á»£c dÃ¹ng khÃ´ng. NÃ³ chá»‰ xem tÃªn thiáº¿t bá»‹, khÃ´ng xem ná»™i dung gÃµ. Pháº§n nÃ y chá»‰ báº­t lÃºc receiver im láº·ng.

## Build tá»« mÃ£ nguá»“n

```powershell
.\build.ps1          # táº¡o dist\PinTray.exe
.\build.ps1 -Test    # build vÃ  cháº¡y test
```

DÃ¹ng trÃ¬nh biÃªn dá»‹ch C# cÃ³ sáºµn trong Windows (`csc.exe` cá»§a .NET Framework 4.x), khÃ´ng cáº§n Visual Studio.

| ThÆ° má»¥c | Ná»™i dung |
|---|---|
| `src/` | LÃµi: khay há»‡ thá»‘ng (`App`, `DeviceTray`), HID, lÆ°u trá»¯, Æ°á»›c tÃ­nh, bÃ¡o cÃ¡o |
| `src/Drivers/` | Má»—i hÃ£ng má»™t file |
| `tests/` | Test giáº£i mÃ£ gÃ³i tin vÃ  Æ°á»›c tÃ­nh |
| `.github/workflows/` | Build + test má»—i láº§n push; táº¡o release khi push tag `v*` |

## PhÃ¡t hÃ nh phiÃªn báº£n má»›i

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions sáº½ build, cháº¡y test rá»“i táº¡o release kÃ¨m `PinTray.exe` vÃ  `SHA256SUMS.txt`.

## License

[MIT](LICENSE)
