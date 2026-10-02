# _wip — código en espera / huérfano

Esta carpeta contiene código que existe en el repositorio pero que **no se usa
en ninguna página actualmente**. Se movió aquí para sacarlo del árbol activo
del sitio sin borrarlo, en caso de que se quiera retomar más adelante.

Está **excluida del build** vía `DefaultItemExcludes` en `SPVetClinic.csproj`,
así que nada de lo que hay acá se compila ni se publica con el sitio.

## Contenido

- `Components/Cards/ServiceCard.razor`, `SpecialistCard.razor` — tarjetas
  reutilizables, sin uso directo (Home y Servicios/Especialistas tienen su
  propio markup de tarjeta).
- `Components/Sections/ServicesSection.razor`, `SpecialistsSection.razor` —
  secciones que usan las tarjetas de arriba; no están referenciadas por
  ninguna página.
- `Components/Booking/*.razor` (BookingForm, BookingSummary, PaymentStep,
  SpecialistSelector, TimeSlotCalendar) — módulo de reserva de horas, UI
  construida pero sin flujo activado desde ninguna página.
- `Services/Booking/*.cs` (BookingStateService, BookingMockDataService) —
  servicios de soporte del módulo de booking; nunca se registraron en
  `Program.cs`.
- `Data/Models/Booking/*.cs` — modelos de datos del módulo de booking.
- `wwwroot/css/booking.css` — estilos del módulo de booking; no estaba
  enlazado desde ningún `.html`.

Todos los `.razor` conservan su `@namespace` original (agregado explícitamente
al moverlos) para que, si se reactivan, no haga falta tocar ninguna
referencia.

## Para reactivar el módulo de booking

1. Mover los archivos de vuelta a sus carpetas originales (o quitar la
   exclusión `_wip/**` del `.csproj` y ajustar rutas).
2. Registrar `BookingStateService` y `BookingMockDataService` en el
   contenedor de DI (`Program.cs`) — no estaba hecho ni siquiera cuando el
   módulo vivía en su ubicación original.
3. Enlazar `booking.css` desde `index.html` si se vuelve a usar.
