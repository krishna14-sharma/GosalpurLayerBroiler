/* =====================================================================
   VIEWS & FUNCTIONS
   The bird inventory is not stored as a running balance; it is derived
   from every movement (placement, approved transfer, mortality, culls,
   sales).  Editing a past daily log therefore automatically corrects all
   later opening/closing figures - the same principle as the old
   Trans_Daily table where SUM(No) gave the balance.
   ===================================================================== */
USE PoultryERP;
GO

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
    FROM dbo.DailyLog d WHERE d.BirdsSold > 0;
GO

/* Birds of one flock in one shed at the START of @OnDate (movements dated before it). */
CREATE OR ALTER FUNCTION dbo.fn_BirdsBefore (@ShedId INT, @FlockId INT, @OnDate DATE)
RETURNS INT
AS
BEGIN
    DECLARE @n INT;
    SELECT @n = ISNULL(SUM(Qty), 0) FROM dbo.vBirdMovement
    WHERE ShedId = @ShedId AND FlockId = @FlockId AND MoveDate < @OnDate;
    RETURN @n;
END;
GO

/* Current (all-time) occupancy of every shed, one row per shed+flock with birds. */
CREATE OR ALTER VIEW dbo.vShedOccupancy
AS
    SELECT m.CompanyId, m.ShedId, m.FlockId, SUM(m.Qty) AS Birds
    FROM dbo.vBirdMovement m
    GROUP BY m.CompanyId, m.ShedId, m.FlockId
    HAVING SUM(m.Qty) <> 0;
GO

/* Effective rate of an item on a date (latest EffectiveFrom <= date). */
CREATE OR ALTER FUNCTION dbo.fn_ItemRate (@ItemId INT, @OnDate DATE)
RETURNS DECIMAL(12,4)
AS
BEGIN
    DECLARE @r DECIMAL(12,4);
    SELECT TOP (1) @r = Rate FROM dbo.ItemRate
    WHERE ItemId = @ItemId AND EffectiveFrom <= @OnDate
    ORDER BY EffectiveFrom DESC;
    IF @r IS NULL
        SELECT TOP (1) @r = Rate FROM dbo.ItemRate WHERE ItemId = @ItemId ORDER BY EffectiveFrom ASC;
    RETURN ISNULL(@r, 0);
END;
GO

/* Shed list with location, farm and current flock - used by many screens. */
CREATE OR ALTER VIEW dbo.vShedInfo
AS
    SELECT s.ShedId, s.CompanyId, s.Code, s.Name, s.Capacity, s.Purpose, s.VentilationType,
           s.HousingSystem, s.Status, s.IsActive,
           l.LocationId, l.Name AS LocationName, f.FarmId, f.Code AS FarmCode, f.Name AS FarmName,
           o.FlockId, fl.FlockCode, fl.HatchDate, ISNULL(o.Birds, 0) AS Birds
    FROM dbo.Shed s
    JOIN dbo.Location l ON l.LocationId = s.LocationId
    JOIN dbo.Farm f     ON f.FarmId = l.FarmId
    OUTER APPLY (SELECT TOP (1) v.FlockId, v.Birds FROM dbo.vShedOccupancy v
                 WHERE v.ShedId = s.ShedId AND v.Birds > 0 ORDER BY v.Birds DESC) o
    LEFT JOIN dbo.Flock fl ON fl.FlockId = o.FlockId;
GO

/* Medicine / feed stock: receipts minus consumption (daily feed, grit, medication). */
CREATE OR ALTER VIEW dbo.vStockMovement
AS
    SELECT r.CompanyId, r.FarmId, r.ItemId, r.ReceiptDate AS MoveDate, N'RECEIPT' AS Kind, r.Qty, r.Rate
    FROM dbo.StockReceipt r
    UNION ALL
    SELECT d.CompanyId, l.FarmId, d.FeedItemId, d.LogDate, N'FEED', -d.FeedKg, NULL
    FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
    WHERE d.FeedItemId IS NOT NULL AND d.FeedKg > 0
    UNION ALL
    SELECT d.CompanyId, l.FarmId, d.GritItemId, d.LogDate, N'GRIT', -d.GritKg, NULL
    FROM dbo.DailyLog d JOIN dbo.Shed s ON s.ShedId = d.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
    WHERE d.GritItemId IS NOT NULL AND d.GritKg > 0
    UNION ALL
    SELECT m.CompanyId, l.FarmId, m.ItemId, m.LogDate, N'MEDICATION', -m.Qty, NULL
    FROM dbo.MedicationLog m JOIN dbo.Shed s ON s.ShedId = m.ShedId JOIN dbo.Location l ON l.LocationId = s.LocationId
    WHERE m.Status = N'Done' AND m.Qty > 0;
GO

PRINT 'Views and functions created.';
GO
