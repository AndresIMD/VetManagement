using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Billing;
using VetManagement.Application.Clinical;
using VetManagement.Application.Scheduling;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Clinical;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Medical;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Api.TestData;

/// <summary>A kind of sample data and what must exist before it can be created.</summary>
public sealed record TestDataGenerator(string Key, string Name, string Description, IReadOnlyList<string> DependsOn);

public sealed record TestDataStatus(TestDataGenerator Generator, int Existing, IReadOnlyList<string> Missing);

public sealed record TestDataResult(int Created, string? Error = null);

/// <summary>
/// Fills a development database with realistic sample data, one kind at a time and in dependency order
/// (e.g. exam requests need exams and pets; pets need clients). Never available in Production (see TestDataController).
/// Writes straight to the database: the data is valid for the app, but no emails, audit entries or stock movements are made.
/// </summary>
public class TestDataService(AppDbContext db, SchedulingSettingsService schedulingSettings, BillingSettingsService billingSettings,
    ClinicalSettingsService clinicalSettings)
{
    public const int MaxCount = 1000;

    public static readonly IReadOnlyList<TestDataGenerator> Generators =
    [
        new("clients", "Clients", "Owners with a valid RUT, phone and email.", []),
        new("pets", "Pets", "Dogs and cats, each assigned to an existing client.", ["clients"]),
        new("exams", "Exam catalog", "Lab exams with sample type, machine and prices.", []),
        new("external-labs", "External labs", "Laboratories that receive outsourced exams.", []),
        new("exam-requests", "Exam requests", "1–3 exams per request, each for a different pet; some sent to external labs.", ["exams", "pets"]),
        new("inventory", "Inventory items", "Materials with stock, cost and sale price.", []),
        new("visits", "Medical visits", "Visits with reason, diagnosis, treatment, weight; some with exam procedures.", ["pets"]),
        new("doses", "Vaccines & deworming", "Doses from the clinic's protocols over the last year (some due soon or overdue).", ["pets"]),
        new("appointments", "Appointments", "Agenda appointments from 2 weeks ago to 2 weeks ahead (some cancelled or no-show).", ["pets"]),
        new("sales", "Sales", "Sales of the last 30 days with services and items, paid, partly paid or open.", ["clients"]),
    ];

    private static readonly string[] FirstNames = ["Camila", "Martina", "Sofía", "Valentina", "Isidora", "Catalina", "Antonia", "Florencia", "Josefa", "Javiera",
        "Benjamín", "Vicente", "Martín", "Matías", "Joaquín", "Agustín", "Tomás", "Lucas", "Felipe", "Diego", "Constanza", "Fernanda", "Ignacio", "Sebastián"];
    private static readonly string[] LastNames = ["González", "Muñoz", "Rojas", "Díaz", "Pérez", "Soto", "Contreras", "Silva", "Martínez", "Sepúlveda",
        "Morales", "Rodríguez", "López", "Fuentes", "Hernández", "Torres", "Araya", "Flores", "Espinoza", "Valenzuela", "Castillo", "Tapia", "Reyes", "Gutiérrez"];
    private static readonly string[] Streets = ["Av. Libertador B. O'Higgins", "Calle Astorga", "Av. Cachapoal", "Pasaje Los Aromos", "Av. Kennedy",
        "Calle Brasil", "Av. San Martín", "Calle Estado", "Pasaje Las Rosas", "Av. República"];
    private static readonly string[] PetNames = ["Luna", "Max", "Rocky", "Nala", "Simba", "Toby", "Kira", "Coco", "Milo", "Lola", "Bruno", "Maya",
        "Thor", "Mía", "Zeus", "Canela", "Oliver", "Pelusa", "Chester", "Frida", "Bobby", "Kiara", "Tomy", "Princesa"];
    private static readonly string[] DogBreeds = ["Mestizo", "Labrador Retriever", "Golden Retriever", "Poodle", "Bulldog Francés", "Pastor Alemán",
        "Beagle", "Schnauzer", "Yorkshire Terrier", "Border Collie", "Dachshund", "Shih Tzu"];
    private static readonly string[] CatBreeds = ["Mestizo", "Siamés", "Persa", "Maine Coon", "Ragdoll", "Bengalí", "British Shorthair"];

    private static readonly (string Name, SampleType Sample, SampleContainer Container, int Price)[] ExamTypes =
    [
        ("Hemograma", SampleType.Blood, SampleContainer.Tube, 18000), ("Perfil bioquímico", SampleType.Blood, SampleContainer.Tube, 35000),
        ("Perfil renal", SampleType.Blood, SampleContainer.Tube, 22000), ("Perfil hepático", SampleType.Blood, SampleContainer.Tube, 24000),
        ("Glicemia", SampleType.Blood, SampleContainer.Tube, 8000), ("Electrolitos", SampleType.Blood, SampleContainer.Tube, 15000),
        ("T4 total", SampleType.Blood, SampleContainer.Tube, 28000), ("Test Leishmania", SampleType.Blood, SampleContainer.Tube, 30000),
        ("Test VIF/ViLeF", SampleType.Blood, SampleContainer.Tube, 32000), ("Test Parvovirus", SampleType.Feces, SampleContainer.Swab, 20000),
        ("Coproparasitario", SampleType.Feces, SampleContainer.Jar, 12000), ("Test Giardia", SampleType.Feces, SampleContainer.Jar, 16000),
        ("Urianálisis completo", SampleType.Urine, SampleContainer.Jar, 14000), ("Urocultivo", SampleType.Urine, SampleContainer.Jar, 26000),
        ("Citología", SampleType.Tissue, SampleContainer.Slide, 25000), ("Biopsia", SampleType.Tissue, SampleContainer.Jar, 60000),
        ("Raspado de piel", SampleType.Tissue, SampleContainer.Slide, 15000), ("Cultivo de hongos", SampleType.Tissue, SampleContainer.Swab, 27000),
        ("Frotis de oído", SampleType.Tissue, SampleContainer.Swab, 13000), ("Proteína C reactiva", SampleType.Blood, SampleContainer.Tube, 21000)
    ];
    private static readonly (string Brand, string Machine)[] LabMachines =
        [("IDEXX", "ProCyte Dx"), ("IDEXX", "Catalyst One"), ("Zoetis", "Vetscan VS2"), ("Mindray", "BC-5000 Vet"), ("Heska", "Element DC"), ("Manual", "Microscopio")];

    private static readonly (string Reason, string Diagnosis, string Treatment)[] VisitCases =
    [
        ("Control anual", "Paciente sano", "Mantener plan de vacunas y desparasitación"),
        ("Vómitos", "Gastritis aguda", "Dieta blanda 3 días; omeprazol 1 mg/kg c/24h"),
        ("Diarrea", "Enteritis", "Probióticos 5 días; hidratación"),
        ("Cojera", "Esguince", "Reposo 7 días; meloxicam 0,1 mg/kg c/24h"),
        ("Picazón", "Dermatitis alérgica", "Baños medicados semanales; antihistamínico"),
        ("Otitis", "Otitis externa", "Limpieza ótica; gotas óticas c/12h por 7 días"),
        ("Tos", "Traqueobronquitis", "Antitusivo 5 días; control en 1 semana"),
        ("Herida", "Herida superficial", "Limpieza y sutura; antibiótico 7 días"),
        ("Decaimiento", "Fiebre de origen a estudiar", "Hemograma y perfil bioquímico; control 48 h"),
        ("Control post operatorio", "Evolución favorable", "Retiro de puntos en 7 días")
    ];

    private static readonly string[] Supplies = ["Jeringa 3 ml", "Jeringa 5 ml", "Guantes de examen (par)", "Gasa estéril", "Suero fisiológico 500 ml",
        "Catéter 22G", "Venda elástica", "Collar isabelino M", "Alcohol 70% 1 L", "Algodón 500 g", "Sutura nylon 3-0", "Tubo EDTA"];

    private readonly Random _random = new();

    public async Task<IReadOnlyList<TestDataStatus>> GetStatusAsync()
    {
        var counts = await CountsAsync();
        return Generators.Select(g => new TestDataStatus(g, counts[g.Key], g.DependsOn.Where(d => counts[d] == 0).ToList())).ToList();
    }

    public async Task<TestDataResult> CreateAsync(string key, int count)
    {
        var generator = Generators.FirstOrDefault(g => g.Key == key);
        if (generator is null)
            return new TestDataResult(0, $"Unknown data kind '{key}'.");
        if (count is < 1 or > MaxCount)
            return new TestDataResult(0, $"Create between 1 and {MaxCount} at a time.");

        var counts = await CountsAsync();
        var missing = generator.DependsOn.Where(d => counts[d] == 0).ToList();
        if (missing.Count > 0)
            return new TestDataResult(0, $"{generator.Name} need {string.Join(" and ", missing.Select(m => Generators.First(g => g.Key == m).Name.ToLowerInvariant()))} first.");

        var created = key switch
        {
            "clients" => await ClientsAsync(count),
            "pets" => await PetsAsync(count),
            "exams" => await ExamsAsync(count),
            "external-labs" => await ExternalLabsAsync(count),
            "exam-requests" => await ExamRequestsAsync(count),
            "inventory" => await InventoryAsync(count),
            "visits" => await VisitsAsync(count),
            "doses" => await DosesAsync(count),
            "appointments" => await AppointmentsAsync(count),
            "sales" => await SalesAsync(count),
            _ => 0
        };
        return new TestDataResult(created);
    }

    private async Task<Dictionary<string, int>> CountsAsync() => new()
    {
        ["clients"] = await db.Clients.CountAsync(),
        ["pets"] = await db.Pets.CountAsync(),
        ["exams"] = await db.Exams.CountAsync(),
        ["external-labs"] = await db.ExternalLabs.CountAsync(),
        ["exam-requests"] = await db.ExamsPerformed.CountAsync(),
        ["inventory"] = await db.Items.CountAsync(),
        ["visits"] = await db.MedicalVisits.CountAsync(),
        ["doses"] = await db.PreventiveDoses.CountAsync(),
        ["appointments"] = await db.Appointments.CountAsync(),
        ["sales"] = await db.Sales.CountAsync()
    };

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    private async Task<int> ClientsAsync(int count)
    {
        var usedRuts = (await db.Clients.Select(c => c.TaxId).ToListAsync()).ToHashSet();
        for (var i = 0; i < count; i++)
        {
            string rut;
            do
            {
                var number = _random.Next(5_000_000, 26_000_000);
                rut = $"{number}-{Rut.ComputeCheckDigit(number)}";
            } while (!usedRuts.Add(rut));

            var first = Pick(FirstNames);
            var last = $"{Pick(LastNames)} {Pick(LastNames)}";
            var email = $"{Normalize(first)}.{Normalize(last.Split(' ')[0])}{_random.Next(10, 999)}@example.test";
            db.Clients.Add(new Client(first, last, rut, $"{Pick(Streets)} {_random.Next(100, 3999)}, Rancagua", _random.Next(900_000_000, 999_999_999), email));
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> PetsAsync(int count)
    {
        var owners = await db.Clients.Select(c => c.Id).ToListAsync();
        for (var i = 0; i < count; i++)
        {
            var dog = _random.Next(100) < 65;
            var birth = DateTime.UtcNow.Date.AddDays(-_random.Next(60, 15 * 365));
            db.Pets.Add(new Pet
            {
                OwnerId = Pick(owners),
                Name = Pick(PetNames),
                Species = dog ? Species.Dog : Species.Cat,
                Breed = dog ? Pick(DogBreeds) : Pick(CatBreeds),
                Sex = _random.Next(2) == 0 ? Sex.Female : Sex.Male,
                Birthdate = birth,
                Age = (int)((DateTime.UtcNow - birth).TotalDays / 365),
                Weight = (float)Math.Round(dog ? 3 + _random.NextDouble() * 37 : 2.5 + _random.NextDouble() * 5, 1),
                ReproductiveStatus = (ReproductiveStatus)_random.Next(0, 3),
                ChipId = _random.Next(100) < 60 ? _random.Next(100_000, 999_999_999) : 0
            });
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> ExamsAsync(int count)
    {
        var existing = (await db.Exams.Select(e => e.Name).ToListAsync()).ToHashSet();
        for (var i = 0; i < count; i++)
        {
            var type = Pick(ExamTypes);
            var (brand, machine) = Pick(LabMachines);
            var name = $"{type.Name} ({machine})";
            for (var n = 2; existing.Contains(name); n++)
                name = $"{type.Name} ({machine}) #{n}";
            existing.Add(name);
            var sell = type.Price + _random.Next(-3, 4) * 1000;
            db.Exams.Add(new Exam
            {
                Name = name, Brand = brand, Machine = machine, SampleType = type.Sample, SampleContainer = type.Container,
                SellPrice = sell, BuyPrice = sell * _random.Next(35, 60) / 100
            });
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> ExternalLabsAsync(int count)
    {
        string[] names = ["LabVet", "Vetlab", "Diagnovet", "BioAnimal", "Citovet", "Patovet", "Laboratorio Andes", "Lab del Sur"];
        for (var i = 0; i < count; i++)
        {
            var name = $"{Pick(names)} {Pick(["Rancagua", "Santiago", "Talca", "Viña del Mar", "Concepción"])}";
            db.ExternalLabs.Add(new ExternalLab
            {
                Name = name, ContactName = $"{Pick(FirstNames)} {Pick(LastNames)}", Phone = $"+56 9 {_random.Next(1000, 9999)} {_random.Next(1000, 9999)}",
                Email = $"contacto{_random.Next(1, 999)}@{Normalize(name.Split(' ')[0])}.example.test", Address = $"{Pick(Streets)} {_random.Next(100, 3999)}",
                DefaultTurnaroundDays = _random.Next(1, 8)
            });
        }
        await db.SaveChangesAsync();
        return count;
    }

    /// <summary>Each request goes to a different pet (cycling when there are fewer pets than requests).</summary>
    private async Task<int> ExamRequestsAsync(int count)
    {
        var exams = await db.Exams.Select(e => e.Id).ToListAsync();
        var labs = await db.ExternalLabs.Select(l => new { l.Id, l.DefaultTurnaroundDays }).ToListAsync();
        var pets = (await db.Pets.Select(p => p.Id).ToListAsync()).OrderBy(_ => _random.Next()).ToList();
        var statuses = new[] { ExamItemStatus.Pending, ExamItemStatus.InProgress, ExamItemStatus.Completed, ExamItemStatus.Completed, ExamItemStatus.Cancelled };

        for (var i = 0; i < count; i++)
        {
            var request = new ExamPerformed
            {
                PatientId = pets[i % pets.Count],
                Responsible = $"Dr. {Pick(LastNames)}",
                Date = DateTime.UtcNow.AddDays(-_random.Next(0, 120)).AddHours(-_random.Next(0, 10))
            };
            foreach (var examId in exams.OrderBy(_ => _random.Next()).Take(_random.Next(1, 4)))
            {
                var external = labs.Count > 0 && _random.Next(100) < 25;
                var lab = external ? Pick(labs) : null;
                request.Items.Add(new ExamRequestItem
                {
                    ExamId = examId,
                    Status = Pick(statuses),
                    IsExternal = external,
                    ExternalLabId = lab?.Id,
                    ExternalEstimatedDays = lab?.DefaultTurnaroundDays
                });
            }
            db.ExamsPerformed.Add(request);
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> InventoryAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var sell = _random.Next(5, 300) * 100;
            var stock = _random.Next(100) < 10 ? 0 : _random.Next(1, 120);
            db.Items.Add(new Item($"{Pick(Supplies)} · lote {_random.Next(1000, 9999)}", ItemType.Material, $"TD{Guid.NewGuid():N}"[..14], "Dato de prueba",
                stock, sell, brand: Pick(["Medline", "3M", "BD", "Romed", "Genérico"]), lowStockThreshold: 5, buyPrice: sell * _random.Next(40, 70) / 100));
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> VisitsAsync(int count)
    {
        var pets = await db.Pets.Select(p => new { p.Id, p.Name, p.Weight }).ToListAsync();
        var exams = await db.Exams.Select(e => new { e.Id, e.Name, e.SellPrice }).ToListAsync();
        for (var i = 0; i < count; i++)
        {
            var pet = Pick(pets);
            var visitCase = Pick(VisitCases);
            var visit = new MedicalVisit
            {
                PatientId = pet.Id, PatientName = pet.Name, Date = DateTime.UtcNow.AddDays(-_random.Next(0, 365)).AddHours(-_random.Next(0, 9)),
                Responsible = $"Dr. {Pick(LastNames)}", Reason = visitCase.Reason, Anamnesis = "Tutor refiere el motivo de consulta hace 1-3 días.",
                Examination = "Mucosas rosadas, TLLC < 2 s, sin hallazgos relevantes salvo lo descrito.", Diagnosis = visitCase.Diagnosis,
                Treatment = visitCase.Treatment, WeightKg = pet.Weight > 0 ? Math.Round((decimal)pet.Weight * (decimal)(0.95 + _random.NextDouble() * 0.1), 1) : null,
                TemperatureC = Math.Round(37.8m + (decimal)_random.NextDouble() * 1.6m, 1)
            };
            if (exams.Count > 0 && _random.Next(100) < 40)
                foreach (var exam in exams.OrderBy(_ => _random.Next()).Take(_random.Next(1, 3)))
                    visit.Procedures.Add(new VisitProcedure { ExamId = exam.Id, Name = exam.Name, Price = exam.SellPrice });
            visit.TotalValue = visit.Procedures.Sum(p => p.Price);
            db.MedicalVisits.Add(visit);
        }
        await db.SaveChangesAsync();
        return count;
    }

    private async Task<int> DosesAsync(int count)
    {
        var protocols = (await clinicalSettings.GetAsync()).Settings.Protocols.Where(p => p.Enabled).ToList();
        if (protocols.Count == 0)
            return 0;
        var pets = await db.Pets.Select(p => new { p.Id, p.Species }).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var pet = Pick(pets);
            var options = protocols.Where(p => p.Species is null || p.Species == pet.Species).ToList();
            if (options.Count == 0)
                continue;
            var protocol = Pick(options);
            var applied = today.AddDays(-_random.Next(0, Math.Max(protocol.IntervalDays, 30) + 60));
            db.PreventiveDoses.Add(new PreventiveDose
            {
                PetId = pet.Id, ProtocolCode = protocol.Code, Kind = protocol.Kind, ProductName = protocol.Name,
                BatchNumber = $"L{_random.Next(10000, 99999)}", AppliedOn = applied,
                NextDueOn = protocol.IntervalDays > 0 ? applied.AddDays(protocol.IntervalDays) : null,
                AppliedBy = $"Dr. {Pick(LastNames)}", CreatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return count;
    }

    /// <summary>Half-hour slots between 9:00 and 18:00 (clinic time), never two on the same vet/room at once.</summary>
    private async Task<int> AppointmentsAsync(int count)
    {
        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var services = agenda.Services.Where(s => s.ResourceCodes.Count > 0).ToList();
        if (services.Count == 0)
            return 0;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(agenda.TimeZone);
        var pets = await db.Pets.Join(db.Clients, p => p.OwnerId, c => c.Id, (p, c) => new { PetId = p.Id, PetName = p.Name, OwnerFirst = c.Name, OwnerLast = c.LastName, c.Email, ClientId = c.Id, c.PhoneNumber })
            .ToListAsync();
        var taken = (await db.Appointments.Select(a => new { a.ResourceCode, a.StartUtc }).ToListAsync()).Select(a => (a.ResourceCode, a.StartUtc)).ToHashSet();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        var now = DateTime.UtcNow;

        var created = 0;
        for (var attempt = 0; created < count && attempt < count * 20; attempt++)
        {
            var service = Pick(services);
            var resource = Pick(service.ResourceCodes);
            var day = today.AddDays(_random.Next(-14, 15));
            var local = day.ToDateTime(new TimeOnly(9, 0)).AddMinutes(30 * _random.Next(0, 18));
            if (zone.IsInvalidTime(local))
                continue;
            var start = TimeZoneInfo.ConvertTimeToUtc(local, zone);
            if (!taken.Add((resource, start)))
                continue;

            var pet = Pick(pets);
            var past = start < now;
            var roll = _random.Next(100);
            db.Appointments.Add(new Appointment
            {
                ServiceCode = service.Code, ResourceCode = resource, StartUtc = start, EndUtc = start.AddMinutes(service.DurationMinutes),
                OccupiedUntilUtc = start.AddMinutes(service.DurationMinutes + service.BufferMinutes),
                Status = roll < 10 ? AppointmentStatus.Cancelled : past ? (roll < 20 ? AppointmentStatus.NoShow : AppointmentStatus.Completed) : AppointmentStatus.Confirmed,
                Source = _random.Next(100) < 40 ? AppointmentSource.Online : AppointmentSource.Staff,
                ClientId = pet.ClientId, PetId = pet.PetId, PetName = pet.PetName, OwnerName = $"{pet.OwnerFirst} {pet.OwnerLast}".Trim(), OwnerEmail = pet.Email,
                OwnerPhone = pet.PhoneNumber > 0 ? pet.PhoneNumber.ToString() : null, Price = service.Price,
                DepositAmount = service.Price * service.DepositPercent / 100, CreatedAtUtc = now, CreatedBy = "test-data"
            });
            created++;
        }
        await db.SaveChangesAsync();
        return created;
    }

    private async Task<int> SalesAsync(int count)
    {
        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var billing = (await billingSettings.GetAsync()).Settings;
        var methods = billing.PaymentMethods.Where(m => m.Enabled).Select(m => m.Code).ToList();
        var clients = await db.Clients.Select(c => new { c.Id, c.Name, c.LastName }).ToListAsync();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(agenda.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        string[] others = ["Baño y corte", "Certificado de salud", "Microchip", "Hospitalización (día)", "Curación", "Limpieza dental"];

        for (var i = 0; i < count; i++)
        {
            var client = Pick(clients);
            var date = today.AddDays(-_random.Next(0, 30));
            var createdAt = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(_random.Next(9, 19), _random.Next(0, 60))), zone);
            var sale = new Sale { ClientId = client.Id, CustomerName = $"{client.Name} {client.LastName}".Trim(), BusinessDate = date, CreatedAtUtc = createdAt, CreatedBy = "test-data" };

            foreach (var _ in Enumerable.Range(0, _random.Next(1, 4)))
            {
                if (agenda.Services.Count > 0 && _random.Next(2) == 0)
                {
                    var service = Pick(agenda.Services);
                    sale.AddLine(new SaleLine { Kind = SaleLineKind.Service, ServiceCode = service.Code, Description = service.Name, Quantity = 1,
                        UnitPrice = service.Price, TaxExempt = billing.Tax.IsServiceExempt(service.Code) });
                }
                else
                    sale.AddLine(new SaleLine { Kind = SaleLineKind.Other, Description = Pick(others), Quantity = 1, UnitPrice = _random.Next(5, 60) * 1000 });
            }

            // 75% fully paid (sometimes in two methods), 15% partly paid, 10% open.
            var roll = _random.Next(100);
            if (methods.Count > 0 && roll < 90)
            {
                var amount = roll < 75 ? sale.Total : sale.Total / 2;
                var split = roll < 20 && amount > 1000 ? amount / 2 : amount;
                foreach (var part in split == amount ? new[] { amount } : [split, amount - split])
                    sale.AddPayment(new SalePayment { Method = Pick(methods), Amount = part, ReceivedAtUtc = createdAt, BusinessDate = date, ReceivedBy = "test-data" });
            }
            db.Sales.Add(sale);
        }
        await db.SaveChangesAsync();
        return count;
    }

    private static string Normalize(string text)
        => new string(text.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterOrDigit(c)).ToArray());
}
