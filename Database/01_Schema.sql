/* =====================================================================
   PHOENIX LAYER POULTRY ERP  -  DATABASE SCHEMA
   Target : Microsoft SQL Server 2016 SP1 or later (Express edition is fine)
   Run    : 01_Schema.sql -> 02_Views_Functions.sql -> 03_Seed.sql
            -> (optional) 04_DemoData.sql
   ===================================================================== */
SET NOCOUNT ON;
GO
IF DB_ID(N'PoultryERP') IS NULL
    CREATE DATABASE PoultryERP;
GO
USE PoultryERP;
GO

/* ---------------------------------------------------------------------
   1. TENANCY / ENTERPRISE
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Company (
    CompanyId            INT IDENTITY(1,1) PRIMARY KEY,
    Code                 NVARCHAR(20)  NOT NULL UNIQUE,
    Name                 NVARCHAR(150) NOT NULL,
    Address              NVARCHAR(400) NULL,
    Phone                NVARCHAR(40)  NULL,
    Email                NVARCHAR(150) NULL,
    GSTIN                NVARCHAR(20)  NULL,
    -- lifecycle stage boundaries (completed weeks of age, inclusive)
    StageChickEndWk      INT NOT NULL CONSTRAINT DF_Company_Chick   DEFAULT (8),
    StageGrowerEndWk     INT NOT NULL CONSTRAINT DF_Company_Grower  DEFAULT (15),
    StagePreLayEndWk     INT NOT NULL CONSTRAINT DF_Company_PreLay  DEFAULT (18),
    EggsPerTray          INT NOT NULL CONSTRAINT DF_Company_Tray    DEFAULT (30),
    EditWindowDays       INT NOT NULL CONSTRAINT DF_Company_EditWin DEFAULT (1),
    CrackedIsSaleable    BIT NOT NULL CONSTRAINT DF_Company_Crack   DEFAULT (0),
    IsActive             BIT NOT NULL CONSTRAINT DF_Company_Active  DEFAULT (1),
    CreatedOn            DATETIME2(0) NOT NULL CONSTRAINT DF_Company_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Company_Stages CHECK (StageChickEndWk < StageGrowerEndWk AND StageGrowerEndWk < StagePreLayEndWk),
    CONSTRAINT CK_Company_Tray   CHECK (EggsPerTray > 0),
    CONSTRAINT CK_Company_EditWin CHECK (EditWindowDays BETWEEN 0 AND 30)
);
GO

CREATE TABLE dbo.Subscription (
    SubscriptionId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId        INT NOT NULL REFERENCES dbo.Company(CompanyId),
    PlanName         NVARCHAR(60) NOT NULL,
    MaxFarms         INT NULL,
    MaxUsers         INT NULL,
    StartDate        DATE NOT NULL,
    EndDate          DATE NULL,
    AmountPerMonth   DECIMAL(12,2) NOT NULL CONSTRAINT DF_Sub_Amt DEFAULT (0),
    Status           NVARCHAR(20) NOT NULL CONSTRAINT DF_Sub_Status DEFAULT (N'Active'),
    Notes            NVARCHAR(400) NULL,
    CONSTRAINT CK_Sub_Status CHECK (Status IN (N'Trial', N'Active', N'Suspended', N'Expired'))
);
GO

/* ---------------------------------------------------------------------
   2. PHYSICAL ASSETS : Farm > Location > Shed
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Farm (
    FarmId            INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId         INT NOT NULL REFERENCES dbo.Company(CompanyId),
    Code              NVARCHAR(20)  NOT NULL,
    Name              NVARCHAR(150) NOT NULL,
    State             NVARCHAR(60)  NULL,
    District          NVARCHAR(60)  NULL,
    Village           NVARCHAR(100) NULL,
    Address           NVARCHAR(400) NULL,
    Latitude          DECIMAL(9,6)  NULL,
    Longitude         DECIMAL(9,6)  NULL,
    ManagerName       NVARCHAR(100) NULL,
    OpeningEggStock   INT  NOT NULL CONSTRAINT DF_Farm_OpEgg DEFAULT (0),
    OpeningEggDate    DATE NULL,
    IsActive          BIT NOT NULL CONSTRAINT DF_Farm_Active DEFAULT (1),
    CONSTRAINT UQ_Farm_Code UNIQUE (CompanyId, Code),
    CONSTRAINT CK_Farm_Lat CHECK (Latitude  IS NULL OR Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_Farm_Lng CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);
GO

CREATE TABLE dbo.Location (
    LocationId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FarmId        INT NOT NULL REFERENCES dbo.Farm(FarmId),
    Name          NVARCHAR(100) NOT NULL,
    Description   NVARCHAR(300) NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Loc_Active DEFAULT (1),
    CONSTRAINT UQ_Location UNIQUE (FarmId, Name)
);
GO

CREATE TABLE dbo.Shed (
    ShedId            INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId         INT NOT NULL REFERENCES dbo.Company(CompanyId),
    LocationId        INT NOT NULL REFERENCES dbo.Location(LocationId),
    Code              NVARCHAR(20)  NOT NULL,
    Name              NVARCHAR(100) NOT NULL,
    Capacity          INT NOT NULL,
    Purpose           NVARCHAR(20) NOT NULL CONSTRAINT DF_Shed_Purpose DEFAULT (N'Laying'),
    VentilationType   NVARCHAR(30) NOT NULL CONSTRAINT DF_Shed_Vent DEFAULT (N'Open-sided'),
    HousingSystem     NVARCHAR(20) NOT NULL CONSTRAINT DF_Shed_House DEFAULT (N'Cage'),
    Status            NVARCHAR(20) NOT NULL CONSTRAINT DF_Shed_Status DEFAULT (N'Active'),
    LengthFt          DECIMAL(8,2) NULL,
    WidthFt           DECIMAL(8,2) NULL,
    IsActive          BIT NOT NULL CONSTRAINT DF_Shed_Active DEFAULT (1),
    CONSTRAINT UQ_Shed_Code UNIQUE (CompanyId, Code),
    CONSTRAINT CK_Shed_Capacity CHECK (Capacity > 0),
    CONSTRAINT CK_Shed_Purpose CHECK (Purpose IN (N'Brooding', N'Grower', N'Laying', N'Multi-stage')),
    CONSTRAINT CK_Shed_Vent    CHECK (VentilationType IN (N'Open-sided', N'Environment Controlled')),
    CONSTRAINT CK_Shed_House   CHECK (HousingSystem IN (N'Cage', N'Floor', N'Deep Litter', N'Aviary')),
    CONSTRAINT CK_Shed_Status  CHECK (Status IN (N'Active', N'Vacant', N'Cleaning', N'Quarantine'))
);
GO

/* ---------------------------------------------------------------------
   3. PARTIES & BREEDS
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Supplier (
    SupplierId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    Name          NVARCHAR(150) NOT NULL,
    SupplierType  NVARCHAR(20) NOT NULL CONSTRAINT DF_Sup_Type DEFAULT (N'Hatchery'),
    ContactPerson NVARCHAR(100) NULL,
    Phone         NVARCHAR(40) NULL,
    Address       NVARCHAR(400) NULL,
    GSTIN         NVARCHAR(20) NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Sup_Active DEFAULT (1),
    CONSTRAINT CK_Sup_Type CHECK (SupplierType IN (N'Hatchery', N'Feed', N'Medicine', N'Other'))
);
GO

CREATE TABLE dbo.Customer (
    CustomerId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    Name          NVARCHAR(150) NOT NULL,
    Phone         NVARCHAR(40) NULL,
    Address       NVARCHAR(400) NULL,
    GSTIN         NVARCHAR(20) NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Cust_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.Breed (
    BreedId       INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    Name          NVARCHAR(100) NOT NULL,
    StrainCode    NVARCHAR(40) NULL,
    BreederCompany NVARCHAR(100) NULL,
    Notes         NVARCHAR(400) NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Breed_Active DEFAULT (1),
    CONSTRAINT UQ_Breed UNIQUE (CompanyId, Name)
);
GO

/* Breeder performance standard, one row per week of age (completed weeks). */
CREATE TABLE dbo.BreedStandard (
    BreedStandardId INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES dbo.Company(CompanyId),
    BreedId         INT NOT NULL REFERENCES dbo.Breed(BreedId),
    AgeWeek         INT NOT NULL,
    BodyWtG         DECIMAL(8,1) NULL,
    FeedGBirdDay    DECIMAL(8,1) NULL,
    HDPPct          DECIMAL(5,2) NULL,
    HHEggsCum       DECIMAL(8,1) NULL,
    EggWtG          DECIMAL(6,2) NULL,
    LivabilityPct   DECIMAL(5,2) NULL,
    CONSTRAINT UQ_BreedStd UNIQUE (BreedId, AgeWeek),
    CONSTRAINT CK_BreedStd_Age CHECK (AgeWeek BETWEEN 0 AND 120)
);
GO

