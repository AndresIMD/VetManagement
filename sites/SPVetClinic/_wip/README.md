# _wip — parked / orphaned code

> **Replaced:** booking is now the product's own portal (`src/VetManagement.Booking.Web`), which the site links to
> through `BookingUrl` (see `docs/DEPLOYMENT.md`). The booking module in this folder won't be revived; it can be
> deleted at any time (git keeps the history).

This folder holds code that exists in the repository but **isn't used by any page**. It was moved here to take it out
of the site's active tree without deleting it.

It is **excluded from the build** through `DefaultItemExcludes` in `SPVetClinic.csproj`, so nothing here is compiled or
published with the site.

## Contents

- `Components/Cards/ServiceCard.razor`, `SpecialistCard.razor` — reusable cards with no direct use (Home and
  Services/Specialists have their own card markup).
- `Components/Sections/ServicesSection.razor`, `SpecialistsSection.razor` — sections using the cards above; no page
  references them.
- `Components/Booking/*.razor` (BookingForm, BookingSummary, PaymentStep, SpecialistSelector, TimeSlotCalendar) —
  booking UI, built but never wired to a page.
- `Services/Booking/*.cs` (BookingStateService, BookingMockDataService) — services for the booking module; never
  registered in `Program.cs`.
- `Data/Models/Booking/*.cs` — booking data models.
- `wwwroot/css/booking.css` — booking styles; not linked from any `.html`.

Every `.razor` keeps its original `@namespace` (added explicitly when moved), so reviving one needs no reference
changes.

## Reviving the booking module

1. Move the files back to their original folders (or remove the `_wip/**` exclusion from the `.csproj` and fix paths).
2. Register `BookingStateService` and `BookingMockDataService` in DI (`Program.cs`) — never done, not even when the
   module lived in its original place.
3. Link `booking.css` from `index.html` if it is used again.
