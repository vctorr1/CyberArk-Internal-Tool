# CyberArk Manager

Aplicación WPF en .NET 8 para operar CyberArk PVWA mediante API REST, con soporte de trabajo local, generador CSV, plantillas cifradas y operaciones masivas.

## Estado actual

El proyecto compila y puede ejecutarse en:

- `modo local`, sin conexión a PVWA
- `modo conectado`, con sesión real en PVWA

## Funcionalidades principales

- Inicio de sesión CyberArk, LDAP, RADIUS y Windows.
- Modo local para trabajar sin acceso a PVWA.
- Gestión de cuentas, safes, usuarios, plataformas, sesiones PSM, aplicaciones y salud del sistema.
- Generador CSV con importación desde `csv`, `txt` o texto pegado.
- Plantillas de aplicación y snapshots procesados almacenados de forma cifrada.
- Vista previa en ventana independiente.
- Subida masiva por API.
- Enlace masivo de cuentas de logon o reconciliación por servidor.

## Requisitos

- Windows 10 u 11
- .NET 8 SDK

## Compilación

```powershell
dotnet build .\CyberArkManager.csproj
```

## Ejecución

```powershell
dotnet run --project .\CyberArkManager.csproj
```

## Pruebas

```powershell
dotnet test .\CyberArkManager.Tests\CyberArkManager.Tests.csproj
```

## Probar la API sin PVWA

Sí. La solución incluye pruebas unitarias de la capa API con `HttpMessageHandler` simulado, por lo que se puede validar:

- la URL generada
- el verbo HTTP
- el JSON enviado
- la paginación

Consulta [PRUEBAS_API_SIN_PVWA](docs/PRUEBAS_API_SIN_PVWA.md).

## Seguridad

- La URL de PVWA y preferencias locales se guardan cifradas con DPAPI.
- Las plantillas y snapshots CSV locales se almacenan cifrados.
- Las contraseñas no se persisten en texto plano.
- Los logs evitan exponer secretos sensibles.

## Documentación

- [Arquitectura técnica](docs/ARQUITECTURA_TECNICA.md)
- [Guía de uso](docs/GUIA_DE_USO.md)
- [Pruebas API sin PVWA](docs/PRUEBAS_API_SIN_PVWA.md)
