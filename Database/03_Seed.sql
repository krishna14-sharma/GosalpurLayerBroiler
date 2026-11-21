/* =====================================================================
   SEED DATA  -  company, users, generic breed standard, items, rates
   Default logins (you will be asked to change the password on first login):
       admin    / Admin@123     (Super Admin - all companies)
       manager  / Phoenix@123   (Farm / Production Manager)
   ===================================================================== */
USE PoultryERP;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @CompanyId INT, @BreedId INT, @Today DATE = CAST(GETDATE() AS DATE);

INSERT INTO dbo.Company (Code, Name, Address, Phone, Email)
VALUES (N'PHX', N'Phoenix Group Poultry Farms', N'Head Office', NULL, NULL);
SET @CompanyId = SCOPE_IDENTITY();

INSERT INTO dbo.Subscription (CompanyId, PlanName, MaxFarms, MaxUsers, StartDate, AmountPerMonth, Status)
VALUES (@CompanyId, N'Enterprise', 50, 200, @Today, 0, N'Active');

INSERT INTO dbo.AppUser (CompanyId, UserName, FullName, Role, PasswordHash, MustChangePassword)
VALUES (NULL, N'admin', N'System Administrator', N'SuperAdmin', N'PBKDF2$SHA256$100000$UE2C33xtxJUUzsmjW6ZHQA==$ugAfaQtRKo5jBvWtOR5CGvI24DC8UquQI7N3eI1K4Bg=', 1),
       (@CompanyId, N'manager', N'Farm Manager', N'FarmManager', N'PBKDF2$SHA256$100000$/UklfICyrXI4DXzczMnCgA==$L/tzZyz3DeWjyrkprmCFT5D2Mg0Cz3G36Iyh89glvTw=', 1);

/* ---- Generic white-layer standard (SAMPLE ONLY - replace with your breeder's management guide) ---- */
INSERT INTO dbo.Breed (CompanyId, Name, StrainCode, BreederCompany, Notes)
VALUES (@CompanyId, N'Generic White Layer', N'GWL', N'Sample', N'Sample curve for testing. Replace values with the official breeder guide (Masters > Breeds & Standards).');
SET @BreedId = SCOPE_IDENTITY();

INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,0,38,10,NULL,NULL,NULL,100);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,1,65,14.0,NULL,NULL,NULL,99.75);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,2,120,18.0,NULL,NULL,NULL,99.5);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,3,190,22.0,NULL,NULL,NULL,99.25);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,4,270,27.0,NULL,NULL,NULL,99.0);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,5,360,32.0,NULL,NULL,NULL,98.88);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,6,450,37.0,NULL,NULL,NULL,98.75);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,7,540,41.0,NULL,NULL,NULL,98.62);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,8,630,45.0,NULL,NULL,NULL,98.5);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,9,710,49.0,NULL,NULL,NULL,98.4);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,10,790,52.0,NULL,NULL,NULL,98.3);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,11,860,55.0,NULL,NULL,NULL,98.2);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,12,930,58.0,NULL,NULL,NULL,98.1);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,13,990,61.0,NULL,NULL,NULL,98.0);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,14,1050,64.0,NULL,NULL,NULL,97.9);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,15,1100,67.0,NULL,NULL,NULL,97.8);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,16,1150,70.0,NULL,NULL,NULL,97.7);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,17,1200,74.0,NULL,NULL,NULL,97.6);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,18,1260,79.0,5.0,0.3,42,97.5);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,19,1330,85.0,20.0,1.7,45.0,97.42);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,20,1400,90.0,50.0,5.1,48.0,97.35);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,21,1445,94.0,75.0,10.2,50.0,97.27);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,22,1490,98.0,88.0,16.2,52.0,97.19);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,23,1515,100.0,92.0,22.5,54.0,97.11);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,24,1540,102.0,94.0,28.8,55.0,97.04);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,25,1550,102.2,95.0,35.3,56.5,96.96);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,26,1560,102.5,95.5,41.8,57.5,96.88);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,27,1570,102.8,95.5,48.2,58.0,96.8);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,28,1580,103.0,95.5,54.7,58.5,96.73);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,29,1584,103.2,95.25,61.2,59.0,96.65);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,30,1588,103.5,95.0,67.6,59.5,96.57);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,31,1592,103.8,94.8,74.0,59.7,96.5);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,32,1597,104.0,94.6,80.4,59.9,96.42);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,33,1601,104.2,94.4,86.7,60.1,96.34);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,34,1605,104.5,94.2,93.1,60.3,96.26);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,35,1609,104.8,94.0,99.4,60.5,96.19);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,36,1613,105.0,93.8,105.7,60.66,96.11);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,37,1618,105.2,93.6,112.0,60.82,96.03);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,38,1622,105.5,93.4,118.3,60.98,95.95);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,39,1626,105.8,93.2,124.5,61.14,95.88);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,40,1630,106.0,93.0,130.8,61.3,95.8);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,41,1632,106.1,92.75,137.0,61.4,95.72);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,42,1634,106.2,92.5,143.2,61.5,95.64);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,43,1636,106.3,92.25,149.3,61.6,95.56);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,44,1638,106.4,92.0,155.5,61.7,95.48);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,45,1640,106.5,91.75,161.6,61.8,95.4);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,46,1642,106.6,91.5,167.7,61.9,95.32);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,47,1644,106.7,91.25,173.8,62.0,95.24);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,48,1646,106.8,91.0,179.9,62.1,95.16);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,49,1648,106.9,90.75,185.9,62.2,95.08);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,50,1650,107.0,90.5,191.9,62.3,95.0);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,51,1652,107.1,90.2,197.9,62.37,94.92);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,52,1654,107.2,89.9,203.9,62.44,94.84);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,53,1656,107.3,89.6,209.8,62.51,94.76);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,54,1658,107.4,89.3,215.8,62.58,94.68);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,55,1660,107.5,89.0,221.7,62.65,94.6);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,56,1662,107.6,88.7,227.5,62.72,94.52);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,57,1664,107.7,88.4,233.4,62.79,94.44);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,58,1666,107.8,88.1,239.2,62.86,94.36);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,59,1668,107.9,87.8,245.0,62.93,94.28);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,60,1670,108.0,87.5,250.7,63.0,94.2);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,61,1672,108.1,87.1,256.5,63.05,94.12);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,62,1673,108.2,86.7,262.2,63.1,94.03);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,63,1674,108.3,86.3,267.9,63.15,93.95);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,64,1676,108.4,85.9,273.5,63.2,93.86);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,65,1678,108.5,85.5,279.1,63.25,93.78);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,66,1679,108.6,85.1,284.7,63.3,93.69);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,67,1680,108.7,84.7,290.3,63.35,93.61);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,68,1682,108.8,84.3,295.8,63.4,93.52);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,69,1684,108.9,83.9,301.3,63.45,93.44);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,70,1685,109.0,83.5,306.7,63.5,93.35);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,71,1686,109.1,83.05,312.1,63.53,93.27);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,72,1688,109.2,82.6,317.5,63.56,93.18);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,73,1690,109.3,82.15,322.9,63.59,93.09);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,74,1691,109.4,81.7,328.2,63.62,93.01);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,75,1692,109.5,81.25,333.5,63.65,92.92);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,76,1694,109.6,80.8,338.7,63.68,92.84);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,77,1696,109.7,80.35,344.0,63.71,92.75);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,78,1697,109.8,79.9,349.1,63.74,92.67);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,79,1698,109.9,79.45,354.3,63.77,92.58);
INSERT INTO dbo.BreedStandard (CompanyId,BreedId,AgeWeek,BodyWtG,FeedGBirdDay,HDPPct,HHEggsCum,EggWtG,LivabilityPct) VALUES (@CompanyId,@BreedId,80,1700,110,79,359.4,63.8,92.5);

