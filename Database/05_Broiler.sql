/* =====================================================================
   BROILER MODULE  -  upgrade script (safe to run more than once)
   Run after 03_Seed.sql on a new database, or on an existing PoultryERP
   database to add broiler (meat bird) support:
     * Flock type Layer / Broiler, broiler breed standard (body weight,
       cumulative feed, FCR), target lifting age, max broiler age (70 d)
     * Broiler sheds, broiler feeds (pre-starter / starter / finisher)
     * Broiler sales ("lifting") by number + live weight, rate per Kg;
       these sales reduce the shed's bird count automatically
   ===================================================================== */
USE PoultryERP;
GO
SET NOCOUNT ON;
GO

/* ---------- new columns ---------- */
IF COL_LENGTH('dbo.Breed', 'BirdType') IS NULL
    ALTER TABLE dbo.Breed ADD BirdType NVARCHAR(10) NOT NULL CONSTRAINT DF_Breed_BirdType DEFAULT (N'Layer');
GO
IF COL_LENGTH('dbo.Flock', 'FlockType') IS NULL
    ALTER TABLE dbo.Flock ADD FlockType NVARCHAR(10) NOT NULL CONSTRAINT DF_Flock_Type DEFAULT (N'Layer');
GO
IF COL_LENGTH('dbo.Flock', 'TargetLiftDays') IS NULL
    ALTER TABLE dbo.Flock ADD TargetLiftDays INT NULL;
GO
IF COL_LENGTH('dbo.BreedStandard', 'CumFeedG') IS NULL
    ALTER TABLE dbo.BreedStandard ADD CumFeedG DECIMAL(10,1) NULL;
GO
IF COL_LENGTH('dbo.BreedStandard', 'StdFCR') IS NULL
    ALTER TABLE dbo.BreedStandard ADD StdFCR DECIMAL(6,3) NULL;
GO
IF COL_LENGTH('dbo.Company', 'BroilerMaxDays') IS NULL
    ALTER TABLE dbo.Company ADD BroilerMaxDays INT NOT NULL CONSTRAINT DF_Company_BrMax DEFAULT (70);
GO
IF COL_LENGTH('dbo.Sale', 'FlockId') IS NULL
    ALTER TABLE dbo.Sale ADD FlockId INT NULL CONSTRAINT FK_Sale_Flock REFERENCES dbo.Flock(FlockId);
GO
IF COL_LENGTH('dbo.Sale', 'RateBasis') IS NULL
    ALTER TABLE dbo.Sale ADD RateBasis NVARCHAR(10) NOT NULL CONSTRAINT DF_Sale_RateBasis DEFAULT (N'Per Unit');
GO

/* ---------- check constraints (drop + recreate with broiler values) ---------- */
IF OBJECT_ID('dbo.CK_Breed_BirdType') IS NULL
    ALTER TABLE dbo.Breed ADD CONSTRAINT CK_Breed_BirdType CHECK (BirdType IN (N'Layer', N'Broiler'));
IF OBJECT_ID('dbo.CK_Flock_Type') IS NULL
    ALTER TABLE dbo.Flock ADD CONSTRAINT CK_Flock_Type CHECK (FlockType IN (N'Layer', N'Broiler'));
IF OBJECT_ID('dbo.CK_Flock_Lift') IS NULL
    ALTER TABLE dbo.Flock ADD CONSTRAINT CK_Flock_Lift CHECK (TargetLiftDays IS NULL OR TargetLiftDays BETWEEN 20 AND 120);
IF OBJECT_ID('dbo.CK_Company_BrMax') IS NULL
    ALTER TABLE dbo.Company ADD CONSTRAINT CK_Company_BrMax CHECK (BroilerMaxDays BETWEEN 30 AND 120);
IF OBJECT_ID('dbo.CK_Sale_RateBasis') IS NULL
    ALTER TABLE dbo.Sale ADD CONSTRAINT CK_Sale_RateBasis CHECK (RateBasis IN (N'Per Unit', N'Per Kg'));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Shed_Purpose' AND definition LIKE '%Broiler%')
