# CyberArk Manager v2.0 — Navaja Suiza PAM

Aplicación WPF (.NET 8) para gestionar **todo** CyberArk PAM SaaS/On-Premise mediante la REST API oficial del PVWA.

---

## Requisitos

- Windows 10/11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 o VS Code con extensión C#

---

## Instalación y Compilación

```powershell
cd CyberArkManager
dotnet restore
dotnet build -c Release
dotnet run
```

---

## Estructura del Proyecto

```
CyberArkManager/
├── Models/
│   └── Models.cs              # Todos los modelos: Account, Safe, User, Platform,
│                              #   PSM Session/Recording, Application, DiscoveredAccount,
│                              #   SystemHealth, AccessRequest, CsvRow, etc.
│
├── Services/
│   ├── AuthService.cs         # Autenticación multi-método + keep-alive + logoff
│   ├── CyberArkApiService.cs  # Wrapper completo REST API v2 (todos los endpoints)
│   └── CsvService.cs          # Generación, exportación e importación CSV Bulk Load
│
├── ViewModels/
│   ├── MainViewModel.cs       # Shell principal + navegación + sesión
│   ├── LoginViewModel.cs      # Formulario de login
│   ├── AccountsViewModel.cs   # CRUD cuentas + ops CPM + selector de campos PATCH
│   └── ViewModels.cs          # SafesVM, UsersVM, PlatformsVM, PsmSessionsVM,
│                              #   PsmRecordingsVM, ApplicationsVM, DiscoveredVM,
│                              #   SystemHealthVM, AccessRequestsVM, CsvGeneratorVM
│
├── Views/
│   ├── MainWindow.xaml        # Shell con sidebar completo de 11 secciones
│   ├── LoginView.xaml
│   ├── AccountsView.xaml
│   ├── SafesView.xaml
│   ├── UsersView.xaml
│   ├── PlatformsView.xaml
│   ├── PsmSessionsView.xaml
│   ├── PsmRecordingsView.xaml
│   ├── ApplicationsView.xaml
│   ├── DiscoveredAccountsView.xaml
│   ├── SystemHealthView.xaml
│   ├── AccessRequestsView.xaml
│   └── CsvGeneratorView.xaml
│
└── Helpers/
    └── Helpers.cs             # DPAPI config, RelayCommand, BaseViewModel, Converters
```

---

## Funcionalidades Completas

### 🔐 Autenticación
| Función | Detalle |
|---|---|
| Login CyberArk nativo | `POST /API/auth/CyberArk/Logon` |
| Login LDAP | `POST /API/auth/LDAP/Logon` |
| Login RADIUS | `POST /API/auth/Radius/Logon` |
| Login Windows Integrated | `POST /API/auth/Windows/Logon` |
| Keep-alive automático | ExtendSession + re-login cada N min (configurable) |
| Logoff explícito | `POST /API/Auth/Logoff` |
| DPAPI | URL y usuario cifrados; contraseña nunca persiste |

---

### 🗂️ Gestión de Cuentas
| Función | Endpoint |
|---|---|
| Listar con filtros y paginación | `GET /API/Accounts` |
| Detalle de cuenta | `GET /API/Accounts/{id}` |
| Crear cuenta | `POST /API/Accounts` |
| **Editar campos específicos (PATCH)** | `PATCH /API/Accounts/{id}` (selector visual de campos) |
| Eliminar | `DELETE /API/Accounts/{id}` |
| Rotar / Change password | `POST /API/Accounts/{id}/Change` |
| Verificar password | `POST /API/Accounts/{id}/Verify` |
| Reconciliar password | `POST /API/Accounts/{id}/Reconcile` |
| Establecer próxima contraseña | `POST /API/Accounts/{id}/SetNextPassword` |
| Recuperar contraseña (Dual Control) | `POST /API/Accounts/{id}/Password/Retrieve` |
| Check-Out | `POST /API/Accounts/{id}/CheckOut` |
| Check-In | `POST /API/Accounts/{id}/CheckIn` |
| Vincular cuenta (Link) | `POST /API/Accounts/{id}/LinkAccount` |
| Log de actividad | `GET /API/Accounts/{id}/Activities` |

**Editor de campos PATCH:** selecciona con checkbox exactamente qué campos modificar (address, userName, platformId, name, automaticManagement, manualReason, remoteMachines...) — sólo se envían los marcados.

---

### 📁 Gestión de Safes
| Función | Endpoint |
|---|---|
| Listar con búsqueda | `GET /API/Safes` |
| Detalle | `GET /API/Safes/{id}` |
| Crear | `POST /API/Safes` |
| Actualizar | `PUT /API/Safes/{id}` |
| Eliminar | `DELETE /API/Safes/{id}` |
| Listar miembros | `GET /API/Safes/{id}/Members` |
| Añadir miembro con permisos | `POST /API/Safes/{id}/Members` |
| Actualizar permisos | `PUT /API/Safes/{id}/Members/{name}` |
| Quitar miembro | `DELETE /API/Safes/{id}/Members/{name}` |

---

