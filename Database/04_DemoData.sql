/* =====================================================================
   OPTIONAL DEMO DATA  -  run after 03_Seed.sql on a TEST database only.
   Creates 1 farm, 7 sheds, 4 flocks at different stages with complete
   daily logs up to yesterday, transfers, vaccinations, overheads, sales.
   Extra demo logins (password Phoenix@123):
       super1 (Supervisor - laying sheds), vet (Veterinarian), accounts (Accountant)
   ===================================================================== */
USE PoultryERP;
GO
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Demo_SimulateFlock
    @FlockId INT, @Shed1 INT, @Shed2 INT = NULL, @Day2 INT = NULL, @Shed3 INT = NULL, @Day3 INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CompanyId INT, @BreedId INT, @Hatch DATE, @Arrival DATE, @Birds INT;
    SELECT @CompanyId = CompanyId, @BreedId = BreedId, @Hatch = HatchDate, @Arrival = ArrivalDate,
           @Birds = InitialCount - ArrivalMortality
    FROM dbo.Flock WHERE FlockId = @FlockId;

    INSERT INTO dbo.FlockPlacement (CompanyId, FlockId, ShedId, PlaceDate, BirdCount, Remarks)
    VALUES (@CompanyId, @FlockId, @Shed1, @Arrival, @Birds, N'Initial placement on arrival');

    DECLARE @d DATE = @Arrival, @End DATE = DATEADD(DAY, -1, CAST(GETDATE() AS DATE));
    DECLARE @Shed INT = @Shed1, @Age INT, @Wk INT, @Mort INT, @Cull INT, @TM INT, @TC INT;
    DECLARE @StdFeed DECIMAL(8,1), @StdHD DECIMAL(5,2), @StdBW DECIMAL(8,1), @StdEW DECIMAL(6,2);
    DECLARE @FeedItem INT, @Grit INT, @FeedKg DECIMAL(10,3), @GritKg DECIMAL(10,3);
    DECLARE @Tot INT, @Crk INT, @Mis INT, @DY INT, @Tmin DECIMAL(5,1);
    DECLARE @Rate FLOAT;
    SELECT @Grit = ItemId FROM dbo.Item WHERE CompanyId = @CompanyId AND Code = N'SG';

    WHILE @d <= @End
    BEGIN
        SET @Age = DATEDIFF(DAY, @Hatch, @d);
        SET @Wk  = @Age / 7;

        IF (@Shed2 IS NOT NULL AND @Age = @Day2) OR (@Shed3 IS NOT NULL AND @Age = @Day3)
        BEGIN
            SET @TM = CEILING(@Birds * 0.0015);
            SET @TC = CEILING(@Birds * 0.0005);
            INSERT INTO dbo.FlockTransfer (CompanyId, FlockId, FromShedId, ToShedId, TransferDate, BirdsDispatched,
                        TransitMortality, TransitCulls, IsFullTransfer, Status, Remarks, ApprovedOn, ApprovalNote)
            VALUES (@CompanyId, @FlockId, @Shed, CASE WHEN @Age = @Day2 THEN @Shed2 ELSE @Shed3 END, @d, @Birds,
                    @TM, @TC, 1, N'Approved', N'Stage shifting', SYSDATETIME(), N'Demo');
            SET @Birds = @Birds - @TM - @TC;
            SET @Shed = CASE WHEN @Age = @Day2 THEN @Shed2 ELSE @Shed3 END;
        END

        SELECT @StdFeed = FeedGBirdDay, @StdHD = HDPPct, @StdBW = BodyWtG, @StdEW = EggWtG
        FROM dbo.BreedStandard WHERE BreedId = @BreedId AND AgeWeek = CASE WHEN @Wk > 80 THEN 80 ELSE @Wk END;

        SET @Rate = CASE WHEN @Age <= 7 THEN 0.0040 WHEN @Age <= 21 THEN 0.0012
                         WHEN @Wk < 18 THEN 0.0004 ELSE 0.00025 END
                    * (0.3 + 1.4 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0);
        SET @Mort = ROUND(@Birds * @Rate, 0);
        SET @Cull = CASE WHEN ABS(CHECKSUM(NEWID())) % 10 = 0 THEN CEILING(@Birds * 0.0004) ELSE 0 END;

        SELECT @FeedItem = ItemId FROM dbo.Item WHERE CompanyId = @CompanyId AND Code =
            CASE WHEN @Wk <= 8 THEN N'CSF' WHEN @Wk <= 15 THEN N'GRF' WHEN @Wk <= 18 THEN N'PLF'
                 WHEN @Wk <= 45 THEN N'LF1' ELSE N'LF2' END;
        SET @FeedKg = @Birds * ISNULL(@StdFeed, 100) / 1000.0 * (0.96 + 0.08 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0);
        SET @GritKg = CASE WHEN @Wk >= 17 THEN @Birds * 3.0 / 1000.0 * (0.9 + 0.2 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0) ELSE 0 END;

        IF @Wk >= 18 AND @StdHD IS NOT NULL
        BEGIN
            SET @Tot = ROUND(@Birds * @StdHD / 100.0 * (0.95 + 0.06 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0), 0);
            SET @Crk = ROUND(@Tot * 0.015 * (0.5 + (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0), 0);
            SET @Mis = ROUND(@Tot * 0.006, 0);
            SET @DY  = ROUND(@Tot * 0.004 * (0.5 + (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0), 0);
        END
        ELSE SELECT @Tot = 0, @Crk = 0, @Mis = 0, @DY = 0;

        SET @Tmin = 18 + 7 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0;

        INSERT INTO dbo.DailyLog (CompanyId, ShedId, FlockId, LogDate, Mortality, Culls, BirdsSold,
                    FeedItemId, FeedKg, GritItemId, GritKg, GritTime, TableEggs, CrackedEggs, MisshapenEggs, DoubleYolkEggs,
                    AvgBodyWtG, AvgEggWtG, WaterLtr, TempMinC, TempMaxC, HumidityPct, Remarks)
        VALUES (@CompanyId, @Shed, @FlockId, @d, @Mort, @Cull, 0,
                @FeedItem, @FeedKg, CASE WHEN @GritKg > 0 THEN @Grit END, @GritKg, CASE WHEN @GritKg > 0 THEN CAST('15:30' AS TIME) END,
                @Tot - @Crk - @Mis - @DY, @Crk, @Mis, @DY,
                CASE WHEN @Age % 7 = 0 THEN @StdBW * (0.95 + 0.07 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0) END,
                CASE WHEN @Age % 7 = 0 AND @Wk >= 19 THEN @StdEW * (0.97 + 0.05 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0) END,
                @FeedKg * 2.1 * (0.9 + 0.2 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0),
                @Tmin, @Tmin + 8 + 4 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0,
                45 + 30 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0, NULL);

        SET @Birds = @Birds - @Mort - @Cull;
        SET @d = DATEADD(DAY, 1, @d);
    END
END;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @C INT = (SELECT CompanyId FROM dbo.Company WHERE Code = N'PHX');
DECLARE @Breed INT = (SELECT TOP (1) BreedId FROM dbo.Breed WHERE CompanyId = @C ORDER BY BreedId);
DECLARE @Today DATE = CAST(GETDATE() AS DATE);
DECLARE @Farm INT, @LocB INT, @LocL INT, @Hatchery INT, @Cust1 INT, @Cust2 INT;

INSERT INTO dbo.Farm (CompanyId, Code, Name, State, District, Village, Latitude, Longitude, ManagerName, OpeningEggDate)
VALUES (@C, N'U1', N'Phoenix Layer Farm - Unit 1', N'Haryana', N'Karnal', N'Demo Village', 29.685600, 76.990500, N'Farm Manager', DATEADD(DAY, -500, @Today));
SET @Farm = SCOPE_IDENTITY();
INSERT INTO dbo.Location (CompanyId, FarmId, Name, Description) VALUES (@C, @Farm, N'Brooding & Grower Block', N'Chick and grower sheds');
SET @LocB = SCOPE_IDENTITY();
INSERT INTO dbo.Location (CompanyId, FarmId, Name, Description) VALUES (@C, @Farm, N'Laying Block', N'Cage layer sheds');
SET @LocL = SCOPE_IDENTITY();

INSERT INTO dbo.Shed (CompanyId, LocationId, Code, Name, Capacity, Purpose, VentilationType, HousingSystem, Status, LengthFt, WidthFt) VALUES
 (@C, @LocB, N'CH-1',  N'Chick Shed 1',  12000, N'Brooding', N'Open-sided', N'Deep Litter', N'Active', 200, 40),
 (@C, @LocB, N'CH-2',  N'Chick Shed 2',  12000, N'Brooding', N'Open-sided', N'Deep Litter', N'Active', 200, 40),
 (@C, @LocB, N'GR-1',  N'Grower Shed 1', 11000, N'Grower',   N'Open-sided', N'Cage',  N'Active', 250, 40),
 (@C, @LocB, N'GR-2',  N'Grower Shed 2', 11000, N'Grower',   N'Open-sided', N'Cage',  N'Active', 250, 40),
 (@C, @LocL, N'LHD-1', N'Layer House 1', 10500, N'Laying',   N'Environment Controlled', N'Cage', N'Active', 300, 42),
 (@C, @LocL, N'LHD-2', N'Layer House 2', 10000, N'Laying',   N'Open-sided', N'Cage', N'Active', 300, 40),
 (@C, @LocL, N'LHD-3', N'Layer House 3', 10000, N'Laying',   N'Open-sided', N'Cage', N'Cleaning', 300, 40);

INSERT INTO dbo.Supplier (CompanyId, Name, SupplierType, ContactPerson, Phone) VALUES
 (@C, N'Phoenix Hatchery', N'Hatchery', N'Hatchery Desk', NULL),
 (@C, N'Feed Mill (Demo)', N'Feed', NULL, NULL);
SET @Hatchery = (SELECT SupplierId FROM dbo.Supplier WHERE CompanyId = @C AND Name = N'Phoenix Hatchery');
INSERT INTO dbo.Customer (CompanyId, Name, Phone) VALUES (@C, N'City Egg Traders (Demo)', NULL);
SET @Cust1 = SCOPE_IDENTITY();
INSERT INTO dbo.Customer (CompanyId, Name, Phone) VALUES (@C, N'Bakery Supplies (Demo)', NULL);
SET @Cust2 = SCOPE_IDENTITY();

DECLARE @CH1 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'CH-1'),
        @CH2 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'CH-2'),
        @GR1 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'GR-1'),
        @GR2 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'GR-2'),
        @L1  INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'LHD-1'),
        @L2  INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'LHD-2'),
        @L3  INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId=@C AND Code=N'LHD-3');
