# Invoice Management Software

## License activation (v1.2.1 and later)

Invoice Maker v1.2.1 and later requires a signed, installation-specific `.invoicelicense` file. On first start, copy the displayed `INV-...` installation ID and send it to the supplier. The supplier creates the license with **MicroBrain License Manager v4.3.0** by selecting **Invoice App**. Use **Activate License** to import the returned file.

The application validates the ECDSA signature, Invoice App product/format, machine-derived installation ID, issue date, and optional expiry date before opening. The public verification key is embedded in the application; the private supplier key is never included. The installed license is stored separately from the SQLite database under `%LOCALAPPDATA%\InvoiceSoftware`, so database backup/restore does not transfer a license to another computer.

Professional offline invoice management desktop application built with C#, .NET 8, WPF, MVVM, SQLite, Entity Framework Core, and QuestPDF.

## Download And Run The Application

1. Open the [latest Invoice Software release](https://github.com/fadiassi411/Invoice_App/releases/latest).
2. Under **Assets**, download the file named `InvoiceSoftwareApp-vX.X.X-Windows-x64.zip`.
3. Extract the complete ZIP file to a normal folder. Do not run the application from inside the ZIP.
4. Keep all extracted files and the `LatoFont` folder together.
5. Double-click `InvoiceSoftware.App.exe` to start the application.

The release is self-contained, so the customer does not need to install .NET. Application data is stored separately under `%LOCALAPPDATA%\InvoiceSoftware`, so installing a newer version does not delete existing invoices, customers, receipts, quotations, or inventory data.

> **Important:** **Code → Download ZIP** downloads the source code for developers. Customers should download the ready-to-use Windows ZIP from the **Releases** page instead.

## Visual Structure From The Supplied Invoice

The print/PDF layout follows the reference invoice: company logo and contact details at top-left, large `INVOICE` title and date/reference/due-date metadata at top-right, a dark blue `BILL TO` band, striped item table with description and amount columns, comments/terms block under the table, totals aligned on the lower-right, signature/stamp area, thank-you message, and footer with the invoice reference and page numbering.

## Projects

- `InvoiceSoftware.App`: WPF shell, MVVM viewmodels, theme, navigation, and commands.
- `InvoiceSoftware.Core`: domain entities, enums, and centralized invoice/quotation calculation logic.
- `InvoiceSoftware.Data`: EF Core SQLite context, indexes, seed data, and database path conventions.
- `InvoiceSoftware.Services`: application services for references, invoices, quotations, receipts, inventory, audit, backup, and restore.
- `InvoiceSoftware.Reporting`: separate QuestPDF invoice, quotation, and receipt templates.
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

## Quotation Management

The **Quotations** workspace creates searchable, revision-controlled quotations from stock items or manual service lines. Quotations use atomic `QUO-YYYY-000001` numbering, store customer/item snapshots, support line and overall discounts, inclusive/exclusive/exempt tax, validity/expiry, status history, duplication, CSV export, archiving, PDF preview/print/export, and transactional conversion of an accepted quotation into a draft invoice. Saving or revising a quotation never changes stock; inventory is affected only by the existing finalized-invoice workflow.

To remove an erroneous quotation in v1.2.2 or later, select its row in **Quotations** and choose **Delete Permanently**. If it was archived already, tick **Include archived** and click **Search** first. This removes only the selected revision, its items, and its status history; it cannot be undone and its number is not reused. Delete later revisions first. A quotation linked to an invoice cannot be deleted. Use **Archive** instead when you need to hide a quotation while retaining its history.

In the quotation editor, enable **PDF: show total price only** to omit line prices and the subtotal breakdown from the customer-facing PDF while retaining the grand total. Tick **Hide in PDF** on an individual row to omit that row from the PDF. Hidden rows remain saved and still contribute to the quotation total; these settings carry forward when duplicating or revising a quotation.

Migration `AddQuotationManagement` adds normalized quotation, quotation-item, and status-history tables; unique number/revision constraints; search indexes; quotation settings; product/customer extensions; and quotation-to-invoice links. Existing customer, product, invoice, receipt, inventory, and backup data is preserved. The automated suite covers calculations, numbering, snapshots, revisions, expiry, search, one-page and multi-page PDFs, backup payloads, migration upgrades, stock isolation, conversion totals, and duplicate-conversion prevention.