/* ---------------------------------------------------------------------
   4. ITEMS (feed, grit, medicine, vaccine...) & PRICE HISTORY
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Item (
    ItemId       INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL REFERENCES dbo.Company(CompanyId),
    Code         NVARCHAR(30) NOT NULL,
    Name         NVARCHAR(120) NOT NULL,
    Category     NVARCHAR(20) NOT NULL,
    Unit         NVARCHAR(10) NOT NULL CONSTRAINT DF_Item_Unit DEFAULT (N'KG'),
    FeedStage    NVARCHAR(20) NULL,
    IsActive     BIT NOT NULL CONSTRAINT DF_Item_Active DEFAULT (1),
    CONSTRAINT UQ_Item_Code UNIQUE (CompanyId, Code),
    CONSTRAINT CK_Item_Cat   CHECK (Category IN (N'FEED', N'GRIT', N'MEDICINE', N'VACCINE', N'PACKING', N'OTHER')),
    CONSTRAINT CK_Item_Unit  CHECK (Unit IN (N'KG', N'G', N'LTR', N'ML', N'DOSE', N'NOS', N'BAG')),
    CONSTRAINT CK_Item_Stage CHECK (FeedStage IS NULL OR FeedStage IN (N'Chick', N'Grower', N'Pre-Lay', N'Layer'))
);
GO

CREATE TABLE dbo.ItemRate (
    ItemRateId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId),
    EffectiveFrom DATE NOT NULL,
    Rate          DECIMAL(12,4) NOT NULL,
    Remarks       NVARCHAR(200) NULL,
    CONSTRAINT UQ_ItemRate UNIQUE (ItemId, EffectiveFrom),
    CONSTRAINT CK_ItemRate CHECK (Rate >= 0)
);
GO

CREATE TABLE dbo.StockReceipt (
    ReceiptId     INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FarmId        INT NOT NULL REFERENCES dbo.Farm(FarmId),
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId),
    ReceiptDate   DATE NOT NULL,
    SupplierId    INT NULL REFERENCES dbo.Supplier(SupplierId),
    InvoiceNo     NVARCHAR(40) NULL,
    Qty           DECIMAL(12,3) NOT NULL,
    Rate          DECIMAL(12,4) NOT NULL,
    Amount        AS (CAST(Qty * Rate AS DECIMAL(14,2))) PERSISTED,
    Remarks       NVARCHAR(200) NULL,
    CONSTRAINT CK_Receipt_Qty CHECK (Qty > 0)
);
GO

/* ---------------------------------------------------------------------
   5. FLOCKS, PLACEMENT, TRANSFERS
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Flock (
    FlockId               INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId             INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FlockCode             NVARCHAR(30) NOT NULL,
    BatchNo               NVARCHAR(40) NULL,
    LotNo                 NVARCHAR(40) NULL,
    SupplierId            INT NULL REFERENCES dbo.Supplier(SupplierId),
    BreedId               INT NOT NULL REFERENCES dbo.Breed(BreedId),
    HatchDate             DATE NOT NULL,
    ArrivalDate           DATE NOT NULL,
    InitialCount          INT NOT NULL,
    ArrivalMortality      INT NOT NULL CONSTRAINT DF_Flock_DOA DEFAULT (0),
    ChickRate             DECIMAL(10,2) NOT NULL CONSTRAINT DF_Flock_Rate DEFAULT (0),
    FreightCost           DECIMAL(12,2) NOT NULL CONSTRAINT DF_Flock_Freight DEFAULT (0),
    HenHousedCount        INT NULL,
    ExpectedSalvagePerBird DECIMAL(10,2) NOT NULL CONSTRAINT DF_Flock_Salvage DEFAULT (0),
    PlannedDepletionWeek  INT NOT NULL CONSTRAINT DF_Flock_DeplWk DEFAULT (80),
    Status                NVARCHAR(20) NOT NULL CONSTRAINT DF_Flock_Status DEFAULT (N'Active'),
    DepletionDate         DATE NULL,
    Remarks               NVARCHAR(400) NULL,
    CreatedBy             INT NULL,
    CreatedOn             DATETIME2(0) NOT NULL CONSTRAINT DF_Flock_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Flock_Code UNIQUE (CompanyId, FlockCode),
    CONSTRAINT CK_Flock_Count  CHECK (InitialCount > 0 AND ArrivalMortality >= 0 AND ArrivalMortality <= InitialCount),
    CONSTRAINT CK_Flock_Dates  CHECK (ArrivalDate >= HatchDate),
    CONSTRAINT CK_Flock_Status CHECK (Status IN (N'Active', N'Depleted')),
    CONSTRAINT CK_Flock_DeplWk CHECK (PlannedDepletionWeek BETWEEN 30 AND 120)
);
GO

CREATE TABLE dbo.FlockPlacement (
    PlacementId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FlockId       INT NOT NULL REFERENCES dbo.Flock(FlockId),
    ShedId        INT NOT NULL REFERENCES dbo.Shed(ShedId),
    PlaceDate     DATE NOT NULL,
    BirdCount     INT NOT NULL,
    Remarks       NVARCHAR(200) NULL,
    CreatedBy     INT NULL,
    CreatedOn     DATETIME2(0) NOT NULL CONSTRAINT DF_Place_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Place_Count CHECK (BirdCount > 0)
);
GO

CREATE TABLE dbo.FlockTransfer (
    TransferId        INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId         INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FlockId           INT NOT NULL REFERENCES dbo.Flock(FlockId),
    FromShedId        INT NOT NULL REFERENCES dbo.Shed(ShedId),
    ToShedId          INT NOT NULL REFERENCES dbo.Shed(ShedId),
    TransferDate      DATE NOT NULL,
    BirdsDispatched   INT NOT NULL,
    TransitMortality  INT NOT NULL CONSTRAINT DF_Trf_Mort DEFAULT (0),
    TransitCulls      INT NOT NULL CONSTRAINT DF_Trf_Cull DEFAULT (0),
    BirdsReceived     AS (BirdsDispatched - TransitMortality - TransitCulls) PERSISTED,
    IsFullTransfer    BIT NOT NULL CONSTRAINT DF_Trf_Full DEFAULT (0),
    Status            NVARCHAR(20) NOT NULL CONSTRAINT DF_Trf_Status DEFAULT (N'Pending'),
    Remarks           NVARCHAR(400) NULL,
    RequestedBy       INT NULL,
    RequestedOn       DATETIME2(0) NOT NULL CONSTRAINT DF_Trf_Req DEFAULT (SYSDATETIME()),
    ApprovedBy        INT NULL,
    ApprovedOn        DATETIME2(0) NULL,
    ApprovalNote      NVARCHAR(400) NULL,
    CONSTRAINT CK_Trf_Sheds  CHECK (FromShedId <> ToShedId),
    CONSTRAINT CK_Trf_Counts CHECK (BirdsDispatched > 0 AND TransitMortality >= 0 AND TransitCulls >= 0
                                    AND TransitMortality + TransitCulls <= BirdsDispatched),
    CONSTRAINT CK_Trf_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected'))
);
GO

/* ---------------------------------------------------------------------
   6. DAILY SHED LOG
   --------------------------------------------------------------------- */
