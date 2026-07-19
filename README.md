# KLAUNCHER - GTA V VMP Edition

[English](#english) | [فارسی](#farsi)

---

<a name="english"></a>
## English

An open-source Windows launcher for **GTA V VMP Edition**, built with **C# / WPF / .NET 10**. Download and install GTA V via BitTorrent with a single click.

### Features

- **BitTorrent Download Engine** — Downloads GTA V directly from the se7en.ws P2P swarm using aria2. No file hosting needed. Connects to 4 trackers with up to 512 peers for maximum speed
- **Modern Premium UI** — Borderless window with glassmorphism effects, neon purple gradients, custom title bar, animated logo with dynamic glow
- **Disk Space Verification** — Checks available disk space before download starts. Requires ~80 GB free
- **Download Manager with Pause/Resume** — Pause and resume the torrent download at any time. Progress is saved automatically
- **Discord Rich Presence** — Real-time Discord status showing download speed, ETA, file progress, seeds/peers count
- **Prerequisites Installer** — One-click silent install of DirectX 11 Runtime and Visual C++ Redistributable 2015-2022
- **Auto-Run Setup** — Automatically launches setup.exe after the download completes
- **Smart Resume on Restart** — Closing and reopening the launcher detects incomplete downloads and resumes from where it left off
- **Bilingual UI** — English and Farsi (Persian) support in dialogs

### System Requirements

- **Windows 10/11** (64-bit)
- **~80 GB free disk space**
- Internet connection

### Download

Download the latest release from [Releases](https://github.com/userbabn/klauncher/releases/latest):

- **KLauncher_x64.zip** — For 64-bit Windows (recommended)
- **KLauncher_x86.zip** — For 32-bit Windows

No .NET runtime needed — the app is self-contained.

### How It Works

1. Click **INSTALL** in the sidebar
2. Choose an installation folder (disk space is checked automatically)
3. Click **DOWNLOAD GTA V** and confirm
4. The launcher uses the bundled `.torrent` file to connect to the se7en.ws swarm
5. aria2 downloads 140 files (~64 GB) with real-time progress (speed, ETA, peers)
6. `setup.exe` runs automatically when download completes
7. Click **PLAY** to launch GTA V VMP Edition

### Build from Source

```bash
git clone https://github.com/userbabn/klauncher.git
cd klauncher
dotnet restore
dotnet run

# Publish
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Project Structure

```
klauncher/
  MainWindow.xaml/.cs              # Main window with sidebar and news panel
  InstallControl.xaml/.cs          # Folder picker, disk space check, download trigger
  DownloadProgressControl.xaml/.cs # Progress bars, speed, ETA, pause button
  ConfirmDialog.xaml/.cs           # Download confirmation dialog
  LauncherService.cs               # Torrent download engine via aria2
  DiscordService.cs                # Discord Rich Presence integration
  aria2c.exe                       # aria2 download engine
  aria2.conf                       # aria2 configuration
  gta-v_legacy.torrent             # Bundled torrent file
```

### License

MIT License - Copyright (c) 2026 userbabn

---

<a name="farsi"></a>
## فارسی (Persian)

لانچر متن‌باز ویندوز برای **بازی GTA V نسخه VMP Edition**، توسعه یافته با **C# / WPF / .NET 10**. دانلود و نصب GTA V از طریق BitTorrent با یک کلیک.

### ویژگی‌ها

- **موتور دانلود BitTorrent** — دانلود مستقیم GTA V از شبکه P2P سایت se7en.ws با استفاده از aria2. بدون نیاز به هاست. اتصال به ۴ ترکر با حداکثر ۵۱۲ پیر برای حداکثر سرعت
- **رابط کاربری مدرن و پرمیوم** — پنجره بدون حاشیه با افکت‌های شیشه‌ای، گرادیان‌های بنفش نئون، نوار عنوان سفارشی، لوگوی متحرک با درخشش پویا
- **بررسی فضای دیسک** — بررسی فضای خالی دیسک قبل از شروع دانلود. حداقل ~۸۰ گیگابایت فضا لازم است
- **مدیریت دانلود با مکث و ادامه** — مکث و ادامه دانلود torrent در هر زمان. پیشرفت به طور خودکار ذخیره می‌شود
- **ارتباط با دیسکورد (Discord Rich Presence)** — نمایش وضعیت لحظه‌ای در دیسکورد شامل سرعت دانلود، زمان باقی‌مانده، پیشرفت فایل‌ها
- **نصب پیش‌نیازها** — نصب بی‌صدا و خودکار DirectX 11 Runtime و Visual C++ Redistributable 2015-2022
- **اجرای خودکار Setup** — اجرای خودکار setup.exe پس از تکمیل دانلود
- **شروع خودکار از سرگیری** — با بستن و باز کردن مجدد لانچر، دانلودهای ناقص شناسایی و از همان نقطه ادامه می‌یابند
- **رابط دوزبانه** — پشتیبانی از انگلیسی و فارسی

### نیازمندی‌های سیستم

- **ویندوز ۱۰/۱۱** (۶۴ بیتی)
- **~۸۰ گیگابایت فضای خالی دیسک**
- اتصال اینترنت

### دانلود

آخرین نسخه را از [Releases](https://github.com/userbabn/klauncher/releases/latest) دانلود کنید:

- **KLauncher_x64.zip** — برای ویندوز ۶۴ بیتی (توصیه شده)
- **KLauncher_x86.zip** — برای ویندوز ۳۲ بیتی

بدون نیاز به .NET Runtime — برنامه کاملاً مستقل است.

### نحوه کار

1. روی **INSTALL** در نوار کناری کلیک کنید
2. پوشه نصب را انتخاب کنید (فضای دیسک خودکار بررسی می‌شود)
3. روی **DOWNLOAD GTA V** کلیک و تایید کنید
4. لانچر از فایل `.torrent` موجود برای اتصال به شبکه se7en.ws استفاده می‌کند
5. aria2 تعداد ۱۴۰ فایل (~۶۴ گیگابایت) را با پیشرفت لحظه‌ای دانلود می‌کند
6. `setup.exe` پس از تکمیل دانلود خودکار اجرا می‌شود
7. روی **PLAY** کلیک کنید تا GTA V VMP Edition اجرا شود

### کامپایل از سورس

```bash
git clone https://github.com/userbabn/klauncher.git
cd klauncher
dotnet restore
dotnet run

# خروجی
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### ساختار پروژه

```
klauncher/
  MainWindow.xaml/.cs              # پنجره اصلی با نوار کناری و پنل اخبار
  InstallControl.xaml/.cs          # انتخاب پوشه، بررسی فضای دیسک، شروع دانلود
  DownloadProgressControl.xaml/.cs # نوار پیشرفت، سرعت، زمان باقی‌مانده، دکمه مکث
  ConfirmDialog.xaml/.cs           # دیالوگ تایید دانلود
  LauncherService.cs               # موتور دانلود torrent با aria2
  DiscordService.cs                # ارتباط Discord Rich Presence
  aria2c.exe                       # موتور دانلود aria2
  aria2.conf                       # تنظیمات aria2
  gta-v_legacy.torrent             # فایل torrent موجود در برنامه
```

### لایسنس

لایسنس MIT - کپی‌رایت (c) 2026 userbabn

---

## Contributing / مشارکت

This project is open-source under the **MIT License**. Feel free to fork and modify.

این پروژه متن‌باز تحت لایسنس **MIT** منتشر شده است. آزادانه فورک و تغییر دهید.
