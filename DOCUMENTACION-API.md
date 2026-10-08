# Documentación funcional — ControlInformes API

> **Documento de referencia obligatorio.** Antes de modificar o agregar código en este
> repositorio, consulta este archivo. Si un cambio agrega, modifica o elimina un endpoint,
> un enum, una validación o una regla de negocio, **actualiza este archivo en el mismo commit**.
>
> Última actualización: 2026-10-06

---

## 1. Convenciones generales

- Todos los endpoints viven bajo `/api/...` y responden `ApiResponse<T>` serializado en camelCase.
- Autenticación: **JWT Bearer**. Todos los controladores llevan `[Authorize]` salvo
  `POST /api/auth/login` (`[AllowAnonymous]`).
- Los controladores son delgados: llaman a un `IBus*` y devuelven `StatusCode(response.HttpCode, response)`.
- Los endpoints que devuelven archivos (`File(...)`) devuelven el `ApiResponse` de error
  solo cuando `HasError`; en caso de éxito devuelven el binario.

### Forma de la respuesta (`ApiResponse<T>`, en `ControlInformes.Utils`)

| Campo | Tipo | Descripción |
|---|---|---|
| `httpCode` | int | Código HTTP con el que se devuelve |
| `result` | T? | Carga útil |
| `hasError` | bool | Indicador de error |
| `mensaje` | string | Mensaje legible |
| `codigoError` | string? | Código de `ErrorCatalog` |
| `errores` | List&lt;string&gt;? | Detalle de validaciones fallidas |

Fábricas: `Ok(data, mensaje, httpCode=200)`, `Fail(mensaje, codigoError, httpCode=400, errores)`,
`NotFound(mensaje, codigoError)` → 404, `Error(mensaje, codigoError)` → 500.
**Nunca construir estas formas a mano.**

### Catálogo de errores (`ErrorCatalog`)

| Código | Significado |
|---|---|
| `ENT_001` | La entidad solicitada no fue encontrada |
| `ENT_002` | Ya existe un registro con los mismos datos |
| `VAL_001` | Se encontraron errores de validación |
| `VAL_002` | El tipo de publicador no es válido |
| `VAL_003` | Las horas solo aplican a precursores |
| `VAL_004` | El publicador ya pertenece a otro grupo |
| `VAL_005` | El capitán asignado no es un publicador válido |
| `EXC_001` | El archivo proporcionado no es válido |
| `EXC_002` | El nombre no puede estar vacío |
| `SYS_001` | Error interno del servidor |
| `AUTH_001` | Credenciales inválidas (401) — definido inline en `BusAuth`, no está en `ErrorCatalog` |
| `AUTH_003` | Usuario inactivo (403) — definido inline en `BusAuth`, no está en `ErrorCatalog` |

---

## 2. Enums del dominio (`ControlInformes.Domain/Enums`)

### `Genero`
| Valor | Nombre |
|---|---|
| 0 | `Hombre` |
| 1 | `Mujer` |

### `CondicionEspiritual`
| Valor | Nombre |
|---|---|
| 0 | `OtrasOvejas` |
| 1 | `Ungido` |

### `RolCongregacion`
| Valor | Nombre |
|---|---|
| 0 | `Ninguno` |
| 1 | `SiervoMinisterial` |
| 2 | `Anciano` |

### `TipoPublicador`
| Valor | Nombre | Reporta horas |
|---|---|---|
| 0 | `Publicador` | No |
| 1 | `NoBautizado` | No |
| 2 | `PrecursorAuxiliar` | Sí |
| 3 | `PrecursorRegular` | Sí |

### `TipoResumenPublicador` (agrupación para la tarjeta resumen en PDF)
| Valor | Nombre | Incluye |
|---|---|---|
| 0 | `Publicador` | `Publicador` + `NoBautizado` |
| 1 | `PrecursorAuxiliar` | `PrecursorAuxiliar` |
| 2 | `PrecursorRegular` | `PrecursorRegular` |

### `TipoReunion`
| Valor | Nombre |
|---|---|
| 0 | `Publica` (fin de semana) |
| 1 | `EntreSemana` |