CREATE TABLE dbo.DailyLog (
    DailyLogId     INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT NOT NULL REFERENCES dbo.Company(CompanyId),
    ShedId         INT NOT NULL REFERENCES dbo.Shed(ShedId),
    FlockId        INT NOT NULL REFERENCES dbo.Flock(FlockId),
    LogDate        DATE NOT NULL,
    -- bird inventory
    Mortality      INT NOT NULL CONSTRAINT DF_DL_Mort DEFAULT (0),
    Culls          INT NOT NULL CONSTRAINT DF_DL_Cull DEFAULT (0),
    BirdsSold      INT NOT NULL CONSTRAINT DF_DL_Sold DEFAULT (0),
    -- feed & nutrition
    FeedItemId     INT NULL REFERENCES dbo.Item(ItemId),
    FeedKg         DECIMAL(10,3) NOT NULL CONSTRAINT DF_DL_Feed DEFAULT (0),
    GritItemId     INT NULL REFERENCES dbo.Item(ItemId),
    GritKg         DECIMAL(10,3) NOT NULL CONSTRAINT DF_DL_Grit DEFAULT (0),
    GritTime       TIME(0) NULL,
    -- egg production (numbers)
    TableEggs      INT NOT NULL CONSTRAINT DF_DL_Table DEFAULT (0),
    CrackedEggs    INT NOT NULL CONSTRAINT DF_DL_Crack DEFAULT (0),
    MisshapenEggs  INT NOT NULL CONSTRAINT DF_DL_Miss  DEFAULT (0),
    DoubleYolkEggs INT NOT NULL CONSTRAINT DF_DL_DY    DEFAULT (0),
    TotalEggs      AS (TableEggs + CrackedEggs + MisshapenEggs + DoubleYolkEggs) PERSISTED,
    -- weights, water, environment
    AvgBodyWtG     DECIMAL(8,1) NULL,
    AvgEggWtG      DECIMAL(6,2) NULL,
    WaterLtr       DECIMAL(10,2) NULL,
    TempMinC       DECIMAL(5,1) NULL,
    TempMaxC       DECIMAL(5,1) NULL,
    HumidityPct    DECIMAL(5,1) NULL,
    Remarks        NVARCHAR(400) NULL,
    CreatedBy      INT NULL,
    CreatedOn      DATETIME2(0) NOT NULL CONSTRAINT DF_DL_Created DEFAULT (SYSDATETIME()),
    ModifiedBy     INT NULL,
    ModifiedOn     DATETIME2(0) NULL,
    CONSTRAINT UQ_DailyLog UNIQUE (ShedId, LogDate),
    CONSTRAINT CK_DL_NonNeg CHECK (Mortality >= 0 AND Culls >= 0 AND BirdsSold >= 0 AND FeedKg >= 0 AND GritKg >= 0
                                   AND TableEggs >= 0 AND CrackedEggs >= 0 AND MisshapenEggs >= 0 AND DoubleYolkEggs >= 0),
    CONSTRAINT CK_DL_Temp  CHECK (TempMinC IS NULL OR TempMaxC IS NULL OR TempMinC <= TempMaxC),
    CONSTRAINT CK_DL_Hum   CHECK (HumidityPct IS NULL OR HumidityPct BETWEEN 0 AND 100)
);
GO
CREATE INDEX IX_DailyLog_Company_Date ON dbo.DailyLog (CompanyId, LogDate) INCLUDE (ShedId, FlockId);
CREATE INDEX IX_DailyLog_Flock ON dbo.DailyLog (FlockId, LogDate);
CREATE INDEX IX_Transfer_From ON dbo.FlockTransfer (FromShedId, TransferDate);
CREATE INDEX IX_Transfer_To   ON dbo.FlockTransfer (ToShedId, TransferDate);
CREATE INDEX IX_Placement_Shed ON dbo.FlockPlacement (ShedId, PlaceDate);
GO