DECLARE @FA INT, @FB INT, @FC INT, @FD INT;

INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, LotNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, ExpectedSalvagePerBird, PlannedDepletionWeek)
VALUES (@C, N'FLK-A', N'B-101', N'L-7781', @Hatchery, @Breed, DATEADD(DAY,-280,@Today), DATEADD(DAY,-279,@Today), 10200, 50, 38.00, 6500, 85, 80);
SET @FA = SCOPE_IDENTITY();
INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, LotNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, ExpectedSalvagePerBird, PlannedDepletionWeek)
VALUES (@C, N'FLK-B', N'B-114', N'L-8120', @Hatchery, @Breed, DATEADD(DAY,-91,@Today), DATEADD(DAY,-90,@Today), 8000, 30, 40.00, 5200, 85, 80);
SET @FB = SCOPE_IDENTITY();
INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, LotNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, ExpectedSalvagePerBird, PlannedDepletionWeek)
VALUES (@C, N'FLK-C', N'B-121', N'L-8410', @Hatchery, @Breed, DATEADD(DAY,-20,@Today), DATEADD(DAY,-19,@Today), 9000, 40, 41.00, 5600, 85, 80);
SET @FC = SCOPE_IDENTITY();
INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, LotNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, ExpectedSalvagePerBird, PlannedDepletionWeek, Remarks)
VALUES (@C, N'FLK-D', N'P-090', N'L-6650', @Hatchery, @Breed, DATEADD(DAY,-455,@Today), DATEADD(DAY,-343,@Today), 9500, 0, 310.00, 9000, 85, 78, N'Purchased as 16-week pullets');
SET @FD = SCOPE_IDENTITY();

