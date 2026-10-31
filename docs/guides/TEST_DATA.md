# Guide: filling the database with test data

The **Settings → Test Data** screen creates fake but realistic records (clients with a valid RUT, pets, exams,
exam requests, visits, vaccines, appointments, sales...) so the system can be tried without typing them by hand.

> **Development only.** On a real clinic's server (Production) the screen says it is unavailable and the API answers
> 404: the real database can never be filled with fake data.

## 1. Start the system (from Visual Studio, no terminal)

1. Open `VetManagement.sln` in Visual Studio.
2. In the startup selector (next to the ▶ button), pick the **Web** profile (starts the API and the staff web together;
   the *All* profile also opens the desktop app). If it isn't listed: right-click the solution → *Configure Startup
   Projects* → *Multiple startup projects* → set **VetManagement.Api** and **VetManagement.Staff.Web** to *Start*.
3. Press ▶ (F5). On start-up the API applies the database changes (migrations) by itself.
4. In the browser that opens (https://localhost:7237), log in with your **administrator** user.
   - If you were already logged in, **log out and in again**: that gives your user the test-data permission and the
     menu appears.

## 2. Create the data

Go to **Settings → Test Data**. Each kind of record has a row with:

- **Needs**: what must exist first (green ✔ = exists, red ✖ = missing).
- **In database**: how many there are today.
- A number (100 by default) and the **Create** button.

If you press *Create* and something is missing, the system says what to create first. For example:

| Attempt | Result |
|---|---|
| Create **Exam requests** with nothing | "Exam requests need exam catalog and pets first." |
| Create **Exam catalog** (100) | ✔ 100 exams created |
| Create **Pets** | "Pets need clients first." |
| Create **Clients** (100) | ✔ 100 clients with RUT, phone and email |
| Create **Pets** (100) | ✔ 100 pets, each with an owner |
| Create **Exam requests** (100) | ✔ 100 requests, each for a different pet, with 1–3 exams |

Recommended order (top to bottom on the screen):

1. Clients → 2. Pets → 3. Exam catalog → 4. External labs → 5. Exam requests → 6. Inventory items →
7. Medical visits → 8. Vaccines & deworming → 9. Appointments → 10. Sales

Notes:

- You can press *Create* several times: each time **adds** more records.
- At most 1000 at a time.
- **Appointments** uses the services and vets in *Settings → Agenda Settings*; appointments go from 2 weeks ago to 2 weeks
  ahead (some cancelled or no-shows, so the reports have a bit of everything).
- **Sales** creates sales over the last 30 days (paid, partly paid or open).
- No emails are sent, no stock is deducted and no audit is written: the records are inserted directly for testing.

## 3. Start over (delete everything)

The cleanest way is a database used **only for testing**, dropped whenever you want:

**From Visual Studio:** *View → SQL Server Object Explorer* → `(localdb)\MSSQLLocalDB` → *Databases* → right-click
the VetManagement database → *Delete*. Pressing ▶ again makes the API create it anew (empty, with only users and base
data).

**From a terminal** (optional, see section 4), in the repository folder:

```powershell
cd src/VetManagement.Api
dotnet ef database drop
```

Answer `y` when asked, then start the system again.

> Before dropping, check that the connection string points to your **local test** database and not a real one.

## 4. The bare minimum of terminal (in case you need it)

- Open **PowerShell** (Windows key → type "PowerShell" → Enter), or in Visual Studio *View → Terminal*.
- `cd "path"` enters a folder. Paths with spaces go in quotes.
- To start the system without Visual Studio, open **two** terminals in the repository folder:

  ```powershell
  # Terminal 1: the API
  dotnet run --project src/VetManagement.Api --launch-profile https

  # Terminal 2: the staff web
  dotnet run --project src/VetManagement.Staff.Web --launch-profile https
  ```

  When each one shows `Now listening on: https://...`, open https://localhost:7237.
- To stop something running in a terminal: **Ctrl + C**.
- If a build says "the file is being used by another process", the same project is already running (e.g. from Visual
  Studio): stop that run first.

## 5. For developers: adding a generator

Everything is in `src/VetManagement.Api/TestData/TestDataService.cs`:

1. Add an entry to `Generators` with its key, name, description and what it depends on (`DependsOn`).
2. Add its count in `CountsAsync` and its creation method in the `switch` of `CreateAsync`.
3. The screen shows it by itself. `TestDataTests` checks that every generator works (on SQL Server too).