/* Manager override that lets a supervisor edit records outside the edit window */
CREATE TABLE dbo.EditOverride (
    OverrideId      INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES dbo.Company(CompanyId),
    ShedId          INT NULL REFERENCES dbo.Shed(ShedId),      -- NULL = all sheds
    FromDate        DATE NOT NULL,
    ToDate          DATE NOT NULL,
    GrantedToUserId INT NULL,                                   -- NULL = any supervisor
    Reason          NVARCHAR(300) NOT NULL,
    GrantedBy       INT NOT NULL,
    GrantedOn       DATETIME2(0) NOT NULL CONSTRAINT DF_Ovr_On DEFAULT (SYSDATETIME()),
    ExpiresOn       DATETIME2(0) NOT NULL,
    IsRevoked       BIT NOT NULL CONSTRAINT DF_Ovr_Rev DEFAULT (0),
    CONSTRAINT CK_Ovr_Dates CHECK (FromDate <= ToDate)
);
GO

/* ---------------------------------------------------------------------
   7. HEALTH : medication / vaccination / post-mortem
   --------------------------------------------------------------------- */
CREATE TABLE dbo.MedicationLog (
    MedLogId        INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES dbo.Company(CompanyId),
    ShedId          INT NOT NULL REFERENCES dbo.Shed(ShedId),
    FlockId         INT NOT NULL REFERENCES dbo.Flock(FlockId),
    LogDate         DATE NOT NULL,
    Kind            NVARCHAR(20) NOT NULL,
    ItemId          INT NOT NULL REFERENCES dbo.Item(ItemId),
    Qty             DECIMAL(12,3) NOT NULL CONSTRAINT DF_Med_Qty DEFAULT (0),
    Dose            NVARCHAR(60) NULL,
    Route           NVARCHAR(30) NULL,
    Reason          NVARCHAR(200) NULL,
    AdministeredBy  NVARCHAR(100) NULL,
    Status          NVARCHAR(20) NOT NULL CONSTRAINT DF_Med_Status DEFAULT (N'Done'),
    ApprovalStatus  NVARCHAR(20) NOT NULL CONSTRAINT DF_Med_Appr DEFAULT (N'Not Required'),
    ApprovedBy      INT NULL,
    ApprovedOn      DATETIME2(0) NULL,
    Remarks         NVARCHAR(300) NULL,
    CreatedBy       INT NULL,
    CreatedOn       DATETIME2(0) NOT NULL CONSTRAINT DF_Med_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Med_Kind   CHECK (Kind IN (N'Medication', N'Vaccination')),
    CONSTRAINT CK_Med_Status CHECK (Status IN (N'Planned', N'Done', N'Skipped')),
    CONSTRAINT CK_Med_Appr   CHECK (ApprovalStatus IN (N'Not Required', N'Pending', N'Approved', N'Rejected')),
    CONSTRAINT CK_Med_Qty    CHECK (Qty >= 0)
);
GO
CREATE INDEX IX_Med_Shed_Date ON dbo.MedicationLog (ShedId, LogDate);
GO