### 👤 Usuarios y Grupos
| Función | Endpoint |
|---|---|
| Listar usuarios (filtros por tipo) | `GET /API/Users` |
| Detalle usuario | `GET /API/Users/{id}` |
| Crear usuario | `POST /API/Users` |
| Actualizar usuario | `PUT /API/Users/{id}` |
| Eliminar | `DELETE /API/Users/{id}` |
| Activar | `POST /API/Users/{id}/Activate` |
| Suspender | `POST /API/Users/{id}/Disable` |
| Resetear contraseña | `POST /API/Users/{id}/ResetPassword` |
| Listar grupos | `GET /API/UserGroups` |
| Crear grupo | `POST /API/UserGroups` |
| Eliminar grupo | `DELETE /API/UserGroups/{id}` |
| Añadir usuario a grupo | `POST /API/UserGroups/{id}/Members` |
| Quitar usuario de grupo | `DELETE /API/UserGroups/{id}/Members/{user}` |

---

### ⚙️ Plataformas
| Función | Endpoint |
|---|---|
| Listar (filtrar activas/inactivas) | `GET /API/Platforms` |
| Detalle | `GET /API/Platforms/{id}` |
| Activar | `POST /API/Platforms/{id}/activate` |
| Desactivar | `POST /API/Platforms/{id}/deactivate` |
| Duplicar | `POST /API/Platforms/{id}/duplicate` |
| Exportar ZIP | `POST /API/Platforms/{id}/Export` |
| Importar ZIP | `POST /API/Platforms/Import` |
| Eliminar | `DELETE /API/Platforms/{id}` |

---

### 🖥️ Sesiones y Grabaciones PSM
| Función | Endpoint |
|---|---|
| Sesiones activas | `GET /API/LiveSessions` |
| Historial de sesiones | `GET /API/Sessions` |
| Terminar sesión activa | `DELETE /API/LiveSessions/{id}/Terminate` |
| Listar grabaciones | `GET /API/Recordings` |

---

### 🔌 Aplicaciones AAM/AIM
| Función | Endpoint |
|---|---|
| Listar aplicaciones | `GET /WebServices/PIMServices.svc/Applications` |
| Crear aplicación | `POST /WebServices/PIMServices.svc/Applications` |
| Eliminar | `DELETE /WebServices/PIMServices.svc/Applications/{id}` |
| Listar métodos de auth | `GET /Applications/{id}/Authentications` |
| Añadir método de auth | `POST /Applications/{id}/Authentications` |
| Eliminar método de auth | `DELETE /Applications/{id}/Authentications/{mid}` |

---

### 🔍 Cuentas Descubiertas (Pending)
| Función | Endpoint |
|---|---|
| Listar pendientes | `GET /API/DiscoveredAccounts` |
| Incorporar al vault (Onboard) | `POST /API/DiscoveredAccounts/{id}` |
| Descartar / Eliminar | `DELETE /API/DiscoveredAccounts/{id}` |

---

### ✅ Solicitudes de Acceso (Dual Control)
| Función | Endpoint |
|---|---|
| Listar mis solicitudes | `GET /API/MyRequests` |
| Confirmar / Aprobar | `POST /API/Accounts/{id}/ConfirmCredentialsChange` |
| Rechazar | `POST /API/Accounts/{id}/DenyCredentialsChange` |

---

### 💓 Salud del Sistema
| Función | Endpoint |
|---|---|
| Estado de todos los componentes | `GET /API/ComponentsMonitoringDetails` |
| Estado de componente específico | `GET /API/ComponentsMonitoringDetails/{id}` |

Muestra estado de: **Vault, PVWA, CPM, PSM, PSMP, AIM** con IP, usuario, conectado/desconectado y último logon.

---

### 📄 Generador CSV / Subida Masiva
| Función | Detalle |
|---|---|
| DataGrid editable | Introduce cuentas directamente en la tabla |
| Importar CSV existente | Compatible con formato Bulk Load de CyberArk |
| Exportar CSV | Genera fichero listo para la herramienta nativa |
| Plantilla vacía | Exporta cabeceras listas para rellenar en Excel |
| **Subida Directa API** | Crea todas las cuentas vía API sin pasar por fichero |
| Barra de progreso | Progreso en tiempo real cuenta por cuenta |
| Log de resultado | Muestra ✔ OK y ✖ Error por cada cuenta |

---

## Seguridad

| Aspecto | Implementación |
|---|---|
| Contraseña en memoria | Solo en PasswordBox, nunca en disco |
| Config cifrada | DPAPI (solo el mismo usuario Windows puede descifrar) |
| Certificados SSL | Validación estándar; bypass sólo para laboratorio |
| Token | Renovado automáticamente, limpiado en logoff |
| Logoff real | Llama al endpoint de logoff + limpia headers HTTP |

### SSL para laboratorio (certificado autofirmado)

Editar `App.xaml.cs`:
```csharp
cfg.AcceptAllCertificates = true; // ⚠ SOLO para laboratorio
```

---

## Notas sobre la API

- Compatible con **CyberArk SaaS** y **On-Premise v12+**
- Para versiones anteriores a v12, `ExtendSession` no existe — el sistema re-autentica automáticamente
- Las cuentas de tipo `Windows` pueden necesitar que `PlatformId` incluya el prefijo correcto
- El Dual Control requiere permisos de confirmador en el Safe correspondiente