BEGIN
    IF OBJECT_ID('dbo.CK_Shed_Purpose') IS NOT NULL ALTER TABLE dbo.Shed DROP CONSTRAINT CK_Shed_Purpose;
    ALTER TABLE dbo.Shed ADD CONSTRAINT CK_Shed_Purpose CHECK (Purpose IN (N'Brooding', N'Grower', N'Laying', N'Multi-stage', N'Broiler'));
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Item_Stage' AND definition LIKE '%Broiler%')
BEGIN
    IF OBJECT_ID('dbo.CK_Item_Stage') IS NOT NULL ALTER TABLE dbo.Item DROP CONSTRAINT CK_Item_Stage;
    ALTER TABLE dbo.Item ADD CONSTRAINT CK_Item_Stage CHECK (FeedStage IS NULL OR FeedStage IN (N'Chick', N'Grower', N'Pre-Lay', N'Layer', N'Broiler'));
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Sale_Product' AND definition LIKE '%Broiler Bird%')
BEGIN
    IF OBJECT_ID('dbo.CK_Sale_Product') IS NOT NULL ALTER TABLE dbo.Sale DROP CONSTRAINT CK_Sale_Product;
    ALTER TABLE dbo.Sale ADD CONSTRAINT CK_Sale_Product CHECK (Product IN (N'Table Egg', N'Cracked Egg', N'Double Yolk', N'Misshapen Egg',
                                                                         N'Cull Bird', N'Broiler Bird', N'Manure', N'Other'));
END
GO
/* Sale amount: per Kg for broilers (live weight x rate), per unit otherwise */
IF NOT EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('dbo.Sale') AND name = 'Amount' AND definition LIKE '%RateBasis%')
BEGIN
    ALTER TABLE dbo.Sale DROP COLUMN Amount;
    EXEC ('ALTER TABLE dbo.Sale ADD Amount AS (CAST(CASE WHEN RateBasis = N''Per Kg'' THEN ISNULL(QtyKg, 0) ELSE Qty END * Rate AS DECIMAL(14,2))) PERSISTED');
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sale_Flock')
    CREATE INDEX IX_Sale_Flock ON dbo.Sale (FlockId, SaleDate) WHERE FlockId IS NOT NULL;
GO

/* ---------- ledger: broiler sales (lifting) reduce the shed's birds ---------- */
CREATE OR ALTER VIEW dbo.vBirdMovement
AS
    SELECT p.CompanyId, p.ShedId, p.FlockId, p.PlaceDate AS MoveDate,
           CAST(N'PLACE' AS NVARCHAR(10)) AS Kind, p.BirdCount AS Qty
    FROM dbo.FlockPlacement p
    UNION ALL
    SELECT t.CompanyId, t.FromShedId, t.FlockId, t.TransferDate, N'TOUT', -t.BirdsDispatched
    FROM dbo.FlockTransfer t WHERE t.Status = N'Approved'
    UNION ALL
    SELECT t.CompanyId, t.ToShedId, t.FlockId, t.TransferDate, N'TIN', t.BirdsReceived
    FROM dbo.FlockTransfer t WHERE t.Status = N'Approved' AND t.BirdsReceived > 0
    UNION ALL
    SELECT d.CompanyId, d.ShedId, d.FlockId, d.LogDate, N'MORT', -d.Mortality
    FROM dbo.DailyLog d WHERE d.Mortality > 0
    UNION ALL
    SELECT d.CompanyId, d.ShedId, d.FlockId, d.LogDate, N'CULL', -d.Culls
    FROM dbo.DailyLog d WHERE d.Culls > 0
    UNION ALL
    SELECT d.CompanyId, d.ShedId, d.FlockId, d.LogDate, N'SALE', -d.BirdsSold
    FROM dbo.DailyLog d WHERE d.BirdsSold > 0
    UNION ALL
    SELECT s.CompanyId, s.ShedId, s.FlockId, s.SaleDate, N'SALE', -CAST(s.Qty AS INT)
    FROM dbo.Sale s WHERE s.Product = N'Broiler Bird' AND s.FlockId IS NOT NULL AND s.ShedId IS NOT NULL;
