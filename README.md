# Invoice Management Software

Professional offline invoice management desktop application built with C#, .NET 8, WPF, MVVM, SQLite, Entity Framework Core, and QuestPDF.

## Visual Structure From The Supplied Invoice

The print/PDF layout follows the reference invoice: company logo and contact details at top-left, large `INVOICE` title and date/reference/due-date metadata at top-right, a dark blue `BILL TO` band, striped item table with description and amount columns, comments/terms block under the table, totals aligned on the lower-right, signature/stamp area, thank-you message, and footer with the invoice reference and page numbering.

## Projects

- `InvoiceSoftware.App`: WPF shell, MVVM viewmodels, theme, navigation, and commands.
- `InvoiceSoftware.Core`: domain entities, enums, and invoice calculation logic.
- `InvoiceSoftware.Data`: EF Core SQLite context, indexes, seed data, and database path conventions.
- `InvoiceSoftware.Services`: application services for references, invoices, receipts, dashboard, lookup data, audit, backup, and restore.
- `InvoiceSoftware.Reporting`: QuestPDF invoice and receipt templates.
- `InvoiceSoftware.Tests`: calculation and validation tests.

## Database Location

Runtime data is stored in:

`%LOCALAPPDATA%\InvoiceSoftware\invoice-software.db`

Uploaded company assets are copied to:

`%LOCALAPPDATA%\InvoiceSoftware\Assets`

Backups are written to:

`%LOCALAPPDATA%\InvoiceSoftware\Backups`

## Build And Run

```powershell
dotnet restore .\InvoiceSoftware.slnx
dotnet build .\InvoiceSoftware.slnx
dotnet run --project .\InvoiceSoftware.App\InvoiceSoftware.App.csproj
```

## Publish Self-Contained Windows App

```powershell
dotnet publish .\InvoiceSoftware.App\InvoiceSoftware.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published files will be under:

`InvoiceSoftware.App\bin\Release\net8.0-windows\win-x64\publish`

## Installer

Create an installer from the publish folder using Visual Studio Installer Projects, WiX Toolset, Advanced Installer, or Inno Setup. Configure the installer to install program files under `Program Files` while leaving the SQLite database in `%LOCALAPPDATA%`.

## Notes

The current version includes a working local database, sample company/customer/product data, invoice creation/editing, receipt posting, dashboard metrics, PDF export, soft delete, audit entries, backup and restore, and resource/theme structure for future Arabic localization and right-to-left support.

## Store And Inventory

The **Store / Inventory** workspace manages products, categories, suppliers, stock adjustments, CSV import/export, printable product lists, and the permanent stock-movement ledger. Product selection in invoice entry searches reference numbers, barcodes, names, and descriptions. Saving an invoice finalizes it and synchronizes stock transactionally; later edits apply only the quantity difference, while cancellation or deletion restores stock. Services and products with stock tracking disabled never affect inventory. The **Inventory Reports** workspace provides current-stock, low-stock, sales, revenue, cost, gross-profit, and margin reporting with CSV export.

Migration `AddStoreInventoryManagement` creates the normalized inventory schema. `BackfillLegacyInventoryData` preserves existing products, derives product names and stock-tracking behavior, links matching legacy categories, and records opening stock balances. Application startup applies both migrations automatically, including after an older backup is restored.
