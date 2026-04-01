# CyberArk Manager

Aplicación de escritorio para Windows, desarrollada en WPF sobre .NET 8, orientada a la operación técnica de entornos CyberArk PVWA mediante API REST.

El proyecto centraliza en una única herramienta tareas habituales de administración, carga masiva y preparación offline, manteniendo una arquitectura MVVM, separación por servicios y almacenamiento local protegido para la configuración sensible.

## Alcance funcional

CyberArk Manager cubre actualmente las siguientes áreas:

- Autenticación contra PVWA por CyberArk, LDAP, RADIUS y Windows.
- Modo local para trabajo offline sin acceso al entorno corporativo.
- Gestión de cuentas, safes, usuarios, plataformas, sesiones PSM y aplicaciones.
- Generación e importación de CSV para altas masivas.
- Plantillas reutilizables y snapshots locales cifrados.
- Subida masiva por API y operaciones auxiliares de enlace de cuentas.

## Principios del proyecto

- Arquitectura MVVM con separación clara entre vistas, lógica de presentación y servicios.
- Integración con CyberArk exclusivamente mediante API REST.
- Persistencia local protegida con DPAPI para datos sensibles.
- Código preparado para operación técnica y evolución incremental.

## Requisitos

- Windows 10 o Windows 11 de 64 bits.
- .NET 8 SDK para compilación desde código fuente.

## Ejecución desde código fuente

```powershell
dotnet build .\CyberArkManager.csproj
dotnet run --project .\CyberArkManager.csproj
```

## Pruebas

```powershell
dotnet test .\CyberArkManager.Tests\CyberArkManager.Tests.csproj
```

La solución incluye pruebas unitarias para:

- lógica de CSV
- flujo local de la aplicación
- capa API sin necesidad de acceso real a PVWA

## Publicación

La aplicación puede publicarse como ejecutable único para Windows mediante `dotnet publish` en modo `Release`, con despliegue autocontenido.

## Seguridad

- La URL de PVWA y la configuración local se almacenan cifradas.
- Las plantillas y snapshots procesados se guardan protegidos en el perfil del usuario.
- Las credenciales no se persisten en texto plano.
- Los logs evitan exponer información sensible.

## Documentación

- [Arquitectura técnica](docs/ARQUITECTURA_TECNICA.md)
- [Guía de uso](docs/GUIA_DE_USO.md)
- [Pruebas API sin PVWA](docs/PRUEBAS_API_SIN_PVWA.md)

## Estado de entrega

El repositorio mantiene una versión funcional compilable y preparada para distribución como ejecutable único en Windows.