GO

/* ---------- broiler masters for every company (only if missing) ---------- */
INSERT INTO dbo.Breed (CompanyId, Name, StrainCode, BreederCompany, Notes, BirdType)
SELECT c.CompanyId, N'Generic Broiler', N'GBR', N'Sample',
       N'Sample broiler curve for testing. Replace with your breeder''s performance objectives (Masters > Breed Standards).', N'Broiler'
FROM dbo.Company c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Breed b WHERE b.CompanyId = c.CompanyId AND b.BirdType = N'Broiler');

/* weekly broiler standard: body weight / cumulative feed at the END of each completed week, daily intake during that week */
INSERT INTO dbo.BreedStandard (CompanyId, BreedId, AgeWeek, BodyWtG, FeedGBirdDay, CumFeedG, StdFCR, LivabilityPct)
SELECT b.CompanyId, b.BreedId, v.Wk, v.Bw, v.Fd, v.Cum, CASE WHEN v.Wk = 0 THEN NULL ELSE CAST(v.Cum * 1.0 / v.Bw AS DECIMAL(6,3)) END, v.Liv
FROM dbo.Breed b
CROSS JOIN (VALUES (0,   42,  24,     0, 100.0), (1,  190,  62,   168, 99.0), (2,  500, 105,   602, 98.6),
                   (3,  960, 150,  1337,  98.2), (4, 1550, 185,  2387, 97.8), (5, 2200, 210,  3682, 97.3),
                   (6, 2850, 225,  5152,  96.8), (7, 3420, 235,  6727, 96.3), (8, 3900, 240,  8372, 95.8),
                   (9, 4280, 242, 10052,  95.3), (10, 4580, 242, 11746, 94.8)) v(Wk, Bw, Fd, Cum, Liv)
WHERE b.BirdType = N'Broiler' AND b.Name = N'Generic Broiler'
  AND NOT EXISTS (SELECT 1 FROM dbo.BreedStandard s WHERE s.BreedId = b.BreedId);

INSERT INTO dbo.Item (CompanyId, Code, Name, Category, Unit, FeedStage)
SELECT c.CompanyId, v.Code, v.Name, N'FEED', N'KG', N'Broiler'
FROM dbo.Company c
CROSS JOIN (VALUES (N'BPS', N'Broiler Pre-Starter'), (N'BST', N'Broiler Starter'), (N'BFN', N'Broiler Finisher')) v(Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Item i WHERE i.CompanyId = c.CompanyId AND i.Code = v.Code);

INSERT INTO dbo.ItemRate (CompanyId, ItemId, EffectiveFrom, Rate, Remarks)
SELECT i.CompanyId, i.ItemId, DATEADD(YEAR, -2, CAST(GETDATE() AS DATE)), v.Rate, N'Opening rate'
FROM dbo.Item i JOIN (VALUES (N'BPS', 42.00), (N'BST', 39.50), (N'BFN', 37.00)) v(Code, Rate) ON v.Code = i.Code
WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemRate r WHERE r.ItemId = i.ItemId);

/* typical broiler vaccination programme, tied to the broiler breed */
INSERT INTO dbo.VaccineSchedule (CompanyId, BreedId, AgeDay, ItemId, Route, DosePerBird, Notes)
SELECT b.CompanyId, b.BreedId, v.AgeDay, i.ItemId, v.Route, 1, v.Notes
FROM dbo.Breed b
CROSS JOIN (VALUES (7, N'NDIB', N'Eye drop', N'ND + IB (broiler)'), (14, N'IBD', N'Drinking water', N'Gumboro (broiler)'),
                   (21, N'NDIB', N'Drinking water', N'ND booster (broiler)')) v(AgeDay, Code, Route, Notes)
JOIN dbo.Item i ON i.CompanyId = b.CompanyId AND i.Code = v.Code
WHERE b.BirdType = N'Broiler' AND b.Name = N'Generic Broiler'
  AND NOT EXISTS (SELECT 1 FROM dbo.VaccineSchedule s WHERE s.BreedId = b.BreedId);
GO
PRINT 'Broiler module installed.';
GO