CREATE TABLE dbo.VaccineSchedule (
    ScheduleId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId    INT NOT NULL REFERENCES dbo.Company(CompanyId),
    BreedId      INT NULL REFERENCES dbo.Breed(BreedId),
    AgeDay       INT NOT NULL,
    ItemId       INT NOT NULL REFERENCES dbo.Item(ItemId),
    Route        NVARCHAR(30) NULL,
    DosePerBird  DECIMAL(10,4) NOT NULL CONSTRAINT DF_VS_Dose DEFAULT (1),
    Notes        NVARCHAR(200) NULL,
    CONSTRAINT CK_VS_Age CHECK (AgeDay BETWEEN 0 AND 800)
);
GO

CREATE TABLE dbo.PostMortem (
    PostMortemId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId      INT NOT NULL REFERENCES dbo.Company(CompanyId),
    ShedId         INT NOT NULL REFERENCES dbo.Shed(ShedId),
    PmDate         DATE NOT NULL,
    BirdsExamined  INT NOT NULL CONSTRAINT DF_PM_Birds DEFAULT (1),
    Findings       NVARCHAR(1000) NOT NULL,
    Diagnosis      NVARCHAR(300) NULL,
    ActionTaken    NVARCHAR(500) NULL,
    VetName        NVARCHAR(100) NULL,
    CreatedBy      INT NULL,
    CreatedOn      DATETIME2(0) NOT NULL CONSTRAINT DF_PM_Created DEFAULT (SYSDATETIME())
);
GO

