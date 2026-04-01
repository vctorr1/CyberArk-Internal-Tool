# Arquitectura técnica

## Resumen

CyberArk Manager es una aplicación de escritorio WPF sobre .NET 8 orientada a la operación de CyberArk PVWA mediante API REST.

La solución sigue una estructura MVVM clásica:

- `Models/`: contratos de datos, DTOs de API y modelos de estado de la UI.
- `Views/`: vistas WPF puras, sin lógica de negocio.
- `ViewModels/`: coordinación de interacción, validación de formularios y comandos.
- `Services/`: acceso a API, autenticación, CSV, almacenamiento local cifrado y diálogos del sistema.
- `Helpers/`: infraestructura compartida como comandos, base observable, DPAPI y convertidores.

## Composición de la aplicación

El arranque se realiza en [App.xaml.cs](/C:/Users/Manoli/Documents/CyberArk/CyberArk-Internal-Tool/App.xaml.cs):

- Carga configuración local cifrada con DPAPI.
- Configura `HttpClient` con compresión y límites de conexiones.
- Inicializa logging global con Serilog.
- Registra el manejo centralizado de excepciones no controladas.
- Construye manualmente los servicios y el `MainViewModel`.

Actualmente la inyección de dependencias es manual, centralizada y explícita en `App.xaml.cs`.

## Capas principales

### Presentación

`MainWindow.xaml` actúa como shell principal. La navegación se resuelve con:

- `MainViewModel.CurrentView`
- `MainViewModel.CurrentContentViewModel`
- `DataTemplate` por tipo de ViewModel

Esto evita acoplar las vistas al árbol visual del `Window` y elimina errores de `DataContext` nulo.

### Autenticación

`AuthService` encapsula:

- Inicio de sesión por CyberArk, LDAP, RADIUS y Windows.
- Sesión local para trabajo offline.
- Renovación automática de token.
- Expiración dura de sesión.
- Limpieza segura del token y de la contraseña en memoria.

La contraseña no se persiste en disco. La URL de PVWA y preferencias locales se almacenan cifradas con DPAPI.

### API REST

`CyberArkApiService` concentra el acceso HTTP a PVWA:

- Construcción de URLs y payloads.
- Retries con Polly para errores transitorios.
- Timeouts por petición.
- Operaciones masivas con paralelismo controlado.
- Búsqueda y enlace de cuentas de logon/reconciliación por servidor.

La clase trabaja sobre el `HttpClient` compartido y usa la sesión actual de `AuthService` como fuente del token.

### CSV y trabajo local

El flujo CSV está repartido en varios servicios:

- `CsvService`: importación, validación, exportación y transformación a payload API.
- `CsvTemplateService`: almacenamiento cifrado de plantillas locales.
- `ProcessedCsvArchiveService`: archivado cifrado de instantáneas procesadas.
- `CsvPreviewService`: apertura de la vista previa en ventana separada.

`CsvGeneratorViewModel` coordina todo el proceso:

- Plantillas por aplicación.
- N cuentas por servidor.
- Importación desde `csv`, `txt` y texto pegado manualmente.
- Vista previa editable.
- Exportación a CSV oficial.
- Subida directa por API.

## Seguridad

### Persistencia local

- La configuración sensible usa DPAPI y queda ligada al usuario de Windows.
- Las plantillas y las instantáneas procesadas se guardan cifradas.
- Los CSV solo se exportan en claro cuando el usuario lo solicita.

### Secretos

- No se hardcodean credenciales.
- La contraseña se mantiene en memoria el tiempo mínimo necesario.
- Los logs evitan registrar tokens y detalles sensibles completos.

### TLS

La aplicación valida certificados TLS por defecto. La opción de aceptar cualquier certificado debe usarse solo en laboratorio.

## Rendimiento

Se han aplicado medidas específicas para mantener buena respuesta en UI y API:

- `DataGrid` con virtualización de filas y columnas.
- Vista previa en ventana independiente para no bloquear la pantalla principal.
- Operaciones masivas con `SemaphoreSlim` y paralelismo limitado.
- `HttpClientHandler` con descompresión automática y `MaxConnectionsPerServer`.
- Trabajo pesado de importación/exportación delegado a servicios asíncronos.

## Estrategia de pruebas

La solución incluye pruebas xUnit en `CyberArkManager.Tests`:

- Validación e importación/exportación CSV.
- Plantillas y snapshots procesados.
- Flujo de login local.
- Pruebas de API sin PVWA real usando `HttpMessageHandler` simulado.

Las pruebas de API verifican:

- Método HTTP.
- URL final.
- JSON enviado.
- Paginación y `nextLink`.

## Evolución recomendada

- Migrar la composición manual a un contenedor DI formal.
- Extraer catálogos de textos visibles a recursos para localización completa.
- Añadir pruebas de ViewModel más finas para navegación y estados de error.
- Incorporar una capa de abstracción para mockear PVWA en pruebas de integración locales.
