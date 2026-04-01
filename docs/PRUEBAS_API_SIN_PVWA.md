# Pruebas de API sin acceso a PVWA

## Sí, se puede probar

Aunque no tengas acceso a un PVWA real, sí es posible validar una parte importante del comportamiento de la capa API.

La estrategia aplicada en este proyecto es usar pruebas unitarias con un `HttpMessageHandler` simulado.

## Qué se valida

Las pruebas actuales cubren:

- construcción correcta de URLs
- método HTTP esperado
- cuerpo JSON enviado
- paginación con `nextLink`
- enlace de cuentas con el payload correcto

Archivo principal:

- `CyberArkManager.Tests/CyberArkApiServiceTests.cs`

## Cómo funciona

Las pruebas:

1. crean un `HttpClient` falso
2. inyectan respuestas simuladas
3. fuerzan una sesión válida en `AuthService`
4. ejecutan métodos reales de `CyberArkApiService`
5. comprueban la petición generada

Esto permite detectar errores de integración antes de disponer de entorno real.

## Ejecución

```powershell
dotnet test .\CyberArkManager.Tests\CyberArkManager.Tests.csproj
```

## Alcance real

Estas pruebas no sustituyen por completo una validación contra PVWA, porque no comprueban:

- permisos reales
- comportamiento exacto de la versión instalada
- reglas de plataforma
- políticas CPM/PSM del entorno
- certificados o conectividad real

Pero sí sirven para validar la mayor parte de la lógica cliente y reducir errores antes de conectarse al entorno corporativo.

## Recomendación práctica

Usa este orden:

1. pruebas unitarias sin PVWA
2. validación manual en modo local
3. prueba controlada contra PVWA cuando tengas acceso

Con eso separas los fallos de interfaz y payload de los problemas propios del entorno CyberArk.