> `Asistencia.TipoReunion` es **nullable**: `null` representa una fecha registrada sin reunión
> (bitácora / observación).

---

## 3. Endpoints

### 3.1 Auth — `/api/auth`

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/login` | Anónimo | Autentica y devuelve `LoginResponseDto` (`token`, `expiracion`) |

**Body:** `LoginRequestDto { username, password }`

Reglas: usuario inexistente → 404 `ENT_001`; usuario con `Activo = false` → 403 `AUTH_003`;
contraseña incorrecta → 401 `AUTH_001`. El token se genera en `TokenService` con la
configuración `Jwt:Key/Issuer/Audience/ExpireMinutes`.

---

### 3.2 Publicadores — `/api/publicadores`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/publicadores` | Lista todos los publicadores |
| GET | `/api/publicadores/sin-grupo` | Publicadores sin grupo asignado |
| GET | `/api/publicadores/listado` | Listado paginado con filtros (`FiltroPublicadorGrupoDto`) |
| GET | `/api/publicadores/{id}` | Detalle de un publicador |
| GET | `/api/publicadores/{id}/tarjeta` | Datos de la tarjeta (12 meses del año de servicio); `?anoServicio=` opcional |
| GET | `/api/publicadores/{id}/tarjeta/pdf` | Tarjeta del publicador en PDF; `?anoServicio=` opcional |
| GET | `/api/publicadores/grupo/{idGrupo}/tarjetas/pdf` | ZIP con las tarjetas PDF de todo un grupo |
| GET | `/api/publicadores/tarjeta-resumen/pdf` | Tarjeta resumen PDF; requiere `anoServicio` y `tipo` (`TipoResumenPublicador`) |
| POST | `/api/publicadores` | Crea un publicador (`CrearPublicadorDto`) |
| POST | `/api/publicadores/importar-tarjetas` | Importa tarjetas PDF rellenables (multipart `archivos`, `?idGrupo=` opcional) |
| PUT | `/api/publicadores/{id}` | Actualiza (`ActualizarPublicadorDto`); el `id` de ruta debe coincidir con `idPublicador` del body |
| DELETE | `/api/publicadores/{id}` | Elimina el publicador |

**Filtros de `/listado` (`FiltroPublicadorGrupoDto`):** `idGrupo`, `idPublicador`,
`nombreCompleto`, `tipo` (int), `inactivo`, `pagina` (def. 1), `tamanoPagina` (def. 20).

Las rutas fijas (`sin-grupo`, `listado`, `tarjeta-resumen/pdf`) se declaran **antes** de las
rutas con `{id}` para evitar colisiones de enrutamiento; mantén ese orden al agregar nuevas.

---

### 3.3 Grupos — `/api/grupos`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/grupos` | Lista de grupos con capitán y total de miembros |
| GET | `/api/grupos/{id}` | Detalle de un grupo |
| GET | `/api/grupos/{id}/miembros` | Publicadores que pertenecen al grupo |
| POST | `/api/grupos` | Crea un grupo (`CrearGrupoDto { nombre, idCapitan }`) |
| POST | `/api/grupos/asignar-publicadores` | Asigna publicadores (`AsignarPublicadoresDto`) |
| POST | `/api/grupos/quitar-publicadores` | Quita publicadores (`QuitarPublicadoresDto`) |
| PUT | `/api/grupos` | Actualiza (`ActualizarGrupoDto`) |
| DELETE | `/api/grupos/{id}` | Elimina el grupo |

---

### 3.4 Informes mensuales — `/api/informes`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/informes` | Listado paginado (`FiltroInformeDto`) |
| GET | `/api/informes/total` | Resumen del mes (`TotalInformeDto`); requiere `ano` y `mes` |
| GET | `/api/informes/template/{idGrupo}` | Descarga plantilla Excel precargada con el grupo |
| POST | `/api/informes/importar` | Importa Excel; query `ano`, `mes`, `idGrupo` + archivo |
| POST | `/api/informes` | Registra un informe (`RegistrarInformeDto`) — **upsert** |
| PUT | `/api/informes/{id}` | Actualiza (`ActualizarInformeDto`); el `id` de ruta debe coincidir |
| DELETE | `/api/informes/{id}` | Elimina el informe |

