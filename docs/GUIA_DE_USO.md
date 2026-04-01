# Guía de uso

## 1. Inicio de la aplicación

La pantalla inicial permite dos modos de trabajo:

- `Modo local`: abre la interfaz sin conexión a PVWA.
- `PVWA`: usa CyberArk, LDAP, RADIUS o Windows para iniciar sesión real.

En modo local puedes:

- Preparar plantillas CSV.
- Importar servidores.
- Generar la vista previa.
- Exportar CSV.
- Guardar plantillas e instantáneas locales.

No podrás:

- Subir cuentas por API.
- Enlazar cuentas de logon o reconciliación por API.

## 2. Generador CSV

El generador CSV está pensado para altas masivas.

### Plantilla de aplicación

La plantilla define la estructura base que se replicará para cada servidor:

- `safe`
- `platformID`
- `username`
- `password`
- `EnableAutoMgmt`
- `ManualMgmtReason`
- `UseSudoOnReconcile`

Puedes:

- Crear una plantilla nueva.
- Guardarla de forma cifrada.
- Cargarla más tarde.
- Eliminarla.

### Entradas soportadas

Se pueden cargar servidores de tres formas:

- Importando un `CSV`.
- Importando un `TXT`.
- Pegando manualmente una dirección por línea.

Cada servidor importado se expande según el número de cuentas configurado en la plantilla.

### Vista previa

La vista previa inferior muestra el resultado final antes de exportar o subir.

También puedes abrirla en una ventana separada para revisar mejor:

- filas generadas
- columnas oficiales
- estado por fila

### Exportación

El CSV exportado sigue el formato oficial:

`username,address,safe,platformID,password,EnableAutoMgmt,ManualMgmtReason`

`ManualMgmtReason` solo tiene sentido cuando `EnableAutoMgmt` está desactivado.

Si `UseSudoOnReconcile` está activado para cuentas UNIX, se incorpora en el payload API y en la vista previa ampliada.

### Instantáneas procesadas

Puedes guardar una instantánea del trabajo actual:

- se almacena cifrada en local
- se puede recargar en otra sesión
- se puede reexportar a CSV plano

## 3. Subida por API

Cuando accedes con sesión real a PVWA, el generador activa:

- `Subir por API`
- `Enlace de cuentas por API`

### Subida masiva

La subida crea las cuentas directamente en CyberArk usando la API REST, sin depender de la herramienta CSV nativa.

### Enlace de cuentas

La sección de enlace permite:

- pegar una o varias direcciones de servidor
- indicar el nombre común de la cuenta enlazada
- elegir si es una cuenta de `logon` o `reconciliación`

La aplicación busca en cada servidor la cuenta indicada y la enlaza al resto de cuentas con la misma dirección.

## 4. Modo local recomendado

Cuando no tienes acceso a PVWA:

1. Entra en `Modo local`.
2. Crea o carga una plantilla.
3. Importa o pega los servidores.
4. Revisa la vista previa.
5. Guarda una instantánea cifrada.
6. Exporta el CSV si lo necesitas.

Cuando vuelvas a tener acceso:

1. Inicia sesión normal.
2. Carga la instantánea procesada.
3. Revisa la vista previa.
4. Sube por API o exporta el CSV final.

## 5. Recomendaciones de uso

- Mantén una plantilla por tipo de plataforma o servicio.
- No guardes contraseñas reales si no son necesarias para la preparación inicial.
- Usa la vista previa en ventana aparte antes de exportar o subir.
- Revisa los mensajes de estado al pie de la ventana para detectar validaciones pendientes.
