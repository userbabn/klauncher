# KLAUNCHER: GTA V + VMP Edition 🐨🎮

[Español](#es) | [English](#en) | [فارسی](#fa)

---

<a name="es"></a>
## 🇪🇸 Español

Lanzador (Launcher) de código abierto para Windows desarrollado en C# y WPF bajo **.NET 10**. Permite a los usuarios descargar, verificar y descomprimir de manera totalmente automatizada el instalador dividido en 29 partes de GTA V (VMP Edition), así como instalar los requisitos del juego.

### 🎨 Características Principales
*   **Diseño Premium y Moderno:** Interfaz sin bordes (Borderless Window) con efectos de cristal, gradientes morados y rosa neón, barra de título personalizada para arrastrar, logo de un **Koala Gamer** con iluminación de sombra dinámica e indicadores visuales de descarga LED neón animados.
*   **Gestor de Descargas con Pausa, Resumen y Auto-reanudación:** Descarga secuencial de las 29 partes de 2.4 GB (~70 GB en total). Si se cae la conexión o el usuario pulsa **Pausar**, la descarga se puede reanudar desde el byte exacto donde se detuvo. Al cerrar y reabrir el launcher, este detecta el progreso e inicia la descarga automáticamente.
*   **Optimizaciones de I/O de Alto Rendimiento:** Pre-asignación de tamaño de archivos en disco al descargar y extraer para reducir la fragmentación en HDDs e incrementar las velocidades de escritura.
*   **Descompresión y Limpieza Automática:** Al finalizar la descarga, descomprime automáticamente la secuencia completa usando la librería `SharpCompress` con buffers optimizados a 256 KB. **Los archivos `.rar` son eliminados automáticamente** al terminar la extracción, recuperando ~70 GB de espacio.
*   **Discord Rich Presence:** Muestra en tiempo real el estado del launcher en Discord: menú principal, descargando (con parte actual, velocidad y tiempo restante), extrayendo, jugando o completado.
*   **Instalador de Requisitos:** Automatiza la descarga e instalación silenciosa de DirectX 11 Runtime y Visual C++ Redistributable 2015-2022.

### 🛠️ Requisitos de Desarrollo
*   **Windows OS** (10 u 11 recomendado).
*   **.NET 10 SDK** o superior instalado.
*   Visual Studio 2022 o VS Code con C# Dev Kit.

### 🚀 Instrucciones Paso a Paso para Compilar y Ejecutar

#### 1. Clonar el repositorio
```powershell
git clone https://github.com/userbabn/klauncher.git
```
#### 2. Restaurar dependencias de NuGet
```powershell
dotnet restore
```
#### 3. Ejecutar en modo desarrollo
```powershell
dotnet run
```
#### 4. Compilar en modo Release (Carpeta)
```powershell
dotnet build -c Release
```
#### 5. Publicación del Ejecutable Único (`.exe` Portátil)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

---

<a name="en"></a>
## 🇬🇧 English

An open-source Windows Launcher developed in C# and WPF under **.NET 10**. It enables users to download, verify, and automatically extract GTA V (VMP Edition) split into 29 parts, as well as install game prerequisites.

### 🎨 Key Features
*   **Premium & Modern Design:** Borderless window interface with glassmorphism, purple and neon pink gradients, custom title bar for dragging, a custom **Koala Gamer** logo with dynamic drop shadow, and neon LED animated download indicators.
*   **Download Manager with Pause, Resume & Auto-Resume:** Sequential download of 29 parts of 2.4 GB (~70 GB total). If the connection drops or the user pauses, it resumes from the exact byte. On application startup, it automatically detects incomplete downloads and resumes immediately.
*   **High-Performance I/O Optimizations:** Disk space pre-allocation (`SetLength`) during download and extraction to prevent fragmentation on HDDs and maximize write speeds.
*   **Automatic Extraction & Cleanup:** Automatically extracts the RAR sequence using `SharpCompress` with optimized 256 KB buffers. **All `.rar` files are automatically deleted** after extraction to free up ~70 GB of disk space.
*   **Discord Rich Presence:** Real-time Discord status updates: main menu, downloading (with current part, speed, and ETA), extracting, playing, or completed.
*   **Prerequisites Installer:** Automates silent download and installation of DirectX 11 Runtime and Visual C++ Redistributable 2015-2022.

### 🛠️ Development Requirements
*   **Windows OS** (10 or 11 recommended).
*   **.NET 10 SDK** or higher installed.
*   Visual Studio 2022 or VS Code with C# Dev Kit.

### 🚀 Build and Run Instructions

#### 1. Clone the repository
```powershell
git clone https://github.com/userbabn/klauncher.git
```
#### 2. Restore NuGet dependencies
```powershell
dotnet restore
```
#### 3. Run in development mode
```powershell
dotnet run
```
#### 4. Build in Release mode (Folder)
```powershell
dotnet build -c Release
```
#### 5. Publish Single Portable Executable (`.exe`)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

---

<a name="fa"></a>
## 🇮🇷 فارسی (Persian)

لانچر متن‌باز ویندوز برای بازی GTA V نسخه VMP Edition توسعه یافته با زبان سی‌شارپ (#C) و WPF تحت فریم‌ورک **NET 10.**. این برنامه به کاربران اجازه می‌دهد بازی تقسیم شده به ۲۹ پارت (حدود ۷۰ گیگابایت) را به‌صورت کاملاً خودکار دانلود، تأیید و استخراج کرده و پیش‌نیازهای بازی را نصب کنند.

### 🎨 ویژگی‌های اصلی
*   **طراحی نئون و پرمیوم:** رابط کاربری بدون حاشیه (Borderless) با افکت‌های شیشه‌ای، طیف‌های رنگی بنفش و صورتی نئون، لوگوی اختصاصی کوالا گیمر به همراه سایه داینامیک و چراغ‌های وضعیت ال‌ای‌دی متحرک.
*   **مدیریت دانلود با قابلیت مکث، ادامه و شروع خودکار:** دانلود متوالی ۲۹ پارت ۲.۴ گیگابایتی. در صورت قطع اتصال یا کلیک بر روی دکمه مکث، دانلود از بایت دقیق متوقف شده ادامه می‌یابد. همچنین با باز کردن مجدد لانچر، فرآیند دانلود به‌طور خودکار از سر گرفته می‌شود.
*   **بهینه‌سازی نوشتن روی دیسک (I/O Optimizations):** تخصیص پیشاپیش فضای دیسک (`SetLength`) هنگام دانلود و استخراج فایل‌ها برای جلوگیری از تکه‌تکه شدن هارد دیسک (HDD) و افزایش سرعت ذخیره‌سازی.
*   **استخراج و پاکسازی خودکار فایل‌ها:** استخراج خودکار پارت‌های RAR با استفاده از کتابخانه `SharpCompress` با بافر بهینه‌سازی شده ۲۵۶ کیلوبایتی. پس از اتمام استخراج، **فایل‌های فشرده RAR به طور خودکار حذف می‌شوند** تا ۷۰ گیگابایت فضای خالی دیسک آزاد شود.
*   **ارتباط با دیسکورد (Discord Rich Presence):** نمایش وضعیت در دیسکورد به همراه مشخصات پارت در حال دانلود، سرعت دانلود، زمان باقی‌مانده و دکمه ورود به دیسکورد سرور.
*   **نصب خودکار پیش‌نیازها:** دانلود و نصب کاملاً بی‌صدا و پس‌زمینه DirectX 11 و Visual C++ Redistributable 2015-2022.

### 🛠️ نیازمندی‌های توسعه
*   **ویندوز** (نسخه ۱۰ یا ۱۱ پیشنهاد می‌شود).
*   **اس‌دی‌کی دات‌نت ۱۰** (.NET 10 SDK) یا بالاتر.
*   ویژوال استودیو ۲۰۲۲ یا VS Code به همراه C# Dev Kit.

### 🚀 راهنمای کامپایل و اجرا

#### ۱. کلون کردن مخزن
```powershell
git clone https://github.com/userbabn/klauncher.git
```
#### ۲. بازیابی پکیج‌های NuGet
```powershell
dotnet restore
```
#### ۳. اجرا در حالت توسعه
```powershell
dotnet run
```
#### ۴. کامپایل در حالت Release (پوشه)
```powershell
dotnet build -c Release
```
#### ۵. خروجی گرفتن به‌صورت تک فایل پرتابل (`.exe`)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

---

## 🤝 Contribuciones y Licencia / License & Contributions / مشارکت و لایسنس
This project is open-source under the **MIT License**. Feel free to fork and modify it.  
Este proyecto es de código abierto bajo la licencia **MIT**.  
این پروژه متن‌باز تحت لایسنس **MIT** منتشر شده است.
