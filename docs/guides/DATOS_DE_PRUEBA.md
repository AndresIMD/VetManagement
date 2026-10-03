# Guía: llenar la base de datos con datos de prueba

La pantalla **Administration → Test Data** crea datos falsos pero realistas (clientes con RUT válido, mascotas,
exámenes, solicitudes, consultas, vacunas, citas, ventas...) para probar el sistema sin escribirlos a mano.

> **Solo funciona en desarrollo.** En el servidor de una clínica real (Production) la pantalla dice que no está
> disponible y la API responde 404: es imposible llenar la base real con datos falsos.

## 1. Iniciar el sistema (desde Visual Studio, sin terminal)

1. Abre `VetManagement.sln` en Visual Studio.
2. Arriba, en el selector de inicio (al lado del botón ▶), elige el perfil **Web** (inicia la API y la web del
   personal juntas; el perfil *All* además abre la app de escritorio). Si no aparece: clic derecho en la solución →
   *Configurar proyectos de inicio* → *Varios proyectos de inicio* → marca **VetManagement.Api** y
   **VetManagement.Staff.Web** como *Iniciar*.
3. Presiona ▶ (F5). Al arrancar, la API aplica sola los cambios de base de datos (migraciones).
4. En el navegador que se abre (https://localhost:7237) inicia sesión con tu usuario **administrador**.
   - Si ya tenías la sesión abierta de antes, **cierra sesión y vuelve a entrar**: así tu usuario recibe el permiso
     nuevo de datos de prueba y aparece el menú.

## 2. Crear los datos

Ve a **Administration → Test Data**. Verás una fila por tipo de dato con:

- **Needs**: lo que debe existir antes (✔ verde = ya existe, ✖ rojo = falta).
- **In database**: cuántos hay hoy.
- Un número (por defecto 100) y el botón **Create**.

Si presionas *Create* y falta algo, el sistema te dice qué crear primero. Por ejemplo:

| Intento | Resultado |
|---|---|
| Crear **Exam requests** sin nada | "Exam requests need exam catalog and pets first." |
| Crear **Exam catalog** (100) | ✔ se crean 100 exámenes |
| Crear **Pets** | "Pets need clients first." |
| Crear **Clients** (100) | ✔ 100 clientes con RUT, teléfono y correo |
| Crear **Pets** (100) | ✔ 100 mascotas, cada una de un cliente |
| Crear **Exam requests** (100) | ✔ 100 solicitudes, cada una para una mascota distinta, con 1–3 exámenes |

Orden recomendado (de arriba hacia abajo en la pantalla):

1. Clients → 2. Pets → 3. Exam catalog → 4. External labs → 5. Exam requests → 6. Inventory items →
7. Medical visits → 8. Vaccines & deworming → 9. Appointments → 10. Sales

Notas:

- Puedes presionar *Create* varias veces: cada vez **agrega** más registros.
- Máximo 1000 por vez.
- **Appointments** usa los servicios y veterinarios de *Agenda → Settings*; las citas van desde hace 2 semanas
  hasta 2 semanas adelante (algunas canceladas o inasistencias, para que los reportes tengan de todo).
- **Sales** crea ventas de los últimos 30 días (pagadas, parcialmente pagadas o abiertas).
- No se envían correos, no se descuenta stock y no se escribe auditoría: son datos directos para pruebas.

## 3. Empezar de cero (borrar todo)

La forma más limpia es usar una base de datos **solo para pruebas** y borrarla cuando quieras:

**Desde Visual Studio:** menú *Ver → Explorador de objetos de SQL Server* → `(localdb)\MSSQLLocalDB` → *Bases de
datos* → clic derecho en la base de VetManagement → *Eliminar*. Al volver a presionar ▶ la API la crea de nuevo
(vacía, solo con usuarios y datos base).

**Desde la terminal** (opcional, ver la sección 4):

```powershell
cd "C:\Users\bubit\Documents\Projects\Web Projects\VetManagement\src\VetManagement.Api"
dotnet ef database drop
```

Responde `y` cuando pregunte. Luego inicia el sistema otra vez.

> Antes de borrar, revisa que la cadena de conexión apunte a tu base **local de pruebas** y no a una real.

## 4. Lo mínimo de terminal (por si lo necesitas)

- Abre **PowerShell** (tecla Windows → escribe "PowerShell" → Enter), o en Visual Studio *Ver → Terminal*.
- `cd "ruta"` entra a una carpeta. Las rutas con espacios van entre comillas.
- Para iniciar el sistema sin Visual Studio, abre **dos** terminales:

  ```powershell
  # Terminal 1: la API
  cd "C:\Users\bubit\Documents\Projects\Web Projects\VetManagement"
  dotnet run --project src/VetManagement.Api --launch-profile https

  # Terminal 2: la web del personal
  cd "C:\Users\bubit\Documents\Projects\Web Projects\VetManagement"
  dotnet run --project src/VetManagement.Staff.Web --launch-profile https
  ```

  Cuando cada una muestre `Now listening on: https://...`, abre https://localhost:7237.
- Para detener algo que corre en la terminal: **Ctrl + C**.
- Si al compilar aparece "el archivo está siendo usado por otro proceso", es porque el mismo proyecto ya se está
  ejecutando (por ejemplo, desde Visual Studio): detén esa ejecución primero.

## 5. Para quien programa: agregar un generador nuevo

Todo está en `src/VetManagement.Api/TestData/TestDataService.cs`:

1. Agrega una entrada a `Generators` con su clave, nombre, descripción y de qué depende (`DependsOn`).
2. Agrega su conteo en `CountsAsync` y su método de creación en el `switch` de `CreateAsync`.
3. La pantalla lo muestra sola. `TestDataTests` verifica que todos los generadores funcionen (también en SQL Server).