/* ---------------------------------------------------------------------
   8. FINANCE : overheads, sales, egg wastage
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Overhead (
    OverheadId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId     INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FarmId        INT NOT NULL REFERENCES dbo.Farm(FarmId),
    ShedId        INT NULL REFERENCES dbo.Shed(ShedId),   -- NULL = shared by farm, allocated on bird-days
    CostHead      NVARCHAR(20) NOT NULL,
    FromDate      DATE NOT NULL,
    ToDate        DATE NOT NULL,
    Amount        DECIMAL(14,2) NOT NULL,
    Description   NVARCHAR(300) NULL,
    CreatedBy     INT NULL,
    CreatedOn     DATETIME2(0) NOT NULL CONSTRAINT DF_OH_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_OH_Head  CHECK (CostHead IN (N'Labor', N'Utility', N'Overhead', N'Other')),
    CONSTRAINT CK_OH_Dates CHECK (FromDate <= ToDate),
    CONSTRAINT CK_OH_Amt   CHECK (Amount >= 0)
);
GO

CREATE TABLE dbo.Sale (
    SaleId          INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FarmId          INT NOT NULL REFERENCES dbo.Farm(FarmId),
    SaleDate        DATE NOT NULL,
    CustomerId      INT NULL REFERENCES dbo.Customer(CustomerId),
    InvoiceNo       NVARCHAR(40) NULL,
    Product         NVARCHAR(20) NOT NULL,
    ShedId          INT NULL REFERENCES dbo.Shed(ShedId),
    Qty             DECIMAL(12,2) NOT NULL,
    QtyKg           DECIMAL(12,2) NULL,
    Rate            DECIMAL(12,4) NOT NULL,
    Amount          AS (CAST(Qty * Rate AS DECIMAL(14,2))) PERSISTED,
    AmountReceived  DECIMAL(14,2) NOT NULL CONSTRAINT DF_Sale_Recv DEFAULT (0),
    Remarks         NVARCHAR(300) NULL,
    CreatedBy       INT NULL,
    CreatedOn       DATETIME2(0) NOT NULL CONSTRAINT DF_Sale_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Sale_Product CHECK (Product IN (N'Table Egg', N'Cracked Egg', N'Double Yolk', N'Misshapen Egg', N'Cull Bird', N'Manure', N'Other')),
    CONSTRAINT CK_Sale_Qty CHECK (Qty > 0 AND Rate >= 0 AND AmountReceived >= 0)
);
GO

CREATE TABLE dbo.EggWastage (
    WastageId   INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NOT NULL REFERENCES dbo.Company(CompanyId),
    FarmId      INT NOT NULL REFERENCES dbo.Farm(FarmId),
    WasteDate   DATE NOT NULL,
    Qty         INT NOT NULL,
    Reason      NVARCHAR(200) NULL,
    CreatedBy   INT NULL,
    CONSTRAINT CK_Waste_Qty CHECK (Qty > 0)
);
GO

/* ---------------------------------------------------------------------
   9. SECURITY : users, shed assignment, audit
   --------------------------------------------------------------------- */