**Filtros (`FiltroInformeDto`):** `ano`, `mes`, `idGrupo`, `idPublicador`, `tipo`,
`inactivo`, `pagina` (def. 1), `tamanoPagina` (def. 50).

`TotalInformeDto` incluye promedios de reunión separados por entre semana / fin de semana,
resumen por tipo (publicadores, precursores auxiliares, regulares) y un bloque `pendientes`
con los grupos sin informe y los publicadores sin informe agrupados por grupo.

---

### 3.5 Asistencia — `/api/asistencia`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/asistencia` | Listado paginado (`FiltroAsistenciaDto`) |
| GET | `/api/asistencia/{id}` | Detalle de un registro |
| POST | `/api/asistencia` | Registra asistencia (`RegistrarAsistenciaDto`) → 201 |
| POST | `/api/asistencia/fecha` | Registra solo fecha + observación, sin reunión (`RegistrarFechaDto`) |
| PUT | `/api/asistencia` | Actualiza (`ActualizarAsistenciaDto`) |
| DELETE | `/api/asistencia/{id}` | Elimina el registro |
| GET | `/api/asistencia/tarjeta/pdf` | Tarjeta de reuniones en PDF; requiere `anoServicio1` y `anoServicio2` |
| GET | `/api/asistencia/plantilla/excel` | Descarga la plantilla de asistencia |
| POST | `/api/asistencia/importar/excel` | Importa la plantilla (`archivo`, solo `.xlsx`) |

**Filtros (`FiltroAsistenciaDto`):** `ano`, `mes`, `tipoReunion`, `pagina` (def. 1),
`tamanoPagina` (def. 20).

---

### 3.6 Excel — `/api/excel`

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/excel/importar-informes` | Importa informes; query `ano`, `mes`, `idGrupo` + archivo |
| GET | `/api/excel/template/{idGrupo}` | Plantilla de informes del grupo |
| GET | `/api/excel/listado-publicadores` | Exporta el listado de publicadores |
| GET | `/api/excel/listado-grupos` | Exporta el listado de grupos |

> `POST /api/excel/importar-informes` y `POST /api/informes/importar` cubren el mismo caso de
> uso por vías distintas (`BusExcel` vs `BusInformeMensual`). Al cambiar reglas de importación
> hay que revisar **ambas**.

---

### 3.7 Dashboard — `/api/dashboard`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/dashboard` | Tarjetas y gráficas; requiere `anoServicio`, `mes` opcional |

Las *cards* responden al filtro (`anoServicio` + `mes` opcional); las gráficas
(`distribucionPorTipo`, `historial12Meses`) siempre cubren los 12 meses Sep→Ago del año de
servicio, marcando el mes filtrado con `esMesFiltrado`.

---

### 3.8 Reportes — `/api/reportes`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/reportes/resumen-mensual` | Resumen mensual (`ResumenMensualDto`); requiere `ano` y `mes` |

---

## 4. Reglas de negocio y validaciones

### 4.1 Año de servicio

- El **año de servicio N** abarca **Sep (N-1) → Ago (N)**.
- Cuando `anoServicio` no se envía, se calcula: `mes actual >= 9 ? año + 1 : año`.
- Validación en dashboard: `anoServicio` debe estar entre **2000 y 2100**.
- Orden de meses en tarjetas: Sep, Oct, Nov, Dic (año N-1), Ene…Ago (año N).

### 4.2 Publicadores

- **Rol vs. tipo:** un `NoBautizado` **no puede** tener rol `Anciano` ni `SiervoMinisterial`
  (`ValidarRol`, aplica al crear y al actualizar).