EXEC dbo.usp_Demo_SimulateFlock @FA, @CH1, @GR1, 56, @L1, 112;
EXEC dbo.usp_Demo_SimulateFlock @FB, @CH2, @GR2, 56;
EXEC dbo.usp_Demo_SimulateFlock @FC, @CH1;
EXEC dbo.usp_Demo_SimulateFlock @FD, @L2;

/* pending request so the approval screen has something to show */
INSERT INTO dbo.FlockTransfer (CompanyId, FlockId, FromShedId, ToShedId, TransferDate, BirdsDispatched, Status, Remarks)
VALUES (@C, @FD, @L2, @L3, @Today, 300, N'Pending', N'Thin out LHD-2 (demo request)');

/* vaccinations from the schedule, at the shed the flock was in on that day */
INSERT INTO dbo.MedicationLog (CompanyId, ShedId, FlockId, LogDate, Kind, ItemId, Qty, Dose, Route, Reason, AdministeredBy, Status, ApprovalStatus, ApprovedOn)
SELECT f.CompanyId, dl.ShedId, f.FlockId, dl.LogDate, N'Vaccination', vs.ItemId, f.InitialCount * vs.DosePerBird, N'1 dose/bird',
       vs.Route, vs.Notes, N'Shed team', N'Done', N'Approved', SYSDATETIME()
