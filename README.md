# Phoenix Layer Poultry ERP

ASP.NET Web Forms 4.8 (C#) · SQL Server · Bootstrap 5 · ASP.NET AJAX (UpdatePanels + page methods) · responsive on mobile, tablet and desktop.

It replaces the old *PoultryFarmMIS* VB.NET / Crystal Reports desktop application with a multi-farm web ERP. The old reports are carried over (same figures and layout style), and the specification's new modules are added.

---

## 1. Installation (about 15 minutes)

**You need:** Windows 10/11 or Windows Server with IIS and ASP.NET 4.8. You also need SQL Server 2016 SP1 or later (Express is fine). Visual Studio 2019/2022 is optional.

1. **Database**: open a command prompt in `Database\` and run:
   ```
   Setup.bat .\SQLEXPRESS          (empty system)
   Setup.bat .\SQLEXPRESS demo     (adds a demo farm with 4 flocks and ~15 months of data)
   ```
   You can also run `01_Schema.sql`, `02_Views_Functions.sql`, `03_Seed.sql`, `05_Broiler.sql` and, optionally, `04_DemoData.sql` and `06_BroilerDemo.sql` in SSMS in that order.
   **Already installed the earlier version?** Run only `05_Broiler.sql` on your existing database. It is safe to run more than once and keeps your data.
   Load the demo data on a test database only.
2. **Connection string**: edit `Web\Web.config` → `connectionStrings/PoultryERP`. For SQL login instead of Windows login use
   `Data Source=SERVER;Initial Catalog=PoultryERP;User ID=erp;Password=...`.
   If you use Windows login under IIS, grant the app-pool identity (`IIS APPPOOL\PoultryERP`) db_datareader, db_datawriter and EXECUTE on the database.
3. **Run it**
   * **Visual Studio**: *File → Open → Web Site…* and choose the `Web` folder (or open `PoultryERP.sln`), then press F5.
   * **IIS**: create an application pointing at the `Web` folder, using an *Integrated* pipeline, *.NET CLR v4.0* app pool.
4. **Sign in** with `admin` / `Admin@123` (Super Admin). The `manager` / `Phoenix@123` account is a Farm Manager. You must change the password at first login.
   The demo data adds `super1`, `vet` and `accounts` (all `Phoenix@123`).
5. In production, switch the site to **HTTPS** and set `requireSSL="true"` on `<forms>` and `<httpCookies>` in Web.config.

No NuGet packages are needed. Bootstrap, Bootstrap Icons, Chart.js and jQuery are bundled in `Web\lib`, so the app also works on an offline farm intranet. The PDF and Excel exports are built in.

## 2. First-time setup order (in the app)

Company Settings → Farms → Locations → Sheds → Breeds & Breed Standards (enter your breeder's guide) → Items → Item Prices → Suppliers/Hatcheries → Users (assign sheds to supervisors) → **Flocks & Placement** (inward) → **Daily Shed Entry** every day.

## 2a. Going live with existing birds and stock

Open *Administration → Opening Balances (Go-Live)*, choose the go-live date and fill in:

1. **Birds in sheds today.** For each occupied shed enter the flock/batch code, breed (this sets Layer or Broiler), real hatch date, birds counted today and value per bird. For layers already laying, also enter **hens housed** from your old records.
   *Optional history:* enter the original placement date, chicks placed and feed eaten so far, typically for a running broiler batch. The system places the original chicks on that date and books one catch-up entry the day before go-live, with mortality = chicks placed − birds today and the feed to date. FCR, livability and batch P&L then come out right.
2. **Eggs in godown**: eggs counted at each farm (stored as the opening egg stock at the end of the day before go-live).
3. **Feed / grit / medicine in store**: quantity and rate of each item (saved as stock receipts with invoice no. `OPENING`).

One click saves everything in a single transaction. Supervisors start Daily Shed Entry on the go-live date.

## 3. Modules

| Area | Screens |
|---|---|
| Enterprise | Companies (tenants), Subscriptions & Billing, Company Settings (stage weeks, eggs/tray, edit window, cracked = saleable?) |
| Assets | Farms (State, District, latitude/longitude), Locations/Blocks, Sheds (capacity, ventilation, cage/floor, Active/Vacant/Cleaning/Quarantine) |
| Flock | Inward (hatchery lineage, batch/lot, hatch & arrival date, head count, DOA, chick rate), placement into sheds, close/reopen flock |
| Shifting | Transfer request → manager approval; full or partial; transit mortality and culls; age at movement; shed status updated automatically |
| Daily log | Per shed per day: mortality, culls, birds sold, feed formulation and Kg, shell grit Kg and time, water, table/cracked/misshapen/double-yolk eggs, body and egg weight, min/max temperature, humidity. Live g/bird, HD %, trays and closing birds, compared with the standard |
| Health | Medication & vaccination log, vet approval of vaccinations, vaccination schedule template and "Generate plan", post-mortem notes |
| Finance | Item prices with history, overheads (labour/utility/other; farm-level costs are allocated on bird-days), egg and bird sales with receipts, egg wastage, stock receipts |
| Admin | Users and shed access, edit overrides, audit log |
| Reports | 12 reports (below): on screen with chart, plus **PDF / Excel / CSV / Print** |

## 3a. Broiler (meat bird) module

Broilers run in the same system as layers. Each flock is created as **Layer** or **Broiler** and uses the same sheds, daily entry, transfers, medication and overhead costing.

| What | How it works for broilers |
|---|---|
| Batch | *Flocks & Placement → New inward → Bird type: Broiler*. Day-old chicks, hatchery, batch/lot, chick rate, freight, **target lifting age** (default 42 days). Age is shown in **days**. The company limit is 70 days (*Company Settings → Broiler maximum age*); older batches are flagged in red |
| Sheds and feeds | Shed purpose **Broiler**. Broiler Pre-Starter, Starter and Finisher feeds are seeded with prices |
| Daily entry | Same screen. For broilers, egg, grit and "birds sold" fields are hidden. Enter mortality, culls, feed, water, climate and the **sample body weight**. Live chips show cumulative feed per bird, **estimated FCR** and body weight, each against the breeder standard for that exact day of age |
| Sales ("lifting") | *Daily Operations → Broiler Sales / Lifting*: birds, total live Kg, rate per Kg, customer, vehicle, amount received. It reduces the shed's birds automatically and blocks lifting more than are in the shed. When the last bird is lifted, the batch closes and the shed becomes Vacant |
| Standards | *Masters → Breed Standards*: weekly body weight, daily feed, cumulative feed and FCR (a sample "Generic Broiler" curve is seeded; replace it with your breeder's figures). Values between weeks are interpolated per day |
| Vaccination | The broiler schedule is linked to the broiler breed, and *Generate plan* uses it. Layer schedules (no breed) are never applied to broilers |

**Broiler reports**

| Report | Shows |
|---|---|
| Broiler Daily Report | Every broiler shed for a day: opening, mortality, culls, lifted, closing, mortality % and cumulative %, feed and g/bird vs standard, cumulative feed per bird, body weight vs standard, estimated FCR vs standard. Footer has the day's lifting (birds, kg, average weight, value) |
| Broiler Performance & FCR | One batch, **daily or weekly**: growth, daily gain, feed, cumulative feed per bird, FCR vs standard, with a weight/FCR chart. Summary covers livability, average weight and age at sale, and **EPEF** |
| Broiler Batch Profit & Loss | Income from bird sales against chicks, freight, feed, medicine and allocated labour, electricity and overheads. Each line is shown per bird placed and per kg live weight, ending in net profit or loss. Running batches also show a provisional result, valuing birds in house at the average rate. Also shows cost per kg, selling rate per kg, margin per kg, and amounts received and outstanding |
| Broiler P&L Summary | All batches of a period side by side: livability, FCR, EPEF, income, expenses, profit, profit per bird, and cost and rate per kg, with a profit chart |

**Broiler formulas.** FCR = total feed eaten ÷ (live kg lifted + birds still in house × latest sample weight). For a closed batch this is simply feed ÷ kg sold.
Livability = (chicks placed − mortality − culls − transit losses) ÷ placed. EPEF = livability % × average weight (kg) × 100 ÷ (average age at sale × FCR).
Labour, electricity and overheads entered at farm level are shared between batches (and layer sheds on the same farm) in proportion to bird-days.

## 4. Reports: old MIS → new

The old `.rpt` files store their layout in a compressed binary format that cannot be read outside Crystal. Each report was therefore rebuilt from the old code that fed it: the dataset columns (`XSDSchema1`, `xsdHenHouse`, …), the parameters (`opnegg`, `geggsld`, `beggsld`, `wasteegg`, `ckh/grw/lyr`, `st_wt`, `st_prod`, …) and the calculations in the `frm*Rpt.vb` forms. Ages keep the old **weeks-days** format (e.g. `40-3`).

| Old (VB form / Crystal file) | New report | Notes |
|---|---|---|
| Daily Transaction Report (`frmTransRepMain` / `crTrans`) | **Daily Transaction Report** | Shed-wise opening, mortality, sold, closing, feed, body weight vs standard, good/broken eggs, egg weight vs standard, HD % vs standard. Footer has the **egg stock** (opening, production, good sold, broken sold, wastage, closing) and **feed by stage** (Chicks / Grower / Pre-Lay / Layer) as before. New: culls, transfers, grit, trays, water and environment |
| Daily Supervisor Report (`frmTransRep` / `crSupervisor`) | **Daily Supervisor Report** | Farm Excel layout: lot no., shed, age, opening birds, % production vs standard, feed g/bird, egg weight and body weight vs standard, eggs, feed; farm production %, wastage %, today vs previous day, birds by stage, feed per bird/egg, feed rate, feed cost and egg production cost |
| Chicks/Grower Report (`frmTransRepMainforChiks` / `crTransChicks`) | **Chicks / Grower Report** | One shed, date range, day by day; adds medication/vaccination, water and climate |
| Transaction Report II (`frmTransRepNew` / `crProd`) | **Flock Transaction Report (II)** | Flock-wise daily: opening, mortality, sale, feed type and quantity, shell grit, closing. Adds transit loss and eggs |
| Hen House / Hen House with weight (`crHenHouse`, `crHenHouseWithebwt`) | **Hen House Production Report** | Weekly from week 18: opening, depletion, eggs, HD %, HH %, eggs/HH and cumulative, feed g/bird/day, **FCR per dozen and per kg egg mass**, all vs breeder standard. "With weights" option gives the old ebwt variant. Chart included |
| Cost of Production (`frmProdRep` / `crCost`) | **Flock Cost of Production** | Chick cost, feed, medicine, labour, misc split into rearing vs laying, per bird housed and per egg; sheds used vs total sheds as before |
| Medicine Stock (`CrMediStock`) | **Feed & Medicine Stock** | Opening, receipts, consumption (taken automatically from daily entries), closing, value |
| Eggs/Bird Sale (`CrItemSale`) | **Eggs / Bird Sale Register** | Invoice-wise sales with receipts and balance, plus a product summary |
| – | **Flock Transfer / Shifting Report** | Audit trail: from/to shed, age at movement, transit mortality and culls, shrinkage %, approval |
| – | **Cost per Egg Report** | Daily, weekly or monthly trend of feed, grit, medicine, labour, utility, overheads and depreciation per saleable egg |
| – | **Mortality Analysis** | By shed and week, with peak days and post-mortem findings |
| – | **Egg & Revenue Reconciliation** | Egg stock reconciliation, revenue and receipts, cost versus revenue |

## 5. Key calculations

* **Birds**: opening and closing are never typed in. They are derived from every movement (placement, approved transfers, mortality, culls, sales), the same idea as `SUM(No)` on the old `Trans_Daily`. Correcting a past day therefore fixes every later balance automatically. A save that would make any later balance negative is refused.
* **Age** runs from the flock's hatch date and continues across shed transfers. **Stage**: Chick ≤ wk 8, Grower ≤ 15, Pre-Lay ≤ 18, Layer from week 19 (adjustable in Company Settings).
* **HD %** = eggs ÷ hens present that day × 100. **HH %** = eggs ÷ (hens housed × days) × 100. Hens housed is the figure stored on the flock; if blank, it is the birds alive at the start of lay.
* **FCR** = kg feed ÷ dozen eggs, and kg feed ÷ kg egg mass (using the recorded egg weight).
* **Cost per egg** = (Feed + Shell grit + Medicine/vaccines + Labour + Utility/overheads) ÷ saleable eggs, using laying sheds only. Saleable eggs are table + double yolk + misshapen, with cracked eggs optional.
  Feed, grit and medicine are valued at the price in force on each day. Farm-level overheads are spread over sheds by bird-days.
* **Bird depreciation**: chick cost and all rearing costs up to the end of pre-lay are capitalised per hen housed. Less the expected salvage value, this is written off evenly until the planned depletion week. Reports show cost per egg both before and after depreciation.

## 6. Roles

| Role | Can do |
|---|---|
| Super Admin | Everything, all companies (switch company in the top bar), users, subscriptions, audit log |
| Farm / Production Manager | All farm data, approve transfers, edit any date (audited), grant edit overrides, all reports |
| Shed Supervisor / Data Entry | Daily entry, broiler liftings, transfer requests and medication **for assigned sheds only**; can edit only within the edit window (default today and yesterday) unless a manager grants an override |
| Veterinarian / Quality Officer | Medication and vaccination, approve vaccinations, schedules, post-mortem, breed standards, mortality reports |
| Accountant / Finance Officer | Item prices, overheads, sales, stock, cost-per-egg, cost of production, reconciliation reports |

## 7. Security

PBKDF2-SHA256 password hashing (100,000 iterations), account lockout, and forced password change at first login. Forms authentication uses HttpOnly cookies and ViewStateUserKey anti-CSRF. All SQL is parameterised, and role checks run on every page, AJAX method and export. Every change, approval, override, export and login is written to the audit log.

## 8. Project layout

```
Database/            01_Schema, 02_Views_Functions, 03_Seed, 04_DemoData, 05_Broiler (upgrade), 06_BroilerDemo, Setup.bat
Web/
  App_Code/          Core (Db, auth, context, audit), BasePage, Nav, Ops (business rules), Crud (master screen definitions)
  App_Code/Reports/  Analytics (bird ledger, standards, rates), CostEngine, report builders, HTML / PDF / Excel / CSV writers
  Daily/ Flock/ Broiler/ Health/ Masters/ Reports/ Admin/ Handlers/Export.ashx
  Site.master, Default.aspx (dashboard), Login.aspx, Content/site.css, Scripts/app.js, lib/
```

Code is written in C# 5, so the Web Site compiles on any IIS with .NET 4.8 without the Roslyn NuGet package.

## 9. Testing done

All C# compiled under C# 5 rules, and every page compiled and ran through the ASP.NET pipeline (Mono) as each role. Role guards, PDF/Excel/CSV output, the bird-ledger maths and the broiler formulas (standard interpolation, FCR, livability, EPEF) were tested.
The SQL scripts were checked but **not executed against a live SQL Server** in the build environment. Run `Setup.bat … demo` on a test server first.
