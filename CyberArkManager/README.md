# CyberArk Manager — Navaja Suiza PAM

Aplicación de escritorio WPF (.NET 8) para gestionar CyberArk PAM a través de la REST API del PVWA.

---

## Requisitos

- Windows 10/11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Acceso de red al PVWA de CyberArk

---

## Compilar y Ejecutar

```powershell
# 1. Restaurar paquetes NuGet
dotnet restore

# 2. Compilar
dotnet build -c Release

# 3. Ejecutar
dotnet run
```

O abrir `CyberArkManager.sln` en Visual Studio 2022 y pulsar **F5**.

---

## Estructura del Proyecto

```
CyberArkManager/
├── Models/
│   ├── Account.cs          # Cuenta CyberArk + DTOs de API
│   ├── Safe.cs             # Safe + respuesta paginada
│   └── UserSession.cs      # Sesión activa + AppConfiguration
│
├── Services/
│   ├── AuthService.cs      # Login, Keep-Alive (Timer), Logoff
│   ├── CyberArkApiService.cs # REST API wrapper completo
│   └── CsvService.cs       # Generación/parseo CSV Bulk Load
│
├── ViewModels/
│   ├── BaseViewModel.cs           # INotifyPropertyChanged + helpers
│   ├── MainViewModel.cs           # Shell, navegación, sesión
│   ├── LoginViewModel.cs          # Login form
│   ├── AccountManagementViewModel.cs # CRUD de cuentas
│   └── CsvGeneratorViewModel.cs   # Generador CSV + subida directa
│
├── Views/
│   ├── MainWindow.xaml      # Shell con sidebar
│   ├── LoginView.xaml       # Pantalla de login
│   ├── CsvGeneratorView.xaml       # DataGrid editable + export/upload
│   └── AccountManagementView.xaml  # DataGrid + filtros + edición
│
└── Helpers/
    ├── DpapiConfigService.cs  # Configuración cifrada (DPAPI)
    └── RelayCommand.cs        # ICommand sync/async
```

---

## Funcionalidades

### 🔐 Autenticación
- URL del PVWA configurable (no hardcoded)
- Login via `POST /API/auth/CyberArk/Logon`
- Configuración persistida cifrada con **DPAPI** (Windows Data Protection API)
- El **usuario** se recuerda; la **contraseña nunca se persiste**

### 🔄 Keep-Alive (sesión indefinida)
- Timer en segundo plano cada N minutos (configurable, por defecto 10)
- Estrategia 1: `POST /API/Auth/ExtendSession` (CyberArk v12+)
- Estrategia 2: Re-autenticación automática si ExtendSession falla
- La sesión dura hasta que el usuario hace **Cerrar Sesión** explícito
- El contador de sesión se muestra en tiempo real en el sidebar

### 📄 Generador de Plantillas CSV
- DataGrid editable con las columnas del formato Bulk Load de CyberArk
- **Exportar CSV** compatible con la herramienta de Bulk Load
- **Importar CSV** para cargar un fichero existente
- **Plantilla Vacía** con cabeceras listas para rellenar en Excel
- **Subida Directa API**: crea las cuentas en CyberArk sin pasar por fichero
  - Barra de progreso en tiempo real
  - Log de éxitos y errores por cuenta

### 🗂️ Gestión de Cuentas
- Búsqueda y filtrado por Safe y keyword
- Paginación automática (sigue `nextLink`)
- **Rotar Password** (`POST /API/Accounts/{id}/Change`)
- **Verificar** (`POST /API/Accounts/{id}/Verify`)
- **Reconciliar** (`POST /API/Accounts/{id}/Reconcile`)
- **Edición** de `address`, `userName`, `platformId` via PATCH
- **Eliminar** con confirmación

---

## Seguridad

| Aspecto | Implementación |
|---|---|
| Contraseña | Solo en memoria (`PasswordBox`), nunca persistida |
| Configuración | Cifrada con DPAPI (solo el mismo usuario Windows puede descifrar) |
| Certificados SSL | Validación estándar por defecto; bypass opcional para labs |
| Token | Almacenado en memoria en `UserSession`, renovado automáticamente |
| Logoff | Llama a `POST /API/Auth/Logoff` + limpia token y password en memoria |

---

## Configuración SSL (Entornos de Laboratorio)

Si el PVWA usa un certificado autofirmado, editar `App.xaml.cs`:

```csharp
// En OnStartup, cambiar:
config.AcceptAllCertificates = true; // Solo para laboratorio
```

> ⚠️ **Nunca activar en producción**

---

## Notas de API

La aplicación usa la **PVWA REST API v2**. Endpoints utilizados:

| Operación | Endpoint |
|---|---|
| Login | `POST /PasswordVault/API/auth/CyberArk/Logon` |
| Extender Sesión | `POST /PasswordVault/API/Auth/ExtendSession` |
| Logoff | `POST /PasswordVault/API/Auth/Logoff` |
| Listar Cuentas | `GET /PasswordVault/API/Accounts` |
| Crear Cuenta | `POST /PasswordVault/API/Accounts` |
| Editar Cuenta | `PATCH /PasswordVault/API/Accounts/{id}` |
| Eliminar Cuenta | `DELETE /PasswordVault/API/Accounts/{id}` |
| Rotar Password | `POST /PasswordVault/API/Accounts/{id}/Change` |
| Verificar | `POST /PasswordVault/API/Accounts/{id}/Verify` |
| Reconciliar | `POST /PasswordVault/API/Accounts/{id}/Reconcile` |
| Listar Safes | `GET /PasswordVault/API/Safes` |
