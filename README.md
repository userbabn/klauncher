# KLAUNCHER: Lanzador de GTA V + VMP Edition 🐨🎮

Lanzador (Launcher) de código abierto para Windows desarrollado en C# y WPF bajo **.NET 10**. Permite a los usuarios descargar, verificar y descomprimir de manera totalmente automatizada el instalador dividido en 29 partes de GTA V (VMP Edition), así como instalar los requisitos del juego.

---

## 🎨 Características Principales
*   **Diseño Premium y Moderno:** Interfaz sin bordes (Borderless Window) con efectos de cristal, gradientes morados y rosa neón, barra de título personalizada para arrastrar y un logo de un **Koala Gamer** con iluminación de sombra dinámica.
*   **Gestor de Descargas con Pausa y Resumen:** Descarga secuencial de las 29 partes de 2.4 GB (~70 GB en total). Si se cae la conexión o el usuario pulsa **Pausar**, la descarga se puede reanudar desde el byte exacto donde se detuvo gracias a las peticiones parciales HTTP (`Range: bytes=`).
*   **Descompresión y Limpieza Automática:** Al finalizar la descarga, descomprime automáticamente la secuencia completa (desde `part01.rar` hasta `part29.rar`) usando la librería nativa de alto rendimiento `SharpCompress` e informa del progreso archivo por archivo. **Los archivos `.rar` son eliminados automáticamente** al terminar la extracción, recuperando ~70 GB de espacio.
*   **Discord Rich Presence:** Muestra en tiempo real el estado del launcher en Discord: menú principal, descargando (con parte actual y velocidad), extrayendo, jugando o completado. Requiere crear una aplicación en el [Discord Developer Portal](https://discord.com/developers/applications) y configurar el `CLIENT_ID` en `DiscordService.cs`.
*   **Ícono personalizado:** El ejecutable `.exe` y la ventana de la aplicación muestran el logo del Koala Gamer como ícono.
*   **Instalador de Requisitos:** Automatiza la descarga e instalación silenciosa de pre-requisitos necesarios para jugar:
    *   Microsoft DirectX 11 Runtime
    *   Visual C++ Redistributable 2015-2022
*   **Sección de Noticias:** Slider de novedades interactivo con un paginador (`◀` y `▶`) para mantener informados a los usuarios.
*   **Autocontenido:** Se puede compilar en un único archivo ejecutable `.exe` independiente de unos 70 MB con todas las imágenes, librerías e iconos integrados dentro del mismo binario.

---

## 📂 Estructura del Código Fuente
El código está organizado de forma modular dentro de las siguientes clases:
*   `klauncher.csproj`: Definición del proyecto, uso del SDK de WindowsDesktop para WPF, y dependencias (SharpCompress).
*   `LauncherService.cs`: Lógica de negocio de bajo nivel (HttpClient por bloques, streams de extracción de archivos con SharpCompress, y procesos de ejecución de instaladores de pre-requisitos).
*   `MainWindow.xaml` / `.xaml.cs`: Estructura del shell principal, barra superior, menú lateral y lógica del carrusel de noticias.
*   `InstallControl.xaml` / `.xaml.cs`: Componente del instalador (selección de carpeta con el selector de Windows, verificación de espacio y menú de requisitos).
*   `DownloadProgressControl.xaml` / `.xaml.cs`: Barra de progreso de descarga/extracción, cálculo de velocidad en tiempo real y controlador de pausa/reanudar.
*   `ConfirmDialog.xaml` / `.xaml.cs`: Cuadro de diálogo modal estético para confirmar la descarga de alta capacidad.
*   `koala_logo.png`: Recurso gráfico de la marca embebido dentro del ejecutable.

---

## 🛠️ Requisitos de Desarrollo
Para abrir, modificar o compilar este código fuente en tu máquina necesitas:
*   **Windows OS** (10 u 11 recomendado).
*   **.NET 10 SDK** o superior instalado.
*   Visual Studio 2022 (con carga de trabajo de desarrollo de escritorio de .NET) o VS Code con C# Dev Kit.

---

## 🚀 Instrucciones Paso a Paso para Compilar y Ejecutar

### 1. Clonar el repositorio
Abre una terminal y clona el proyecto en tu carpeta de preferencia.

### 2. Restaurar dependencias de NuGet
Ejecuta el siguiente comando para descargar los paquetes externos necesarios (como SharpCompress):
```powershell
dotnet restore
```

### 3. Ejecutar en modo desarrollo
Si deseas lanzar el launcher directamente para depurar o probar cambios:
```powershell
dotnet run
```

### 4. Compilar en modo Release (Carpeta)
Para compilar la aplicación optimizada pero con los archivos DLL sueltos:
```powershell
dotnet build -c Release
```

---

## 📦 Publicación del Ejecutable Único (`.exe` Portátil)

Uno de los mayores atractivos de este proyecto es que puedes empaquetarlo todo en **un solo archivo ejecutable independiente** que los jugadores pueden abrir directamente sin instalar nada más en su computadora.

Para generarlo, abre PowerShell en la raíz del proyecto y ejecuta:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFileBundle=true
```

### Explicación de los parámetros:
*   `-c Release`: Genera la versión final optimizada.
*   `-r win-x64`: Empaqueta específicamente para sistemas Windows de 64 bits.
*   `--self-contained true`: Incluye el entorno de ejecución de .NET 10 dentro del ejecutable, por lo que el jugador **no necesita tener instalado .NET**.
*   `-p:PublishSingleFile=true`: Agrupa todos los archivos DLL y recursos (incluido el logo del Koala) en un único `.exe`.
*   `-p:PublishReadyToRun=true`: Precompila el código para acelerar el tiempo de inicio de la aplicación en el ordenador del usuario.
*   `-p:EnableCompressionInSingleFileBundle=true`: Comprime el binario final para reducir significativamente el peso de distribución.

Una vez que termine la compilación, encontrarás tu archivo ejecutable listo para distribuir en la ruta:
📁 `bin\Release\net10.0-windows\win-x64\publish\klauncher.exe`

---

## 🤝 Contribuciones y Licencia
Este proyecto es de código abierto (Open Source). Cualquier contribución es bienvenida mediante Pull Requests. Siéntete libre de clonarlo, bifurcarlo (fork) y adaptarlo para las necesidades de tu propia comunidad de juego.