/* ---- Items ---- */
INSERT INTO dbo.Item (CompanyId, Code, Name, Category, Unit, FeedStage) VALUES
 (@CompanyId, N'CSF',   N'Chick Starter Feed',  N'FEED', N'KG', N'Chick'),
 (@CompanyId, N'GRF',   N'Grower Feed',         N'FEED', N'KG', N'Grower'),
 (@CompanyId, N'PLF',   N'Pre-Lay Feed',        N'FEED', N'KG', N'Pre-Lay'),
 (@CompanyId, N'LF1',   N'Layer Feed Phase-1',  N'FEED', N'KG', N'Layer'),
 (@CompanyId, N'LF2',   N'Layer Feed Phase-2',  N'FEED', N'KG', N'Layer'),
 (@CompanyId, N'SG',    N'Shell Grit',          N'GRIT', N'KG', NULL),
 (@CompanyId, N'MULTV', N'Multivitamin (liquid)', N'MEDICINE', N'ML', NULL),
 (@CompanyId, N'ELECT', N'Electrolyte Powder',  N'MEDICINE', N'G', NULL),
 (@CompanyId, N'ENRO',  N'Enrofloxacin 10%',    N'MEDICINE', N'ML', NULL),
 (@CompanyId, N'MAREK', N'Marek''s Vaccine (HVT)', N'VACCINE', N'DOSE', NULL),
 (@CompanyId, N'NDIB',  N'ND + IB Live (Lasota)', N'VACCINE', N'DOSE', NULL),
 (@CompanyId, N'IBD',   N'IBD Intermediate',    N'VACCINE', N'DOSE', NULL),
 (@CompanyId, N'FPOX',  N'Fowl Pox',            N'VACCINE', N'DOSE', NULL),
 (@CompanyId, N'NDK',   N'ND Killed (oil)',     N'VACCINE', N'DOSE', NULL),
 (@CompanyId, N'TRAY',  N'Egg Tray (30)',       N'PACKING', N'NOS', NULL);

INSERT INTO dbo.ItemRate (CompanyId, ItemId, EffectiveFrom, Rate, Remarks)
SELECT @CompanyId, i.ItemId, DATEADD(YEAR, -2, @Today), r.Rate, N'Opening rate'
FROM dbo.Item i
JOIN (VALUES (N'CSF',34.00),(N'GRF',29.50),(N'PLF',30.50),(N'LF1',28.00),(N'LF2',27.00),(N'SG',6.00),
             (N'MULTV',0.45),(N'ELECT',0.30),(N'ENRO',1.20),(N'MAREK',0.80),(N'NDIB',0.35),(N'IBD',0.40),
             (N'FPOX',0.60),(N'NDK',1.10),(N'TRAY',3.50)) r(Code, Rate) ON r.Code = i.Code
WHERE i.CompanyId = @CompanyId;

/* ---- Standard vaccination programme (typical Indian layer schedule - review with your vet) ---- */
INSERT INTO dbo.VaccineSchedule (CompanyId, BreedId, AgeDay, ItemId, Route, DosePerBird, Notes)
SELECT @CompanyId, NULL, v.AgeDay, i.ItemId, v.Route, 1, v.Notes
FROM (VALUES (1,  N'MAREK', N'Injection',   N'At hatchery / day 1'),
             (7,  N'NDIB',  N'Eye drop',    N'ND + IB primary'),
             (14, N'IBD',   N'Drinking water', N'Gumboro 1st'),
             (21, N'NDIB',  N'Drinking water', N'ND booster'),
             (28, N'IBD',   N'Drinking water', N'Gumboro 2nd'),
             (42, N'FPOX',  N'Wing web',    N'Fowl pox'),
             (56, N'NDIB',  N'Drinking water', N'ND booster'),
             (112,N'NDK',   N'Injection',   N'ND killed before lay'),
             (126,N'FPOX',  N'Wing web',    N'Fowl pox booster')) v(AgeDay, Code, Route, Notes)
JOIN dbo.Item i ON i.Code = v.Code AND i.CompanyId = @CompanyId;

COMMIT TRANSACTION;
PRINT 'Seed data inserted.';
GO
