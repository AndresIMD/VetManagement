using Microsoft.AspNetCore.Identity;
using VetManagement.Api.Authorization;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Audit;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Inventory;
using VetManagement.Shared.Models.Exams;
using VetManagement.Shared.Models.Medical;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainItem = VetManagement.Domain.Inventory.Item;
using DomainDrug = VetManagement.Domain.Inventory.Drug;
using DomainDosageRange = VetManagement.Domain.Inventory.DosageRange;

namespace VetManagement.Api.Seeding;

/// <summary>
/// Default data seeder for roles and admin/test users.
/// </summary>
public class DataSeeder(
    RoleManager<IdentityRole> roles,
    UserManager<IdentityUser> users,
    IConfiguration config,
    ILogger<DataSeeder> logger,
    IUnitOfWork unitOfWork) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureRolesAsync(ct);
        await EnsureAdminAsync(ct);
        await EnsureDevTestUsersAsync(ct);
        await EnsureInventoryAsync(ct);
        await EnsureExamsAsync(ct);
        await EnsureExternalLabsAsync(ct);
        await EnsureClientsAndPetsAsync(ct);
        await EnsureMedicalVisitsAsync(ct);
        await EnsureAuditLogsAsync(ct);
    }

    private async Task EnsureRolesAsync(CancellationToken ct)
    {
        foreach (var role in new[] { "Admin", "Manager", "Employee" })
            if (!await roles.RoleExistsAsync(role))
            {
                var res = await roles.CreateAsync(new IdentityRole(role));
                if (res.Succeeded)
                    logger.LogInformation("Role {Role} created", role);
                else
                    logger.LogWarning("Role {Role} creation failed: {Errors}", role, string.Join(",", res.Errors.Select(e => e.Code)));
            }
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedAdmin:Enabled") ?? false;

        if (!enabled)
            return;

        var userName = config["SeedAdmin:UserName"];
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { logger.LogWarning("SeedAdmin is enabled but missing credentials."); return; }
        var user = await users.FindByNameAsync(userName);
        if (user == null)
        {
            user = new IdentityUser { UserName = userName, Email = email, EmailConfirmed = true };
            var createRes = await users.CreateAsync(user, password);
            if (!createRes.Succeeded)
            { logger.LogError("Failed to create admin user: {Errors}", string.Join(",", createRes.Errors.Select(e => e.Code))); return; }
            await users.AddToRoleAsync(user, "Admin");
            logger.LogInformation("Admin user {User} created and role assigned.", userName);
        }
        await EnsureAllPermissionsAsync(user);
    }

    private async Task EnsureDevTestUsersAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedTest:Enabled") ?? false;

        if (!enabled)
            return;

        var defs = new[]
        {
            new { UserName = config["SeedTest:Manager:UserName"], Email = config["SeedTest:Manager:Email"], Password = config["SeedTest:Manager:Password"], Role = "Manager" },
            new { UserName = config["SeedTest:Employee:UserName"], Email = config["SeedTest:Employee:Email"], Password = config["SeedTest:Employee:Password"], Role = "Employee" }
        };
        foreach (var d in defs)
        {
            if (string.IsNullOrWhiteSpace(d.UserName) || string.IsNullOrWhiteSpace(d.Email) || string.IsNullOrWhiteSpace(d.Password))
            { logger.LogWarning("SeedTest user missing configuration for role {Role}", d.Role); continue; }
            var user = await users.FindByNameAsync(d.UserName);
            if (user == null)
            {
                user = new IdentityUser { UserName = d.UserName, Email = d.Email, EmailConfirmed = true };
                var res = await users.CreateAsync(user, d.Password);
                if (res.Succeeded)
                { await users.AddToRoleAsync(user, d.Role); logger.LogInformation("Test user {User} created with role {Role}", d.UserName, d.Role); }
                else
                { logger.LogWarning("Failed to create test user {User}: {Errors}", d.UserName, string.Join(",", res.Errors.Select(e => e.Code))); }
            }
            await EnsureRolePermissionsAsync(user, d.Role);
        }
    }

    private async Task EnsureInventoryAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        // Check if we already have items
        var existingItems = await unitOfWork.Items.GetAllAsync();
        if (existingItems.Any())
        {
            logger.LogInformation("Inventory already seeded.");
            return;
        }

        logger.LogInformation("Seeding inventory...");

        var random = new Random();
        var brands = new[] { "Pfizer", "Bayer", "Zoetis", "Merck", "Generic", "Royal Canin", "Purina" };
        var drugNames = new[] { "Amoxicillin", "Carprofen", "Meloxicam", "Prednisone", "Gabapentin", "Tramadol", "Cephalexin" };
        var materialNames = new[] { "Syringe 3ml", "Syringe 5ml", "Bandage", "Cotton Balls", "Surgical Gloves", "Scalpel Blade", "Gauze" };

        // 1. Seed Materials
        for (int i = 0; i < 20; i++)
        {
            var name = $"{materialNames[random.Next(materialNames.Length)]} - {i + 1}";
            var item = new DomainItem(
                name: name,
                type: DomainItemType.Material,
                barcode: $"MAT-{1000 + i}",
                description: $"Standard medical material: {name}",
                stock: random.Next(10, 500),
                sellPrice: random.Next(100, 1000) * 10,
                brand: brands[random.Next(brands.Length)],
                buyPrice: random.Next(50, 500) * 10
            );
            await unitOfWork.Items.AddAsync(item);
        }

        // 2. Seed Drugs
        for (int i = 0; i < 30; i++)
        {
            var baseName = drugNames[random.Next(drugNames.Length)];
            var name = $"{baseName} {random.Next(10, 100)}mg";

            var drug = new DomainDrug(
                type: DomainItemType.Drug,
                name: name,
                barcode: $"DRUG-{2000 + i}",
                description: $"Veterinary drug: {name}",
                compound: baseName,
                ml: random.Next(10, 500),
                concentration: (float)random.NextDouble() * 100,
                dosageDog: new DomainDosageRange(random.Next(5, 10), random.Next(15, 20)),
                dosageCat: new DomainDosageRange(random.Next(2, 5), random.Next(8, 12)),
                stock: random.Next(5, 100),
                sellPrice: random.Next(500, 5000) * 10,
                brand: brands[random.Next(brands.Length)]
            )
            {
                BuyPrice = random.Next(200, 2000) * 10
            };

            await unitOfWork.Items.AddAsync(drug);
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Inventory seeded successfully.");
    }

    private async Task EnsureExamsAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        var existingExams = await unitOfWork.Exams.GetAllAsync();
        if (existingExams.Any())
        {
            logger.LogInformation("Exams already seeded.");
            return;
        }

        logger.LogInformation("Seeding exams...");

        var exams = new[]
        {
            new Exam { Name = "Complete Blood Count (CBC)", Brand = "Idexx", Machine = "ProCyte Dx", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 15000, SellPrice = 25000 },
            new Exam { Name = "Blood Chemistry Panel", Brand = "Idexx", Machine = "Catalyst One", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 20000, SellPrice = 35000 },
            new Exam { Name = "Urinalysis", Brand = "Generic", Machine = "Manual", SampleType = SampleType.Urine, SampleContainer = SampleContainer.Jar, BuyPrice = 8000, SellPrice = 15000 },
            new Exam { Name = "Fecal Parasite Exam", Brand = "Generic", Machine = "Microscope", SampleType = SampleType.Feces, SampleContainer = SampleContainer.Pot, BuyPrice = 5000, SellPrice = 10000 },
            new Exam { Name = "T4 Thyroid Test", Brand = "Zoetis", Machine = "VetTest", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 12000, SellPrice = 22000 },
            new Exam { Name = "FeLV/FIV Test", Brand = "Idexx", Machine = "SNAP", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 18000, SellPrice = 30000 },
            new Exam { Name = "Heartworm Test", Brand = "Idexx", Machine = "SNAP 4Dx Plus", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 20000, SellPrice = 35000 },
            new Exam { Name = "Parvovirus Test", Brand = "Zoetis", Machine = "SNAP Parvo", SampleType = SampleType.Feces, SampleContainer = SampleContainer.Pot, BuyPrice = 15000, SellPrice = 25000 },
            new Exam { Name = "Blood Glucose", Brand = "Generic", Machine = "Glucometer", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube, BuyPrice = 3000, SellPrice = 8000 },
            new Exam { Name = "Skin Scraping", Brand = "Generic", Machine = "Microscope", SampleType = SampleType.Tissue, SampleContainer = SampleContainer.Slide, BuyPrice = 4000, SellPrice = 10000 }
        };

        foreach (var exam in exams)
            await unitOfWork.Exams.AddAsync(exam);

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Exams seeded successfully.");
    }

    private async Task EnsureExternalLabsAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        var existingLabs = await unitOfWork.ExternalLabs.GetAllAsync();
        if (existingLabs.Any())
        {
            logger.LogInformation("External labs already seeded.");
            return;
        }

        logger.LogInformation("Seeding external labs...");

        var labs = new[]
        {
            new ExternalLab { Name = "VetPath Diagnostics", ContactName = "Dr. Sarah Mitchell", Phone = "+1234567890", Email = "contact@vetpath.com", Address = "123 Lab Street, Medical District", Notes = "Specialized in histopathology", DefaultTurnaroundDays = 5 },
            new ExternalLab { Name = "BioVet Laboratory", ContactName = "Dr. John Anderson", Phone = "+1234567891", Email = "info@biovet.com", Address = "456 Science Avenue, Research Park", Notes = "Full service veterinary diagnostics", DefaultTurnaroundDays = 3 },
            new ExternalLab { Name = "Animal Health Labs", ContactName = "Dr. Emily Chen", Phone = "+1234567892", Email = "lab@animalhealth.com", Address = "789 Diagnostic Road, Tech Hub", Notes = "Rapid turnaround for urgent cases", DefaultTurnaroundDays = 2 }
        };

        foreach (var lab in labs)
            await unitOfWork.ExternalLabs.AddAsync(lab);

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("External labs seeded successfully.");
    }

    private async Task EnsureClientsAndPetsAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        var existingClients = await unitOfWork.Clients.GetAllAsync();
        if (existingClients.Any())
        {
            logger.LogInformation("Clients and pets already seeded.");
            return;
        }

        logger.LogInformation("Seeding clients and pets...");

        var random = new Random();
        var firstNames = new[] { "John", "Maria", "Carlos", "Ana", "David", "Laura", "Miguel", "Sofia", "Pedro", "Isabella" };
        var lastNames = new[] { "Smith", "Garcia", "Johnson", "Martinez", "Williams", "Rodriguez", "Brown", "Lopez", "Davis", "Gonzalez" };
        var dogNames = new[] { "Max", "Luna", "Charlie", "Bella", "Rocky", "Daisy", "Cooper", "Lucy", "Buddy", "Molly" };
        var catNames = new[] { "Whiskers", "Shadow", "Mittens", "Tiger", "Simba", "Cleo", "Felix", "Nala", "Oliver", "Luna" };
        var dogBreeds = new[] { "Labrador Retriever", "German Shepherd", "Golden Retriever", "Bulldog", "Beagle" };
        var catBreeds = new[] { "Persian", "Maine Coon", "Siamese", "Ragdoll", "Bengal" };

        for (int i = 0; i < 15; i++)
        {
            var client = new Client(
                name: firstNames[random.Next(firstNames.Length)],
                lastName: lastNames[random.Next(lastNames.Length)],
                taxId: $"TAX-{10000 + i}",
                address: $"{random.Next(100, 999)} Main Street, Apt {random.Next(1, 50)}",
                phoneNumber: random.Next(100000000, 999999999),
                email: $"client{i + 1}@email.com"
            );

            await unitOfWork.Clients.AddAsync(client);
            await unitOfWork.SaveChangesAsync();

            int petCount = random.Next(1, 4);
            for (int p = 0; p < petCount; p++)
            {
                var isDog = random.Next(0, 2) == 0;
                var species = isDog ? Species.Dog : Species.Cat;
                var breeds = isDog ? dogBreeds : catBreeds;
                var age = random.Next(1, 15);
                var birthdate = DateTime.UtcNow.AddYears(-age).AddMonths(-random.Next(0, 12));

                var pet = new Pet
                {
                    OwnerId = client.Id,
                    Name = isDog ? dogNames[random.Next(dogNames.Length)] : catNames[random.Next(catNames.Length)],
                    Sex = (Sex)random.Next(1, 3),
                    Species = species,
                    Breed = breeds[random.Next(breeds.Length)],
                    Birthdate = birthdate,
                    Age = age,
                    Weight = isDog ? random.Next(5, 40) + (float)random.NextDouble() : random.Next(2, 8) + (float)random.NextDouble(),
                    ReproductiveStatus = (ReproductiveStatus)random.Next(1, 4),
                    ChipId = random.Next(1000000, 9999999)
                };

                await unitOfWork.Pets.AddAsync(pet);
            }
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Clients and pets seeded successfully.");
    }

    private async Task EnsureMedicalVisitsAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        var existingVisits = await unitOfWork.MedicalVisits.GetAllAsync();
        if (existingVisits.Any())
        {
            logger.LogInformation("Medical visits already seeded.");
            return;
        }

        logger.LogInformation("Seeding medical visits...");

        var random = new Random();
        var pets = await unitOfWork.Pets.GetAllAsync();
        var exams = await unitOfWork.Exams.GetAllAsync();
        var procedures = new[] { "Vaccination", "Deworming", "Dental Cleaning", "Nail Trim", "Ear Cleaning", "Wound Treatment", "Physical Examination", "Surgery Consultation" };

        if (!pets.Any() || !exams.Any())
        {
            logger.LogWarning("Cannot seed medical visits: no pets or exams found.");
            return;
        }

        for (int i = 0; i < 25; i++)
        {
            var pet = pets[random.Next(pets.Count)];
            var procedureCount = random.Next(1, 4);
            var visitProcedures = new List<VisitProcedure>();
            var totalValue = 0;

            for (int p = 0; p < procedureCount; p++)
            {
                var price = random.Next(5000, 50000);
                totalValue += price;

                visitProcedures.Add(new VisitProcedure
                {
                    ExamId = random.Next(0, 3) == 0 ? exams[random.Next(exams.Count)].Id : null,
                    Name = procedures[random.Next(procedures.Length)],
                    Price = price,
                    Notes = random.Next(0, 3) == 0 ? "Patient responded well to treatment" : null
                });
            }

            var visit = new MedicalVisit
            {
                Date = DateTime.UtcNow.AddDays(-random.Next(0, 180)),
                PatientId = pet.Id,
                RecordNumber = $"MV-{1000 + i}",
                PatientName = pet.Name,
                Responsible = "Dr. Veterinarian",
                Location = "Main Clinic",
                BudgetNumber = random.Next(0, 2) == 0 ? $"BUD-{random.Next(1000, 9999)}" : null,
                PaymentStatus = (PaymentStatus)random.Next(1, 4),
                PaymentMethod = (PaymentMethod)random.Next(1, 6),
                TotalValue = totalValue,
                Procedures = visitProcedures
            };

            await unitOfWork.MedicalVisits.AddAsync(visit);
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Medical visits seeded successfully.");
    }

    private async Task EnsureAuditLogsAsync(CancellationToken ct)
    {
        var enabled = config.GetValue<bool?>("SeedInventory:Enabled") ?? false;
        if (!enabled)
            return;

        var existingLogs = await unitOfWork.AuditLogs.GetAllAsync();
        if (existingLogs.Any())
        {
            logger.LogInformation("Audit logs already seeded.");
            return;
        }

        logger.LogInformation("Seeding audit logs...");

        var random = new Random();
        var users = new[] { "admin", "manager", "employee", "Dr. Veterinarian" };
        var entityNames = new[] { nameof(Item), nameof(Client), nameof(Pet), nameof(Exam), nameof(ExternalLab), nameof(MedicalVisit) };

        var items = await unitOfWork.Items.GetAllAsync();
        var clients = await unitOfWork.Clients.GetAllAsync();
        var pets = await unitOfWork.Pets.GetAllAsync();
        var exams = await unitOfWork.Exams.GetAllAsync();
        var labs = await unitOfWork.ExternalLabs.GetAllAsync();
        var visits = await unitOfWork.MedicalVisits.GetAllAsync();

        for (int i = 0; i < 100; i++)
        {
            var entityName = entityNames[random.Next(entityNames.Length)];
            var action = (AuditActionType)random.Next(1, 4);
            int entityId = 1;
            string changes = "{}";

            switch (entityName)
            {
                case nameof(Item):
                    if (items.Any())
                    {
                        var item = items[random.Next(items.Count)];
                        entityId = item.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"name\":\"{item.Name}\",\"stock\":{item.Stock},\"sellPrice\":{item.SellPrice}}}",
                            AuditActionType.Edit => $"{{\"oldStock\":{random.Next(0, 100)},\"newStock\":{item.Stock},\"name\":\"{item.Name}\"}}",
                            AuditActionType.Delete => $"{{\"name\":\"{item.Name}\",\"type\":\"{item.Type}\"}}",
                            _ => "{}"
                        };
                    }
                    break;

                case nameof(Client):
                    if (clients.Any())
                    {
                        var client = clients[random.Next(clients.Count)];
                        entityId = client.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"name\":\"{client.Name}\",\"lastName\":\"{client.LastName}\",\"email\":\"{client.Email}\"}}",
                            AuditActionType.Edit => $"{{\"name\":\"{client.Name}\",\"phone\":{client.PhoneNumber}}}",
                            AuditActionType.Delete => $"{{\"name\":\"{client.Name} {client.LastName}\",\"taxId\":\"{client.TaxId}\"}}",
                            _ => "{}"
                        };
                    }
                    break;

                case nameof(Pet):
                    if (pets.Any())
                    {
                        var pet = pets[random.Next(pets.Count)];
                        entityId = pet.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"name\":\"{pet.Name}\",\"species\":\"{pet.Species}\",\"breed\":\"{pet.Breed}\"}}",
                            AuditActionType.Edit => $"{{\"name\":\"{pet.Name}\",\"weight\":{pet.Weight},\"age\":{pet.Age}}}",
                            AuditActionType.Delete => $"{{\"name\":\"{pet.Name}\",\"species\":\"{pet.Species}\"}}",
                            _ => "{}"
                        };
                    }
                    break;

                case nameof(Exam):
                    if (exams.Any())
                    {
                        var exam = exams[random.Next(exams.Count)];
                        entityId = exam.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"name\":\"{exam.Name}\",\"brand\":\"{exam.Brand}\",\"sellPrice\":{exam.SellPrice}}}",
                            AuditActionType.Edit => $"{{\"name\":\"{exam.Name}\",\"oldPrice\":{exam.SellPrice - random.Next(1000, 5000)},\"newPrice\":{exam.SellPrice}}}",
                            AuditActionType.Delete => $"{{\"name\":\"{exam.Name}\"}}",
                            _ => "{}"
                        };
                    }
                    break;

                case nameof(ExternalLab):
                    if (labs.Any())
                    {
                        var lab = labs[random.Next(labs.Count)];
                        entityId = lab.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"name\":\"{lab.Name}\",\"contactName\":\"{lab.ContactName}\",\"phone\":\"{lab.Phone}\"}}",
                            AuditActionType.Edit => $"{{\"name\":\"{lab.Name}\",\"turnaroundDays\":{lab.DefaultTurnaroundDays}}}",
                            AuditActionType.Delete => $"{{\"name\":\"{lab.Name}\"}}",
                            _ => "{}"
                        };
                    }
                    break;

                case nameof(MedicalVisit):
                    if (visits.Any())
                    {
                        var visit = visits[random.Next(visits.Count)];
                        entityId = visit.Id;
                        changes = action switch
                        {
                            AuditActionType.Add => $"{{\"patientName\":\"{visit.PatientName}\",\"totalValue\":{visit.TotalValue},\"procedures\":{visit.Procedures.Count}}}",
                            AuditActionType.Edit => $"{{\"patientName\":\"{visit.PatientName}\",\"paymentStatus\":\"{visit.PaymentStatus}\"}}",
                            AuditActionType.Delete => $"{{\"recordNumber\":\"{visit.RecordNumber}\"}}",
                            _ => "{}"
                        };
                    }
                    break;
            }

            var log = new AuditLog
            {
                EntityId = entityId,
                EntityName = entityName,
                Date = DateTime.UtcNow.AddDays(-random.Next(0, 180)).AddHours(-random.Next(0, 24)),
                Action = action,
                Changes = changes,
                User = users[random.Next(users.Length)]
            };

            await unitOfWork.AuditLogs.AddAsync(log);
        }

        for (int i = 0; i < 30; i++)
        {
            if (!items.Any())
                break;

            var item = items[random.Next(items.Count)];
            var movementType = (AuditActionType)random.Next(100, 106);
            var quantity = random.Next(5, 50);

            var log = new AuditLog
            {
                EntityId = item.Id,
                EntityName = nameof(Item),
                Date = DateTime.UtcNow.AddDays(-random.Next(0, 90)).AddHours(-random.Next(0, 24)),
                Action = movementType,
                Changes = $"{{\"itemName\":\"{item.Name}\",\"quantity\":{quantity},\"type\":\"{movementType}\"}}",
                User = users[random.Next(users.Length)]
            };

            await unitOfWork.AuditLogs.AddAsync(log);
        }

        for (int i = 0; i < 20; i++)
        {
            var securityAction = (AuditActionType)random.Next(200, 209);
            var log = new AuditLog
            {
                EntityId = 0,
                EntityName = "Security",
                Date = DateTime.UtcNow.AddDays(-random.Next(0, 60)).AddHours(-random.Next(0, 24)),
                Action = securityAction,
                Changes = securityAction switch
                {
                    AuditActionType.UserLogin => $"{{\"user\":\"{users[random.Next(users.Length)]}\",\"success\":true}}",
                    AuditActionType.UserLoginFailed => $"{{\"user\":\"unknown_user\",\"success\":false}}",
                    AuditActionType.PasswordChanged => $"{{\"user\":\"{users[random.Next(users.Length)]}\"}}",
                    _ => "{}"
                },
                User = users[random.Next(users.Length)]
            };

            await unitOfWork.AuditLogs.AddAsync(log);
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Audit logs seeded successfully.");
    }

    private async Task EnsureAllPermissionsAsync(IdentityUser user)
    {
        var claims = await users.GetClaimsAsync(user);
        var has = claims.Where(c => c.Type == Permissions.CLAIM_TYPE).Select(c => c.Value).ToHashSet();
        foreach (var perm in Permissions.ALL())
            if (!has.Contains(perm))
                await users.AddClaimAsync(user, new System.Security.Claims.Claim(Permissions.CLAIM_TYPE, perm));
    }

    private async Task EnsureRolePermissionsAsync(IdentityUser user, string role)
    {
        var claims = await users.GetClaimsAsync(user);
        var has = claims.Where(c => c.Type == Permissions.CLAIM_TYPE).Select(c => c.Value).ToHashSet();
        IEnumerable<string> target = role switch
        {
            "Admin" => Permissions.ALL(),
            "Manager" => new[]
            {
                Permissions.INVENTORY.READ, Permissions.INVENTORY.CREATE, Permissions.INVENTORY.UPDATE,
                Permissions.EXAMS.READ, Permissions.EXAMS.UPDATE,
                Permissions.EXTERNAL_LABS.READ, Permissions.EXTERNAL_LABS.UPDATE,
                Permissions.EXAMS_PERFORMED.READ, Permissions.EXAMS_PERFORMED.CREATE, Permissions.EXAMS_PERFORMED.UPDATE,
                Permissions.MEDICAL.READ, Permissions.MEDICAL.CREATE, Permissions.MEDICAL.UPDATE,
                Permissions.CLIENTS_PETS.READ, Permissions.CLIENTS_PETS.CREATE, Permissions.CLIENTS_PETS.UPDATE,
                Permissions.AUDIT.READ,
                Permissions.SYSTEM.SEND_EMAIL
            },
            _ => new[]
            {
                Permissions.INVENTORY.READ, Permissions.INVENTORY.CREATE, Permissions.INVENTORY.UPDATE,
                Permissions.EXAMS.READ,
                Permissions.EXTERNAL_LABS.READ,
                Permissions.EXAMS_PERFORMED.READ, Permissions.EXAMS_PERFORMED.CREATE,
                Permissions.MEDICAL.READ, Permissions.MEDICAL.CREATE,
                Permissions.CLIENTS_PETS.READ, Permissions.CLIENTS_PETS.CREATE
            }
        };
        foreach (var perm in target)
            if (!has.Contains(perm))
                await users.AddClaimAsync(user, new System.Security.Claims.Claim(Permissions.CLAIM_TYPE, perm));
    }
}
