/* =====================================================================
   OPTIONAL BROILER DEMO DATA  -  run after 05_Broiler.sql, TEST database only.
   Creates "Phoenix Broiler Farm" with 3 sheds and 3 batches:
     BR-01 completed (lifted day 38-44, closed), BR-02 running (~day 30), BR-03 placed 8 days ago.
   ===================================================================== */
USE PoultryERP;
GO
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Demo_SimulateBroiler @FlockId INT, @ShedId INT, @LiftFrom INT = NULL, @LiftTo INT = NULL, @CustomerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @C INT, @Breed INT, @Hatch DATE, @Arrival DATE, @Birds INT, @Farm INT;
    SELECT @C = CompanyId, @Breed = BreedId, @Hatch = HatchDate, @Arrival = ArrivalDate, @Birds = InitialCount - ArrivalMortality
    FROM dbo.Flock WHERE FlockId = @FlockId;
    SELECT @Farm = l.FarmId FROM dbo.Shed s JOIN dbo.Location l ON l.LocationId = s.LocationId WHERE s.ShedId = @ShedId;
    INSERT INTO dbo.FlockPlacement (CompanyId, FlockId, ShedId, PlaceDate, BirdCount, Remarks)
    VALUES (@C, @FlockId, @ShedId, @Arrival, @Birds, N'Day-old chicks placed');

    DECLARE @d DATE = @Arrival, @End DATE = DATEADD(DAY, -1, CAST(GETDATE() AS DATE));
    DECLARE @Age INT, @Wk INT, @Mort INT, @Feed DECIMAL(10,3), @FeedG DECIMAL(8,1), @Bw0 DECIMAL(8,1), @Bw1 DECIMAL(8,1), @Bw DECIMAL(8,1);
    DECLARE @Item INT, @Lift INT, @Kg DECIMAL(12,2), @Tmin DECIMAL(5,1);
    WHILE @d <= @End AND @Birds > 0
    BEGIN
        SET @Age = DATEDIFF(DAY, @Hatch, @d);
        SET @Wk = CASE WHEN @Age / 7 > 10 THEN 10 ELSE @Age / 7 END;
        SELECT @FeedG = FeedGBirdDay, @Bw0 = BodyWtG FROM dbo.BreedStandard WHERE BreedId = @Breed AND AgeWeek = @Wk;
        SELECT @Bw1 = BodyWtG FROM dbo.BreedStandard WHERE BreedId = @Breed AND AgeWeek = CASE WHEN @Wk >= 10 THEN 10 ELSE @Wk + 1 END;
        SET @Bw = @Bw0 + (@Bw1 - @Bw0) * (@Age % 7) / 7.0;
        SET @Mort = ROUND(@Birds * CASE WHEN @Age <= 7 THEN 0.0018 ELSE 0.0007 END * (0.3 + 1.4 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0), 0);
        SET @Feed = @Birds * @FeedG / 1000.0 * (0.96 + 0.08 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0);
        SELECT @Item = ItemId FROM dbo.Item WHERE CompanyId = @C AND Code = CASE WHEN @Age <= 10 THEN N'BPS' WHEN @Age <= 24 THEN N'BST' ELSE N'BFN' END;
        SET @Tmin = 24 + 6 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0;

        INSERT INTO dbo.DailyLog (CompanyId, ShedId, FlockId, LogDate, Mortality, Culls, FeedItemId, FeedKg, AvgBodyWtG, WaterLtr, TempMinC, TempMaxC, HumidityPct)
        VALUES (@C, @ShedId, @FlockId, @d, @Mort, CASE WHEN @Age % 9 = 0 THEN 2 ELSE 0 END, @Item, @Feed,
                CASE WHEN @Age % 3 = 0 AND @Age > 0 THEN @Bw * (0.95 + 0.08 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0) END,
                @Feed * 1.9, @Tmin, @Tmin + 7, 55 + 20 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0);
        SET @Birds = @Birds - @Mort - CASE WHEN @Age % 9 = 0 THEN 2 ELSE 0 END;

        IF @LiftFrom IS NOT NULL AND @Age BETWEEN @LiftFrom AND @LiftTo
        BEGIN
            SET @Lift = CASE WHEN @Age = @LiftTo THEN @Birds ELSE @Birds / (@LiftTo - @Age + 1) END;
            SET @Kg = @Lift * @Bw / 1000.0 * (0.97 + 0.06 * (ABS(CHECKSUM(NEWID())) % 1000) / 1000.0);
            INSERT INTO dbo.Sale (CompanyId, FarmId, SaleDate, CustomerId, InvoiceNo, Product, ShedId, FlockId, Qty, QtyKg, Rate, RateBasis, AmountReceived, Remarks)
            VALUES (@C, @Farm, @d, @CustomerId, N'BR-' + CONVERT(NVARCHAR(8), @d, 112), N'Broiler Bird', @ShedId, @FlockId, @Lift, @Kg,
                    96 + (@Age % 5) * 2, N'Per Kg', CAST(@Kg * (96 + (@Age % 5) * 2) AS DECIMAL(14,2)), N'Vehicle demo');
            SET @Birds = @Birds - @Lift;
            IF @Birds = 0 UPDATE dbo.Flock SET Status = N'Depleted', DepletionDate = @d WHERE FlockId = @FlockId;
        END
        SET @d = DATEADD(DAY, 1, @d);
    END
END;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @C INT = (SELECT CompanyId FROM dbo.Company WHERE Code = N'PHX');
DECLARE @Breed INT = (SELECT TOP (1) BreedId FROM dbo.Breed WHERE CompanyId = @C AND BirdType = N'Broiler');
DECLARE @Today DATE = CAST(GETDATE() AS DATE), @Farm INT, @Loc INT, @Cust INT, @Hatchery INT;

