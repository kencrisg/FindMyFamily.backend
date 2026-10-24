# FindMyFamily.Tests

Pruebas automatizadas del backend. **143 tests**, ~1 s de ejecución.

```bash
# Todos
dotnet test FindMyFamily.Tests

# Una clase o un test concreto
dotnet test FindMyFamily.Tests --filter FamilyServiceTests
dotnet test FindMyFamily.Tests --filter RemoveMember_SiEraSuUnicaFamilia_BorraLaCuentaLogicamente

# Toda la solución
dotnet test
```

No necesitan Docker, ni PostgreSQL, ni la API levantada.

## Cómo están montados

Los tests ejercitan los **servicios de dominio reales sobre los repositorios y el
`ApplicationDbContext` reales**, con una base **SQLite en memoria** por test
(`ServiceHarness`). Solo se sustituyen las fronteras externas: Firebase, SignalR, el hasher
de contraseñas y el generador de JWT (`Infrastructure/Fakes.cs`).

Dos decisiones que conviene conocer antes de añadir tests:

- **SQLite y no el proveedor InMemory.** El código usa `ExecuteUpdateAsync` /
  `ExecuteDeleteAsync` (revocación de tokens, borrado de pertenencias) y transacciones, que
  InMemory no sabe ejecutar. Con repositorios falsos se quedaría sin cubrir justo lo más
  delicado: el filtro global de borrado lógico, las cascadas y la atomicidad.
- **El hasher real de BCrypt no se usa en los tests de servicio.** Con factor de trabajo 12
  cada hash cuesta ~250 ms. BCrypt se prueba aparte, en `Common/BCryptPasswordHasherTests`.

### Trampa a tener en cuenta

`ExecuteUpdateAsync` / `ExecuteDeleteAsync` van **directas al motor** y no actualizan las
entidades ya rastreadas. Al comprobar sus efectos hay que releer desde la base de datos con
`h.ReloadDeviceAsync(...)`, `h.ReloadDevicesOfUserAsync(...)` o
`h.ReloadUserIncludingDeletedAsync(...)`: una consulta normal devolvería el valor viejo que
quedó en memoria. Un test escrito sin esto da un falso fallo.

Además, cada test comparte **un único `DbContext`** entre operaciones, mientras que en una
petición HTTP real cada una tiene el suyo. Donde eso cambia el resultado está anotado en el
propio test.

## Qué cubre

| Archivo | Foco |
| :--- | :--- |
| `Common/PasswordPolicyTests` | Longitud y complejidad mínimas, y sus códigos de error |
| `Common/PhoneNumberTests` | Normalización del teléfono (identidad de login) y formatos inválidos |
| `Common/BCryptPasswordHasherTests` | Hash con salt, verificación, hash corrupto sin reventar |
| `Services/AuthServiceTests` | Login, rotación de refresh tokens, logout, hash del token en BD, sesiones separadas por dispositivo, cuenta borrada |
| `Services/FamilyServiceTests` | **Matriz de permisos** del grupo familiar y **semántica de eliminación** (lógica vs desvinculación) |
| `Services/UserServiceTests` | Operaciones de Super Admin, papelera y restauración, revocación de sesiones al cambiar contraseña, paginación acotada |
| `Services/LocationServiceTests` | Flujo On-Demand, degradación a `UNAVAILABLE` con última ubicación, y **a quién se difunde** cada coordenada |
| `Services/DeviceServiceTests` | Alta del token FCM y desvinculación, con sus comprobaciones de propiedad |

Las zonas de mayor riesgo son `FamilyServiceTests` (un fallo expone la ubicación de una
persona a quien no debe verla, o deja activa una cuenta que debería estar cerrada) y la
sección de difusión de `LocationServiceTests` (una coordenada no debe salir de la familia que
la pidió).

## Lo que NO cubren

- **Los controladores y la tubería HTTP**: atributos `[Authorize]`, rate limiting, el
  middleware de excepciones y el mapeo a códigos de estado. Se han validado a mano
  (`docs/GUIA_PRUEBAS_SWAGGER.md`); faltan tests de integración con `WebApplicationFactory`.
- **PostgreSQL concreto**: SQLite no reproduce sus tipos ni sus errores. Un `LIMIT` negativo
  revienta en PostgreSQL y no en SQLite, así que los tests comprueban que el **servicio**
  acota el valor, no que el motor lo rechace.
- **SignalR y Firebase reales**: son dobles de prueba.
- **La app móvil**: no tiene tests.
