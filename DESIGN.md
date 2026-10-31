# Design Direction

What VetManagement should feel like and what to protect when building a screen. Code style lives in
[`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md), structure in
[`docs/architecture/ARCHITECTURE.md`](docs/architecture/ARCHITECTURE.md).

Everything here is the owner's decision except what is marked **Proposed**. **Pending** items wait for a decision.

## Positioning

The market offers comfortable CRMs with few features, and feature-packed CRMs that make daily work more cumbersome and
less productive than simpler products. VetManagement aims at the gap: a veterinary CRM with robust, professional tools,
focused on usefulness and, above all, on a comfortable user experience.

The feature-packed ones go wrong in three ways:

- Too many options.
- Functions used on rare occasions are as visible as those used all day.
- Setup before a module can be switched on is so complex that companies use only some modules and avoid the rest,
  because those don't help the company move faster.

## Product context

| Topic | Direction | More in |
|---|---|---|
| Product | One generic, flexible CRM sold to many clinics. What is specific to a clinic stays in its private repository or its deployment settings. | [`PROJECT_STATE.md`](PROJECT_STATE.md) |
| Modules | Each area is an independent module. Clinics pay monthly for the mix they need; the fee also covers maintenance and support for incidents. | [`ARCHITECTURE.md`](docs/architecture/ARCHITECTURE.md#module-pattern) |
| Deployment | Cloud is what most clinics will rely on. Large organisations such as hospitals may need a private server. A mixed plan is a possible add-on, chosen with the plan: a private server set up in advance keeps the app working normally during a serious internet incident, and the cloud is updated once it is over. Production today runs on a clinic's local network. | [`DEPLOYMENT.md`](docs/DEPLOYMENT.md) |
| Platforms | Windows at the initial launch, other platforms later. The final goal includes native apps for Windows, iOS and Android. The same screens inside MAUI are accepted for now, to be reviewed at the testing stage. | |
| Real use | A pilot clinic runs it in production: its unmet needs and feedback guide the work. | |

## Design rules

1. **Visibility follows use.** The areas used most come first: medical visits, payments, exams and inventory. Rare
   functions stay reachable but never get the same weight.
2. **A module works the day it is enabled.** Complex setup must not stand between a clinic and a module. Proposed:
   defaults cover the start (per-clinic settings [already begin from them](docs/architecture/ARCHITECTURE.md#shared-mechanisms))
   and guided forms replace the [JSON editors](PROJECT_STATE.md#open-product-decisions).
3. **Spacious and calm.** Screens leave room and never feel like a packed forum. Proposed: the room comes from showing
   fewer things at once, not only from padding.
4. **Modules a clinic doesn't have are hidden.** Proposed: menus and the home page still look complete with any mix.
5. **Desktop leads.** Web on a desktop and the Windows MAUI app carry the most weight in every decision. Other devices
   adapt to them, never the other way round. Phone and tablet layouts must not break, but they don't hold
   back a desktop delivery until the device-testing stage.
6. **A feature must not slow daily work** (Proposed). A new screen is judged by what its users have to learn, and
   configuration sits behind the daily work.
7. **The connection is never a surprise** (Proposed). The app shows whether it reaches the server, says when it is
   working locally and will update the cloud later, and each screen has a defined behaviour when it can't connect.
8. **Clinic branding has fixed slots** (Proposed). A clinic's logo, colour and report design fill defined places and
   never change how a screen works.

## Visual direction

| Field | Direction | Today |
|---|---|---|
| Identity | Not defined: name, brand and marketing come later, with a graphic designer. "VetManagement" is the working name. | No product logo; the booking portal uses the clinic's `PrimaryColor` |
| Personality | Effective (efficacy plus efficiency), calm, professional, clean | |
| Mood | Visual cleanliness, because this is medical software | |
| Reference | Stripe, for looking minimal yet organized: its hierarchy and white space, not its gradients or motion | |
| Density | Spacious | `Dense="true"` in 46 `Staff.UI` files |
| Palette | Pending, with the brand. Today's values stay as neutral stand-ins; no restyling until then. | MudBlazor's stock blue `#1976d2` on `#f5f7fa`, plus a softened dark palette (`AppTheme.cs`) |
| Typography | Pending, with the brand. Today's fonts stay as neutral stand-ins; no restyling until then. | Helvetica Neue, Helvetica, Arial (`app.css`); Inter, Figtree and Jost ship for reports |

Dial: ENERGY 1 / RHYTHM 1 / MOTION 1
