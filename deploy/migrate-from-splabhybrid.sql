/*
  One-time data copy from the legacy SPLabHybrid database (EF migration 20250913015650_v0_0_3)
  into a NEW VetManagement database. The legacy database is only read, never changed.

  Run it connected to the new database, after its schema exists and BEFORE the new API starts
  (the API seeds roles on start-up and they would clash with the copied ones):

    sqlcmd -S localhost\SQLEXPRESS -E -I -d VetManagement -b -v Source=SPVetClinic -i migrate-from-splabhybrid.sql

  -I (QUOTED_IDENTIFIER ON) is required: the schema has filtered indexes.

  Copied: users, roles and their links (password hashes keep working), items, inventory movements,
  audit log, exams. Clients, pets, visits and exam requests were empty in the legacy database.
  Changes on the way: Items.Price -> SellPrice (BuyPrice = 0, the legacy app had no cost),
  AuditLogs.Action text -> number. Roles are copied as they are; on its first start the API replaces the old ones
  (Employee, Manager, Laboratorio) by area roles with the same access (see DataSeeder.LegacyRoles).
  Everything runs in one transaction: any error leaves the new database empty.
*/
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM [$(Source)].dbo.__EFMigrationsHistory WHERE MigrationId = '20250913015650_v0_0_3')
    THROW 50001, 'The source is not the SPLabHybrid v0_0_3 database.', 1;
IF EXISTS (SELECT 1 FROM [$(Source)].dbo.__EFMigrationsHistory WHERE MigrationId > '20250913015650_v0_0_3')
    THROW 50002, 'The source has migrations newer than v0_0_3; review this script first.', 1;
IF EXISTS (SELECT 1 FROM dbo.AspNetUsers) OR EXISTS (SELECT 1 FROM dbo.AspNetRoles) OR EXISTS (SELECT 1 FROM dbo.Items)
   OR EXISTS (SELECT 1 FROM dbo.InventoryMovements) OR EXISTS (SELECT 1 FROM dbo.AuditLogs) OR EXISTS (SELECT 1 FROM dbo.Exams)
    THROW 50003, 'The target database is not empty: run this only on a freshly created database, before the API starts.', 1;

DECLARE @unmapped nvarchar(max) =
    (SELECT STRING_AGG(Action, ', ') FROM (SELECT DISTINCT Action FROM [$(Source)].dbo.AuditLogs) a
     WHERE Action NOT IN ('Add', 'Agregar', 'Edit', 'Editar', 'Delete', 'Eliminar', 'Entry', 'Exit', 'Ajuste de stock',
                          'MassiveStockEntry', 'Ingreso masivo', 'MassiveStockExit', 'Egreso masivo'));
IF @unmapped IS NOT NULL
BEGIN
    DECLARE @message nvarchar(2048) = CONCAT('Audit actions without a mapping: ', @unmapped);
    THROW 50004, @message, 1;
END;

BEGIN TRANSACTION;

INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT Id, Name, NormalizedName, ConcurrencyStamp FROM [$(Source)].dbo.AspNetRoles;

INSERT INTO dbo.AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp,
    ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount)
SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp,
    ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
FROM [$(Source)].dbo.AspNetUsers;

INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
SELECT UserId, RoleId FROM [$(Source)].dbo.AspNetUserRoles;

SET IDENTITY_INSERT dbo.Items ON;
INSERT INTO dbo.Items (Id, Name, Type, Brand, Description, Stock, SellPrice, BuyPrice, Barcode, BrandBarcode, LowStockThreshold,
    Compound, ML, Concentration, DosageDog_Min, DosageDog_Max, DosageCat_Min, DosageCat_Max)
SELECT Id, Name, Type, Brand, Description, Stock, Price, 0, Barcode, BrandBarcode, LowStockThreshold,
    Compound, ML, Concentration, DosageDog_Min, DosageDog_Max, DosageCat_Min, DosageCat_Max
FROM [$(Source)].dbo.Items;
SET IDENTITY_INSERT dbo.Items OFF;

-- Movement types 1 (Entry) and 2 (Exit) mean Ingress and Egress in both versions.
SET IDENTITY_INSERT dbo.InventoryMovements ON;
INSERT INTO dbo.InventoryMovements (Id, ItemId, Type, Quantity, Date, Reason, Responsible)
SELECT Id, ItemId, Type, Quantity, Date, Reason, Responsible FROM [$(Source)].dbo.InventoryMovements;
SET IDENTITY_INSERT dbo.InventoryMovements OFF;

-- AuditActionType: Add 1, Edit 2, Delete 3, Ingress 100, Egress 101, Adjustment 102, MassiveStockIngress 103, MassiveStockEgress 104.
SET IDENTITY_INSERT dbo.AuditLogs ON;
INSERT INTO dbo.AuditLogs (Id, EntityId, EntityName, Date, Action, Changes, [User])
SELECT Id, EntityId, EntityName, Date,
    CASE Action
        WHEN 'Add' THEN 1 WHEN 'Agregar' THEN 1
        WHEN 'Edit' THEN 2 WHEN 'Editar' THEN 2
        WHEN 'Delete' THEN 3 WHEN 'Eliminar' THEN 3
        WHEN 'Entry' THEN 100
        WHEN 'Exit' THEN 101
        WHEN 'Ajuste de stock' THEN 102
        WHEN 'MassiveStockEntry' THEN 103 WHEN 'Ingreso masivo' THEN 103
        WHEN 'MassiveStockExit' THEN 104 WHEN 'Egreso masivo' THEN 104
    END,
    Changes, [User]
FROM [$(Source)].dbo.AuditLogs;
SET IDENTITY_INSERT dbo.AuditLogs OFF;

SET IDENTITY_INSERT dbo.Exams ON;
INSERT INTO dbo.Exams (Id, Name, Brand, Machine, SampleType, SampleContainer, BuyPrice, SellPrice)
SELECT Id, Name, Brand, Machine, SampleType, SampleContainer, BuyPrice, SellPrice FROM [$(Source)].dbo.Exams;
SET IDENTITY_INSERT dbo.Exams OFF;

COMMIT;

SELECT 'Users' AS [Table], (SELECT COUNT(*) FROM dbo.AspNetUsers) AS Target, (SELECT COUNT(*) FROM [$(Source)].dbo.AspNetUsers) AS Source
UNION ALL SELECT 'Items', (SELECT COUNT(*) FROM dbo.Items), (SELECT COUNT(*) FROM [$(Source)].dbo.Items)
UNION ALL SELECT 'InventoryMovements', (SELECT COUNT(*) FROM dbo.InventoryMovements), (SELECT COUNT(*) FROM [$(Source)].dbo.InventoryMovements)
UNION ALL SELECT 'AuditLogs', (SELECT COUNT(*) FROM dbo.AuditLogs), (SELECT COUNT(*) FROM [$(Source)].dbo.AuditLogs)
UNION ALL SELECT 'Exams', (SELECT COUNT(*) FROM dbo.Exams), (SELECT COUNT(*) FROM [$(Source)].dbo.Exams);