CREATE TABLE dbo.AppUser (
    UserId              INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId           INT NULL REFERENCES dbo.Company(CompanyId),  -- NULL for Super Admin
    UserName            NVARCHAR(50)  NOT NULL UNIQUE,
    FullName            NVARCHAR(100) NOT NULL,
    Email               NVARCHAR(150) NULL,
    Phone               NVARCHAR(40)  NULL,
    Role                NVARCHAR(30)  NOT NULL,
    PasswordHash        NVARCHAR(200) NOT NULL,
    MustChangePassword  BIT NOT NULL CONSTRAINT DF_User_MCP DEFAULT (1),
    IsActive            BIT NOT NULL CONSTRAINT DF_User_Active DEFAULT (1),
    FailedAttempts      INT NOT NULL CONSTRAINT DF_User_Fail DEFAULT (0),
    LockedUntil         DATETIME2(0) NULL,
    LastLoginOn         DATETIME2(0) NULL,
    CreatedOn           DATETIME2(0) NOT NULL CONSTRAINT DF_User_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_User_Role CHECK (Role IN (N'SuperAdmin', N'FarmManager', N'Supervisor', N'Veterinarian', N'Accountant')),
    CONSTRAINT CK_User_Company CHECK (Role = N'SuperAdmin' OR CompanyId IS NOT NULL)
);
GO

CREATE TABLE dbo.UserShed (
    UserId   INT NOT NULL REFERENCES dbo.AppUser(UserId) ON DELETE CASCADE,
    ShedId   INT NOT NULL REFERENCES dbo.Shed(ShedId),
    CONSTRAINT PK_UserShed PRIMARY KEY (UserId, ShedId)
);
GO

CREATE TABLE dbo.AuditLog (
    AuditId     BIGINT IDENTITY(1,1) PRIMARY KEY,
    CompanyId   INT NULL,
    UserId      INT NULL,
    UserName    NVARCHAR(50) NULL,
    Action      NVARCHAR(30) NOT NULL,
    EntityName  NVARCHAR(60) NOT NULL,
    EntityId    NVARCHAR(40) NULL,
    Details     NVARCHAR(MAX) NULL,
    IpAddress   NVARCHAR(45) NULL,
    CreatedOn   DATETIME2(0) NOT NULL CONSTRAINT DF_Audit_On DEFAULT (SYSDATETIME())
);
GO
CREATE INDEX IX_Audit_Company_Date ON dbo.AuditLog (CompanyId, CreatedOn DESC);
GO

PRINT 'Schema created.';
GO
