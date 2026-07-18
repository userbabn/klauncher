# KLAUNCHER - GTA V VMP Edition

[English](#english) | [فارسی](#farsi)

---

<a name="english"></a>
## English

An open-source Windows launcher for **GTA V VMP Edition**, built with **C# / WPF / .NET 10**. Download, extract, and launch GTA V VMP Edition with a single click.

### Features

- **Modern Premium UI** - Borderless window with glassmorphism effects, neon purple/pink gradients, custom title bar, animated Koala Gamer logo with dynamic glow
- **Download Manager with Pause/Resume** - Sequential download of 29 RAR parts (~70 GB total) with per-byte resume capability. Auto-resumes on launcher restart
- **Animated Download Indicator** - Pulsing LED status dot and animated cloud download icon clearly show active download state
- **Automatic Extraction** - Extracts all RAR parts using SharpCompress with optimized 256 KB buffers. RAR files are auto-deleted after extraction (~70 GB freed)
- **Discord Rich Presence** - Real-time Discord status: menu, downloading (part/speed/ETA), paused, extracting, playing, or completed
- **Prerequisites Installer** - One-click silent install of DirectX 11 Runtime and Visual C++ Redistributable 2015-2022
- **High-Performance I/O** - Disk space pre-allocation with `SetLength` to reduce HDD fragmentation during download and extraction
- **Smart Resume on Restart** - Closing and reopening the launcher detects incomplete downloads and resumes automatically from where it left off

### Requirements

- **Windows 10/11**
- **.NET 10 SDK** or higher
- Visual Studio 2022 or VS Code with C# Dev Kit

### Build & Run

```bash
# Clone
git clone https://github.com/userbabn/klauncher.git
cd klauncher

# Restore dependencies
dotnet restore

# Run in development
dotnet run

# Build Release
dotnet build -c Release

# Publish single portable .exe
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

### Project Structure

```
klauncher/
  MainWindow.xaml/.cs          # Main window with sidebar navigation and news panel
  InstallControl.xaml/.cs      # Installation folder picker and download trigger
  DownloadProgressControl.xaml/.cs  # Download progress bars, speed, ETA, pause button
  ConfirmDialog.xaml/.cs       # Download confirmation dialog
  LauncherService.cs           # Core download/extraction engine with resume support
  DiscordService.cs            # Discord Rich Presence integration
  koala_logo.png/.ico          # Application assets
```

### How It Works

1. Click **INSTALAR** in the sidebar to open the installer
2. Choose an installation folder and click **DESCARGAR GTA V**
3. Confirm the download in the dialog
4. The launcher downloads 29 RAR parts sequentially from `cdn.vmp.ir`
5. Each part is saved with a `.tmp` extension and renamed on completion
6. Progress is saved to `download_state.json` after each part
7. After all parts download, RAR files are extracted automatically
8. RAR files are deleted to free ~70 GB of disk space
9. Click **JUGAR** to launch GTA V VMP Edition

### License

MIT License - Copyright (c) 2026 Koala Gamer

---

<a name="farsi"></a>
## فارسی (Persian)

لانچر متن‌باز ویندوز برای **بازی GTA V نسخه VMP Edition**، توسعه یافته با **C# / WPF / .NET 10**. دانلود، استخراج و اجرای بازی GTA V VMP Edition با یک کلیک.

### ویژگی‌ها

- **رابط کاربری مدرن و پرمیوم** - پنجره بدون حاشیه با افکت‌های شیشه‌ای، گرادیان‌های بنفش و صورتی نئون، نوار عنوان سفارشی، لوگوی متحرک کوالا گیمر با درخشش پویا
- **مدیریت دانلود با قابلیت مکث و ادامه** - دانلود متوالی ۲۹ پارت RAR (حدود ۷۰ گیگابایت) با قابلیت ادامه از بایت دقیق متوقف شده. شروع خودکار هنگام بازگشایی مجدد لانچر
- **نشانگر دانلود متحرک** - نقطه وضعیت ال‌ای‌دی پالس‌زننده و آیکون ابری متحرک دانلود، وضعیت دانلود فعال را به وضوح نشان می‌دهد
- **استخراج خودکار** - استخراج تمام پارت‌های RAR با SharpCompress و بافر بهینه ۲۵۶ کیلوبایتی. فایل‌های RAR پس از استخراج حذف می‌شوند (حدود ۷۰ گیگابایت آزاد)
- **ارتباط با دیسکورد (Discord Rich Presence)** - نمایش وضعیت لحظه‌ای در دیسکورد: منو، دانلود (پارت/سرعت/زمان باقی‌مانده)، مکث، استخراج، در حال بازی یا تکمیل شده
- **نصب پیش‌نیازها** - نصب بی‌صدا و خودکار DirectX 11 Runtime و Visual C++ Redistributable 2015-2022 با یک کلیک
- **بهینه‌سازی I/O با کارایی بالا** - تخصیص پیشاپیش فضای دیسک با `SetLength` برای کاهش تکه‌تکه شدن HDD هنگام دانلود و استخراج
- **شروع خودکار از سرگیری** - با بستن و باز کردن مجدد لانچر، دانلودهای ناقص شناسایی و به طور خودکار از همان نقطه ادامه می‌یابند

### نیازمندی‌ها

- **ویندوز ۱۰/۱۱**
- **اس‌دی‌کی .NET 10** یا بالاتر
- ویژوال استودیو ۲۰۲۲ یا VS Code با C# Dev Kit

### کامپایل و اجرا

```bash
# کلون کردن
git clone https://github.com/userbabn/klauncher.git
cd klauncher

# بازیابی پکیج‌ها
dotnet restore

# اجرا در حالت توسعه
dotnet run

# کامپایل Release
dotnet build -c Release

# خروجی تک فایل پرتابل (.exe)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

### ساختار پروژه

```
klauncher/
  MainWindow.xaml/.cs          # پنجره اصلی با نوار کناری و پنل اخبار
  InstallControl.xaml/.cs      # انتخاب پوشه نصب و شروع دانلود
  DownloadProgressControl.xaml/.cs  # نوار پیشرفت، سرعت، زمان باقی‌مانده، دکمه مکث
  ConfirmDialog.xaml/.cs       # دیالوگ تایید دانلود
  LauncherService.cs           # موتور دانلود/استخراج با پشتیبانی از ادامه
  DiscordService.cs            # ارتباط Discord Rich Presence
  koala_logo.png/.ico          # دارایی‌های برنامه
```

### نحوه کار

1. روی **INSTALAR** در نوار کناری کلیک کنید تا نصب‌کننده باز شود
2. پوشه نصب را انتخاب کنید و روی **DESCARGAR GTA V** کلیک کنید
3. دانلود را در دیالوگ تایید کنید
4. لانچر ۲۹ پارت RAR را به صورت متوالی از `cdn.vmp.ir` دانلود می‌کند
5. هر پارت با پسوند `.tmp` ذخیره و پس از تکمیل تغییر نام می‌یابد
6. پیشرفت پس از هر پارت در `download_state.json` ذخیره می‌شود
7. پس از دانلود تمام پارت‌ها، فایل‌های RAR به طور خودکار استخراج می‌شوند
8. فایل‌های RAR برای آزادسازی حدود ۷۰ گیگابایت فضا حذف می‌شوند
9. روی **JUGAR** کلیک کنید تا GTA V VMP Edition اجرا شود

### لایسنس

لایسنس MIT - کپی‌رایت (c) 2026 Koala Gamer

---

## Contributing / مشارکت

This project is open-source under the **MIT License**. Feel free to fork and modify.

این پروژه متن‌باز تحت لایسنس **MIT** منتشر شده است. آزادانه فورک و تغییر دهید.
