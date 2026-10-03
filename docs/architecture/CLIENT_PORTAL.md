# Client portal ("Mis mascotas") — design

Part of the hosted booking portal (`VetManagement.Booking.Web`): `/mis-mascotas`. Clients see their pets'
vaccines and deworming (with next due date and status), past visits and upcoming appointments, without
creating an account.

## Access by emailed link

1. The client enters their RUT (`POST api/public/portal/link`, rate limit `public-write`).
2. If the RUT belongs to a client with an email, the API emails a link
   `{BookingPortal:BaseUrl}/mis-mascotas/{token}`. The answer is **202 either way**, so nobody can find out
   which RUTs are registered. At most one link per client every 2 minutes.
3. The link (`GET api/public/portal/{token}`, rate limit `public-read`) works until it expires.

The token is 32 random bytes (base64url); only its SHA-256 is stored (`ClientAccessTokens`), so a database
leak doesn't give usable links.

## Settings (clinical settings → `ClientPortal`, admin)

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `false` | Off until the clinic offers it; the portal header shows "Mis mascotas" only when on |
| `LinkValidMinutes` | `60` | Link lifetime (5–1440) |
| `ShowVisitDetails` | `true` | Show diagnosis and treatment of past visits (otherwise date, reason and weight only) |

Anamnesis and physical examination notes are never shown to the owner. Replaced (superseded) doses
are hidden. Upcoming appointments booked online link to their booking page (view/cancel).

## Limits

The RUT must match the client's stored RUT in normalized form (`12345678-5`); clients typed by staff in
another format aren't found (same rule as online booking). Requires SMTP to be configured.