FROM dbo.Flock f
JOIN dbo.VaccineSchedule vs ON vs.CompanyId = f.CompanyId
JOIN dbo.DailyLog dl ON dl.FlockId = f.FlockId AND dl.LogDate = DATEADD(DAY, vs.AgeDay, f.HatchDate)
WHERE f.CompanyId = @C;

/* fortnightly multivitamin in water */
INSERT INTO dbo.MedicationLog (CompanyId, ShedId, FlockId, LogDate, Kind, ItemId, Qty, Dose, Route, Reason, AdministeredBy, Status)
SELECT dl.CompanyId, dl.ShedId, dl.FlockId, dl.LogDate, N'Medication', i.ItemId, 250, N'1 ml / 4 L water', N'Drinking water',
       N'Routine supplement', N'Shed team', N'Done'
FROM dbo.DailyLog dl JOIN dbo.Item i ON i.CompanyId = dl.CompanyId AND i.Code = N'MULTV'
WHERE dl.CompanyId = @C AND DATEDIFF(DAY, '2000-01-01', dl.LogDate) % 14 = 0;

/* monthly overheads for the last 16 months (farm level - allocated on bird-days) */
DECLARE @m DATE = DATEFROMPARTS(YEAR(DATEADD(MONTH,-15,@Today)), MONTH(DATEADD(MONTH,-15,@Today)), 1);
WHILE @m <= @Today
BEGIN
    INSERT INTO dbo.Overhead (CompanyId, FarmId, ShedId, CostHead, FromDate, ToDate, Amount, Description) VALUES
     (@C, @Farm, NULL, N'Labor',    @m, EOMONTH(@m), 145000, N'Salaries & wages'),
     (@C, @Farm, NULL, N'Utility',  @m, EOMONTH(@m),  52000, N'Electricity & diesel'),
     (@C, @Farm, NULL, N'Overhead', @m, EOMONTH(@m),  21000, N'Repairs, admin, insurance');
    SET @m = DATEADD(MONTH, 1, @m);
END

/* egg sales: next-day sale of table + double-yolk eggs, cracked sold separately */
INSERT INTO dbo.Sale (CompanyId, FarmId, SaleDate, CustomerId, InvoiceNo, Product, Qty, Rate, AmountReceived)
SELECT @C, @Farm, DATEADD(DAY,1,x.LogDate), CASE WHEN DAY(x.LogDate) % 3 = 0 THEN @Cust2 ELSE @Cust1 END,
       N'TE-' + CONVERT(NVARCHAR(8), x.LogDate, 112), N'Table Egg', x.Q, x.R,
       CASE WHEN x.LogDate < DATEADD(DAY,-10,@Today) THEN CAST(x.Q * x.R AS DECIMAL(14,2)) ELSE 0 END
FROM (SELECT LogDate, FLOOR(SUM(TableEggs + DoubleYolkEggs) * 0.985) AS Q,
             CAST(5.10 + (DATEPART(DAYOFYEAR, LogDate) % 12) * 0.05 AS DECIMAL(12,4)) AS R
      FROM dbo.DailyLog WHERE CompanyId = @C GROUP BY LogDate HAVING SUM(TableEggs + DoubleYolkEggs) > 10) x
WHERE DATEADD(DAY,1,x.LogDate) < @Today;

INSERT INTO dbo.Sale (CompanyId, FarmId, SaleDate, CustomerId, InvoiceNo, Product, Qty, Rate, AmountReceived)
SELECT @C, @Farm, DATEADD(DAY,1,LogDate), @Cust2, N'CE-' + CONVERT(NVARCHAR(8), LogDate, 112), N'Cracked Egg',
       SUM(CrackedEggs), 3.00, SUM(CrackedEggs) * 3.00