- En `PUT`, el `id` de la ruta debe coincidir con `idPublicador` del body → 400 si no.
- La eliminación es física (`Delete`), sin validación previa de dependencias.
- **Importación de tarjetas PDF:** solo acepta `.pdf`; el archivo debe tener formulario
  (AcroForm) con campos activos; el nombre no puede venir vacío (`EXC_002`); el año de
  servicio debe ser numérico (si no viene, se calcula con la regla 4.1). Hace **upsert por
  nombre completo**: si el publicador existe se actualizan sus datos (las fechas y el grupo
  solo se sobrescriben si el PDF trae valor) y se insertan los informes del año que falten.

### 4.3 Grupos

- El **nombre de grupo es único** (al crear y al actualizar, excluyéndose a sí mismo).
- El **capitán debe existir** como publicador (404 `ENT_001`) y **no puede ser capitán de otro
  grupo** (`VAL_005`).
- **Asignar publicadores:** cada publicador debe existir; si ya pertenece a otro grupo se
  rechaza (`ENT_002`).
- **Quitar publicadores:** el publicador debe pertenecer a ese grupo (`VAL_001`) y **el capitán
  no puede ser removido** del grupo (`VAL_001`).

### 4.4 Informes mensuales

- **Validaciones (`ValidarInforme`):**
  - `ano` entre **2000 y 2100**.
  - `mes` entre **1 y 12**.
  - `cursosBiblicos` **no negativo**.
  - `horas` (si viene) **no negativa**.
- **Limpieza de campos por tipo (`LimpiarCamposPorTipo`)** — se aplica siempre antes de guardar:
  1. Si `inactivo` → `horas = null`, `cursos = 0`.
  2. Si `!participo` → `horas = null`, `cursos = 0`.
  3. Si el tipo es `Publicador` o `NoBautizado` → `horas = null` (los cursos se conservan).
  4. En otro caso (precursores) se conservan horas y cursos.
  > Esta misma lógica está duplicada en `BusInformeMensual` y `BusPublicador`: si cambia, hay
  > que cambiarla en ambos.
- **Upsert por (publicador, año, mes):** `POST /api/informes` no duplica; si ya existe un
  informe para ese publicador y mes, lo actualiza y devuelve su id.
- **Tipo informativo del mes:** `tipoInformativo` permite sobrescribir el tipo solo para ese
  informe, sin cambiar el tipo del publicador.
- **Sincronización de tipo tras importar:** al terminar la importación Excel, el `Tipo` del
  publicador se actualiza al tipo de su informe **más reciente que no sea futuro**
  (comparación por `año * 12 + mes` contra el mes actual).
- La importación Excel exige que el grupo exista; las filas sin nombre se ignoran; el tipo se
  parsea contra `TipoPublicador` (case-insensitive) y una fila inválida se contabiliza como
  fallida con su detalle en `errores`.

### 4.5 Asistencia

- **Validaciones:** `cantidadPresencial` y `cantidadVirtual` **no negativas**.
- `total` es **calculado** (`presencial + virtual`), no se persiste como entrada.
- **Unicidad:** no puede existir más de un registro para la misma **fecha + tipo de reunión**
  (`ENT_002`). Esta validación **solo se ejecuta cuando `tipoReunion` tiene valor**; los
  registros sin tipo (bitácora de fecha) no la aplican — y, por lo mismo, tampoco se validan
  las cantidades negativas en ese caso.
- Al actualizar, el duplicado se detecta excluyendo el propio registro.
- **Importación Excel:** solo `.xlsx`; hace **upsert** por fecha+tipo (o por fecha sin tipo) y
  devuelve `insertados`, `actualizados`, `errores` y `detalles`.

### 4.6 Autenticación

- Token JWT con expiración según `Jwt:ExpireMinutes`.
- Usuario inactivo (`Activo = false`) no puede iniciar sesión.

---

## 5. Cómo mantener este documento

Al implementar un cambio:

1. Si agregas/renombras/eliminas un **endpoint** → actualiza la sección 3.
2. Si agregas/cambias un **enum** o un valor → actualiza la sección 2.
3. Si agregas/cambias una **validación o regla de negocio** → actualiza la sección 4.
4. Si agregas un **código de error** nuevo a `ErrorCatalog` → actualiza la tabla de la sección 1.
5. Actualiza la fecha de "Última actualización" en la cabecera.