INSERT INTO dbo.Farm (CompanyId, Code, Name, State, District, Village, Latitude, Longitude, ManagerName)
VALUES (@C, N'BR', N'Phoenix Broiler Farm', N'Haryana', N'Karnal', N'Demo Village', 29.701200, 77.012300, N'Broiler Manager');
SET @Farm = SCOPE_IDENTITY();
INSERT INTO dbo.Location (CompanyId, FarmId, Name, Description) VALUES (@C, @Farm, N'Broiler Block', N'Open-sided broiler houses');
SET @Loc = SCOPE_IDENTITY();
INSERT INTO dbo.Shed (CompanyId, LocationId, Code, Name, Capacity, Purpose, VentilationType, HousingSystem, Status, LengthFt, WidthFt) VALUES
 (@C, @Loc, N'BR-1', N'Broiler House 1', 6000, N'Broiler', N'Open-sided', N'Deep Litter', N'Active', 250, 40),
 (@C, @Loc, N'BR-2', N'Broiler House 2', 6000, N'Broiler', N'Open-sided', N'Deep Litter', N'Active', 250, 40),
 (@C, @Loc, N'BR-3', N'Broiler House 3', 6000, N'Broiler', N'Environment Controlled', N'Deep Litter', N'Active', 250, 40);
INSERT INTO dbo.Customer (CompanyId, Name) VALUES (@C, N'Fresh Meat Traders (Demo)');
SET @Cust = SCOPE_IDENTITY();
SET @Hatchery = (SELECT TOP (1) SupplierId FROM dbo.Supplier WHERE CompanyId = @C AND SupplierType = N'Hatchery');

DECLARE @S1 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId = @C AND Code = N'BR-1'),
        @S2 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId = @C AND Code = N'BR-2'),
        @S3 INT = (SELECT ShedId FROM dbo.Shed WHERE CompanyId = @C AND Code = N'BR-3'), @F1 INT, @F2 INT, @F3 INT;

INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, FlockType, TargetLiftDays, PlannedDepletionWeek)
VALUES (@C, N'BR-01', N'BB-201', @Hatchery, @Breed, DATEADD(DAY,-75,@Today), DATEADD(DAY,-75,@Today), 5200, 20, 34.00, 3000, N'Broiler', 42, 30);
SET @F1 = SCOPE_IDENTITY();
INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, FlockType, TargetLiftDays, PlannedDepletionWeek)
VALUES (@C, N'BR-02', N'BB-214', @Hatchery, @Breed, DATEADD(DAY,-30,@Today), DATEADD(DAY,-30,@Today), 5500, 15, 36.00, 3100, N'Broiler', 42, 30);
SET @F2 = SCOPE_IDENTITY();
INSERT INTO dbo.Flock (CompanyId, FlockCode, BatchNo, SupplierId, BreedId, HatchDate, ArrivalDate, InitialCount, ArrivalMortality, ChickRate, FreightCost, FlockType, TargetLiftDays, PlannedDepletionWeek)
VALUES (@C, N'BR-03', N'BB-221', @Hatchery, @Breed, DATEADD(DAY,-8,@Today), DATEADD(DAY,-8,@Today), 5400, 25, 38.00, 3100, N'Broiler', 40, 30);
SET @F3 = SCOPE_IDENTITY();

EXEC dbo.usp_Demo_SimulateBroiler @F1, @S1, 38, 44, @Cust;
EXEC dbo.usp_Demo_SimulateBroiler @F2, @S2;
EXEC dbo.usp_Demo_SimulateBroiler @F3, @S3;
UPDATE dbo.Shed SET Status = N'Vacant' WHERE ShedId = @S1;

/* monthly overheads for the broiler farm (allocated to the batches on bird-days) */
DECLARE @m DATE = DATEFROMPARTS(YEAR(DATEADD(MONTH,-3,@Today)), MONTH(DATEADD(MONTH,-3,@Today)), 1);
WHILE @m <= @Today
BEGIN
    INSERT INTO dbo.Overhead (CompanyId, FarmId, ShedId, CostHead, FromDate, ToDate, Amount, Description) VALUES
     (@C, @Farm, NULL, N'Labor',   @m, EOMONTH(@m), 48000, N'Broiler farm wages'),
     (@C, @Farm, NULL, N'Utility', @m, EOMONTH(@m), 22000, N'Electricity, brooding gas, litter');
    SET @m = DATEADD(MONTH, 1, @m);
END

/* vaccinations from the broiler schedule */
INSERT INTO dbo.MedicationLog (CompanyId, ShedId, FlockId, LogDate, Kind, ItemId, Qty, Dose, Route, Reason, AdministeredBy, Status, ApprovalStatus, ApprovedOn)
SELECT f.CompanyId, dl.ShedId, f.FlockId, dl.LogDate, N'Vaccination', vs.ItemId, f.InitialCount, N'1 dose/bird', vs.Route, vs.Notes, N'Shed team', N'Done', N'Approved', SYSDATETIME()
FROM dbo.Flock f JOIN dbo.VaccineSchedule vs ON vs.BreedId = f.BreedId
JOIN dbo.DailyLog dl ON dl.FlockId = f.FlockId AND dl.LogDate = DATEADD(DAY, vs.AgeDay, f.HatchDate)
WHERE f.CompanyId = @C AND f.FlockType = N'Broiler';
COMMIT TRANSACTION;
GO
DROP PROCEDURE dbo.usp_Demo_SimulateBroiler;
GO
PRINT 'Broiler demo data created.';
GO