FROM dbo.DailyLog WHERE CompanyId = @C AND LogDate < DATEADD(DAY,-1,@Today)
GROUP BY LogDate HAVING SUM(CrackedEggs) > 0;

INSERT INTO dbo.EggWastage (CompanyId, FarmId, WasteDate, Qty, Reason)
SELECT @C, @Farm, LogDate, CEILING(SUM(MisshapenEggs) * 0.9), N'Misshapen / thin shell discarded'
FROM dbo.DailyLog WHERE CompanyId = @C GROUP BY LogDate HAVING SUM(MisshapenEggs) > 0;

/* monthly feed purchases roughly equal to consumption */
INSERT INTO dbo.StockReceipt (CompanyId, FarmId, ItemId, ReceiptDate, SupplierId, InvoiceNo, Qty, Rate)
SELECT @C, @Farm, x.FeedItemId, x.M, (SELECT SupplierId FROM dbo.Supplier WHERE CompanyId=@C AND Name=N'Feed Mill (Demo)'),
       N'FM-' + CONVERT(NVARCHAR(6), x.M, 112), CEILING(x.Kg / 1000.0) * 1000, dbo.fn_ItemRate(x.FeedItemId, x.M)
FROM (SELECT FeedItemId, DATEFROMPARTS(YEAR(LogDate), MONTH(LogDate), 1) AS M, SUM(FeedKg) AS Kg
      FROM dbo.DailyLog WHERE CompanyId = @C AND FeedItemId IS NOT NULL
      GROUP BY FeedItemId, DATEFROMPARTS(YEAR(LogDate), MONTH(LogDate), 1)) x;

/* a feed price change two months ago, to show rate history in costing */
INSERT INTO dbo.ItemRate (CompanyId, ItemId, EffectiveFrom, Rate, Remarks)
SELECT @C, ItemId, DATEADD(MONTH,-2,@Today), 29.25, N'Maize price increase'
FROM dbo.Item WHERE CompanyId = @C AND Code = N'LF1';

INSERT INTO dbo.PostMortem (CompanyId, ShedId, PmDate, BirdsExamined, Findings, Diagnosis, ActionTaken, VetName) VALUES
 (@C, @L2, DATEADD(DAY,-12,@Today), 3, N'Pale comb, enlarged liver, mild haemorrhage', N'Suspected fatty liver', N'Review energy level in feed; add liver tonic 5 days', N'Dr. Demo'),
 (@C, @CH1, DATEADD(DAY,-15,@Today), 4, N'Unabsorbed yolk, pasted vent', N'Omphalitis (early chick mortality)', N'Brooder temperature check; electrolytes', N'Dr. Demo');

INSERT INTO dbo.AppUser (CompanyId, UserName, FullName, Role, PasswordHash, MustChangePassword) VALUES
 (@C, N'super1',   N'Shed Supervisor (Layers)', N'Supervisor',   N'PBKDF2$SHA256$100000$KMlbVgVDbjEhhku6FiU58A==$7FR3eY0qTDPduhCxME7eRxvQAI7k3gg+TOtzgbbNSX4=', 0),
 (@C, N'vet',      N'Veterinary Officer',       N'Veterinarian', N'PBKDF2$SHA256$100000$XaqTA8zkH9C7wET2f4kdAw==$J16seH4jn6f0I5d1Dzo/H/iOJNn8lhDXgAl9QU5cd3A=', 0),
 (@C, N'accounts', N'Accounts Officer',         N'Accountant',   N'PBKDF2$SHA256$100000$iHt+WO3gUe3dXBeykwcSvg==$zqPzfxG1/jmZZ3LGtMGTsuA2FrmxIVhB9XmnPBKTHRg=', 0);
INSERT INTO dbo.UserShed (UserId, ShedId)
SELECT u.UserId, s.ShedId FROM dbo.AppUser u CROSS JOIN dbo.Shed s
WHERE u.UserName = N'super1' AND s.CompanyId = @C AND s.Code IN (N'LHD-1', N'LHD-2', N'LHD-3');

COMMIT TRANSACTION;
GO
DROP PROCEDURE dbo.usp_Demo_SimulateFlock;
GO
PRINT 'Demo data created.';
GO
