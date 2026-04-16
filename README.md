# MaterialManagement

ERP-style internal system for materials, sales, purchases, inventory, payments, reservations, returns, expenses, employees, equipment, maintenance, and reports.

## Overview

MaterialManagement is an **ASP.NET Core MVC / Razor** application organized into three main projects:

- `MaterialManagement` — Web UI / presentation layer
- `MaterialManagement.BLL` — business logic and view models
- `MaterialManagement.DAL` — Entity Framework Core data access, entities, repositories, and migrations

The solution also includes:

- `IntegrationTestSandbox` — verification/smoke-test harness for critical business flows
- EF Core migrations up to:
  - `Phase1_Stabilization_Tokens`
  - `AddSalesReturnsModule`
  - `AddOneTimeInvoiceParties`

The current UI direction is **desktop-first**, **Arabic RTL**, **worksheet / ledger style**, intended primarily for **laptop use**. The uploaded project tree confirms the main structure, projects, controllers, views, services, and migrations in the solution. fileciteturn26file0

---

## Main Features

### Sales
- Sales invoice creation, listing, review, and deletion
- Registered clients and walk-in / one-time customer flow
- Client balance updates
- Sales returns support
- Account statement reporting

### Purchases
- Purchase invoice creation, listing, review, and deletion
- Registered suppliers and one-time / manual supplier flow
- Registered client-return purchase mode
- Supplier balance updates

### Inventory
- Material management
- Low stock page
- Material movement report
- Negative stock is allowed by business rules

### Payments
- Client payments
- Supplier payments
- Ownership and overpayment protections on registered flows
- One-time party later-payment flow intentionally not supported

### Reservations
- Reservation create / list / details / fulfill flows
- Reservation stabilization and balance-safe stock handling

### Returns
- Sales return support
- Sales return sandbox UI/controller available
- Supervisor-protected sensitive return actions

### Expenses / Operations
- Expense management
- Employee screens
- Equipment and maintenance flows
- Dashboard / landing page
- Reports and printable statement screens

---

## Business Rules

The current system behavior includes these important rules:

### Allowed negative states
These are valid business states in this system:
- negative client balance
- negative supplier balance
- negative material quantity

So negative resulting balances are **not automatically errors**.

### One-time parties
Supported:
- walk-in / cash sales customer
- one-time / manual supplier

Rules:
- one-time sales must be fully settled at creation
- one-time purchases must be fully settled at creation
- later payment flow for one-time parties is intentionally blocked

### Supervisor authorization
Supervisor authorization is used for selected sensitive/destructive actions, such as:
- invoice deletes
- expense delete
- equipment delete
- employee disable
- reservation cancel
- sales return creation
- registered client-return purchase creation

---

## Solution Structure

```text
MaterialManagement.sln
├── MaterialManagement            # ASP.NET Core MVC web app
├── MaterialManagement.BLL        # business logic layer
├── MaterialManagement.DAL        # data access layer
└── IntegrationTestSandbox        # smoke / verification harness
```

### Web Project Highlights
Inside `MaterialManagement` you will find:

- `Controllers/`
- `Views/`
- `Services/`
- `Models/`
- `wwwroot/css`
- `wwwroot/js`

### BLL Highlights
Inside `MaterialManagement.BLL`:

- `Service/Abstractions`
- `Service/Implementations`
- `ModelVM/...`
- `Features/Invoicing`
- `Features/Payments`
- `Features/Returns`
- `Helper/AutoMapperProfile.cs`

### DAL Highlights
Inside `MaterialManagement.DAL`:

- `Entities/`
- `DB/MaterialManagementContext.cs`
- `Repo/Abstractions`
- `Repo/Implementations`
- `Enums/Enums.cs`
- `Migrations/`

---

## Prerequisites

Install these on the target machine:

- **Windows**
- **.NET 8 SDK**
- **SQL Server** or **SQL Server Express**
- Optional:
  - **Visual Studio 2022**
  - or **VS Code** with C# tooling

---

## Configuration

### 1) Connection string

Update the connection string in:

- `MaterialManagement/appsettings.json`
- and/or
- `MaterialManagement/appsettings.Development.json`

Example:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=MaterialManagementDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Adjust `Server=` to match the SQL Server instance on the target machine.

### 2) Supervisor password

Set supervisor credentials in:

- `MaterialManagement/appsettings.Development.json`
- or environment variables

Example:

```json
"SupervisorAuthorization": {
  "Password": "************"
}
```

You may also use `PasswordHash` if you want a safer setup.

> Important: if the active environment has no configured supervisor secret, protected actions fail closed.

---

## First-Time Setup

From the solution root:

```powershell
dotnet restore
dotnet build MaterialManagement.sln -m:1 -v:minimal
```

Then apply migrations:

```powershell
dotnet ef database update --project MaterialManagement.DAL\MaterialManagement.DAL.csproj --startup-project MaterialManagement\MaterialManagement.PL.csproj --context MaterialManagementContext
```

---

## Run the Project

### Option A — normal run

```powershell
dotnet run --project MaterialManagement\MaterialManagement.PL.csproj
```

### Option B — if apphost/exe causes trouble

```powershell
dotnet build MaterialManagement\MaterialManagement.PL.csproj --no-restore -m:1 -p:UseAppHost=false
dotnet .\MaterialManagement\bin\Debug\net8.0\MaterialManagement.PL.dll
```

---

## Publishing for Another PC

If you want a cleaner deployment package instead of moving the full source:

```powershell
dotnet publish MaterialManagement\MaterialManagement.PL.csproj -c Release -o .\publish
```

Then copy the `publish` folder to the target machine, along with:
- the correct appsettings values
- an available SQL Server
- the target database after migrations are applied

---

## Smoke / Verification Project

`IntegrationTestSandbox` exists to validate critical flows such as:

- phase 1 financial guards
- purchase invoice mode rules
- reservations stabilization
- historical visibility
- report math
- one-time party flow

Typical commands:

```powershell
dotnet build IntegrationTestSandbox\IntegrationTestSandbox.csproj -m:1 -v:minimal
dotnet run --project IntegrationTestSandbox\IntegrationTestSandbox.csproj --no-build -- one-time-party
```

---

## Reports

The system includes report pages for:

- Account Statement
- Material Movement
- Profit Report

The account statement UI was refined toward a **customer ledger / worksheet style** suitable for laptop use and printing.

---

## UI / UX Direction

The current project direction is:

- desktop-first
- laptop-friendly
- full-width where useful
- Arabic RTL
- worksheet / Excel / ledger feel
- data-first, operational ERP style
- less decorative cards, more practical grids/tables

---

## Known Notes

- Some warnings may still appear during build, including:
  - AutoMapper package advisory
  - older nullable warnings
  - some unused variable warnings
- These did not block the verified builds discussed during development.

---

## Recommended Post-Install Checks

After installing on another PC, verify:

1. connection string works
2. migrations are applied
3. supervisor password is configured
4. dashboard opens
5. sales invoice flow works
6. purchase invoice flow works
7. payments work
8. reservations work
9. reports open correctly
10. protected actions request supervisor authorization

---

## License / Internal Use

Copyright (c) 2026 Abdelrahman Abaza

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
