using InvoiceSoftware.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InvoiceSoftware.Reporting;

public interface IInvoicePdfService
{
    void ExportInvoice(Invoice invoice, CompanySettings company, string filePath);
    void ExportReceipt(Receipt receipt, CompanySettings company, string filePath);
}

public sealed class InvoicePdfService : IInvoicePdfService
{
    public InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportInvoice(Invoice invoice, CompanySettings company, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));
                page.Header().Element(c => ComposeInvoiceHeader(c, invoice, company));
                page.Content().Element(c => ComposeInvoiceContent(c, invoice, company));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"{invoice.ReferenceNumber}  |  ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    public void ExportReceipt(Receipt receipt, CompanySettings company, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));
                page.Content().Element(c => ComposePaymentReceipt(c, receipt, company));
            });
        }).GeneratePdf(filePath);
    }

    private static void ComposePaymentReceipt(IContainer container, Receipt receipt, CompanySettings company)
    {
        var amountText = AmountToWords(receipt.PaymentAmount, company.Currency);
        var invoice = receipt.Invoice;
        var customer = receipt.Customer;
        var invoiceTotal = invoice?.GrandTotal ?? receipt.PaymentAmount;
        var remainingAmount = invoice?.RemainingBalance ?? 0;
        var contactPerson = !string.IsNullOrWhiteSpace(company.ContactPersonName)
            ? company.ContactPersonName
            : receipt.ReceivedBy;
        var description = !string.IsNullOrWhiteSpace(receipt.Notes)
            ? receipt.Notes
            : invoice is not null
                ? $"I hereby acknowledge receipt of {amountText} ({company.Currency} {receipt.PaymentAmount:N2}) from {customer?.Name ?? "Customer"} as payment for invoice {invoice.ReferenceNumber}."
                : $"I hereby acknowledge receipt of {amountText} ({company.Currency} {receipt.PaymentAmount:N2}).";

        container.PaddingHorizontal(18).PaddingTop(18).Column(outer =>
        {
            outer.Item().Border(1).BorderColor(Colors.Black).MinHeight(700).Column(col =>
            {
                col.Item().Padding(4).MinHeight(125).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().PaddingTop(64).Text("FROM").FontSize(10).Bold();
                        left.Item().Text(contactPerson ?? company.CompanyName);
                        left.Item().Text(company.Email ?? "").FontColor("#0563c1").Underline();
                        left.Item().Text(company.Mobile ?? company.Telephone ?? "");
                    });
                    row.RelativeItem().Column(right =>
                    {
                        right.Item().AlignCenter().Text("Receipt").FontSize(28).Bold().FontColor("#555555");
                        right.Item().PaddingTop(18).Row(meta =>
                        {
                            meta.ConstantItem(70).AlignRight().Text("DATE:").Bold();
                            meta.RelativeItem().BorderBottom(1).PaddingLeft(4).Text(receipt.ReceiptDate.ToString("d"));
                        });
                        right.Item().Row(meta =>
                        {
                            meta.ConstantItem(70).AlignRight().Text("RECEIPT #").Bold();
                            meta.RelativeItem().BorderBottom(1).PaddingLeft(4).Text(receipt.ReferenceNumber);
                        });
                        right.Item().PaddingTop(26).Text("TO").FontSize(10).Bold();
                        right.Item().Text($"Name: {customer?.Name ?? "Customer"}");
                        right.Item().Text(customer?.Address ?? "");
                        right.Item().Text($"Phone: {customer?.Telephone ?? customer?.Mobile ?? ""}");
                    });
                });

                col.Item().PaddingTop(8).PaddingLeft(40).Text(t =>
                {
                    t.Span("DUE:  ").Bold();
                    t.Span((invoice?.DueDate ?? receipt.ReceiptDate).ToString("d"));
                });

                col.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(5);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1.1f);
                        cols.RelativeColumn(1.35f);
                    });

                    HeaderCellBlack(table, "Description");
                    HeaderCellBlack(table, "Quantity");
                    HeaderCellBlack(table, "Price");
                    HeaderCellBlack(table, "Amount");

                    table.Cell().Border(1).MinHeight(220).Padding(4).AlignTop().Text(description).FontSize(11);
                    table.Cell().Border(1).MinHeight(220).AlignMiddle().AlignCenter().Text("1.00").FontSize(11);
                    table.Cell().Border(1).MinHeight(220).AlignMiddle().AlignCenter().Text($"{company.CurrencySymbol} {receipt.PaymentAmount:N2}").FontSize(11);
                    table.Cell().Border(1).MinHeight(220).AlignMiddle().AlignCenter().Text($"{company.CurrencySymbol} {receipt.PaymentAmount:N2}").FontSize(11);
                });

                col.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).MinHeight(88).Row(row =>
                {
                    row.RelativeItem().Text("");
                    row.ConstantItem(250).PaddingRight(8).Column(totals =>
                    {
                        ReceiptTotal(totals, "Invoice Total", $"{company.CurrencySymbol} {invoiceTotal:N2}");
                        ReceiptTotal(totals, "Amount Received", $"{company.CurrencySymbol} {receipt.PaymentAmount:N2}");
                        totals.Item().BorderTop(1).PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("REMAINING AMOUNT").FontSize(13).Bold();
                            r.ConstantItem(110).AlignRight().Text($"{company.CurrencySymbol} {remainingAmount:N2}").FontSize(13);
                        });
                    });
                });

                col.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).Padding(4).MinHeight(54).Column(notes =>
                {
                    notes.Item().Text("Notes").FontSize(14);
                    notes.Item().Text($"Only: {amountText}.");
                    if (invoice is not null)
                        notes.Item().Text($"Remaining amount after this receipt: {company.CurrencySymbol} {remainingAmount:N2}.");
                });

                col.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).Height(145).PaddingTop(44).AlignCenter().Text("PAID").FontSize(40).FontColor("#9a9a9a");
            });

            outer.Item().AlignCenter().PaddingTop(16).Text("1");
        });
    }

    private static void ComposeInvoiceHeader(IContainer container, Invoice invoice, CompanySettings company)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                AddLogo(col.Item().Height(64), company.LogoPath);
                AddOptionalText(col, company.Address);
                AddContactLine(col, ("Phone", company.Telephone), ("Mobile", company.Mobile));
                AddContactLine(col, ("Email", company.Email), ("Website", company.Website));
                AddOptionalLabeledText(col, "Tax ID", company.TaxIdentificationNumber);
            });
            row.ConstantItem(205).Column(col =>
            {
                col.Item().AlignLeft().Text("INVOICE").FontSize(30).Bold().FontColor("#6d85c7");
                AddMetaRow(col, "DATE", invoice.InvoiceDate.ToString("d"));
                AddMetaRow(col, "INVOICE #", invoice.ReferenceNumber);
                AddMetaRow(col, "DUE DATE", invoice.DueDate.ToString("d"));
                AddMetaRow(col, "STATUS", invoice.PaymentStatus.ToString());
            });
        });
    }

    private static void ComposeInvoiceContent(IContainer container, Invoice invoice, CompanySettings company)
    {
        container.PaddingTop(22).Column(col =>
        {
            col.Item().Width(210).Background("#2f488f").Padding(4).Text("BILL TO").FontColor(Colors.White).Bold();
            col.Item().PaddingTop(4).Text(invoice.CustomerNameSnapshot ?? invoice.Customer?.Name ?? "");
            col.Item().Text($"Attention: {invoice.CustomerAttentionNameSnapshot ?? invoice.Customer?.AttentionName ?? invoice.Customer?.ContactPerson ?? ""}");
            col.Item().Text(invoice.CustomerAddressSnapshot ?? "");
            col.Item().Text(invoice.CustomerTelephoneSnapshot ?? "");

            col.Item().PaddingTop(24).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(28);
                    cols.RelativeColumn(4);
                    cols.ConstantColumn(44);
                    cols.ConstantColumn(58);
                    cols.ConstantColumn(54);
                    cols.ConstantColumn(44);
                    cols.ConstantColumn(62);
                });

                table.Header(header =>
                {
                    HeaderCell(header, "#");
                    HeaderCell(header, "DESCRIPTION");
                    HeaderCell(header, "QTY");
                    HeaderCell(header, "UNIT PRICE");
                    HeaderCell(header, "DISCOUNT");
                    HeaderCell(header, "TAX");
                    HeaderCell(header, "AMOUNT");
                });

                var index = 0;
                foreach (var item in invoice.Items.OrderBy(x => x.SortOrder))
                {
                    var bg = index++ % 2 == 0 ? "#f1f1f1" : "#ffffff";
                    BodyCell(table, item.SortOrder.ToString(), bg);
                    BodyCell(table, item.Description, bg);
                    BodyCell(table, item.Quantity.ToString("N2"), bg);
                    BodyCell(table, item.UnitPrice.ToString("N2"), bg);
                    BodyCell(table, item.LineDiscount.ToString("N2"), bg);
                    BodyCell(table, item.LineTax.ToString("N2"), bg);
                    BodyCell(table, item.LineTotal.ToString("N2"), bg);
                }
            });

            col.Item().PaddingTop(22).Row(row =>
            {
                row.ConstantItem(350).Column(left =>
                {
                    left.Item().Background("#2f488f").Padding(4).Text("OTHER COMMENTS").FontColor(Colors.White).Bold();
                    left.Item().Border(1).BorderColor("#b7b7b7").Padding(10).MinHeight(96).Text($"{company.PaymentTerms}\n\n{invoice.Notes}\n{company.InvoiceFooterNotes}");
                });
                row.ConstantItem(24).Text("");
                row.RelativeItem().Column(totals =>
                {
                    totals.Item().BorderBottom(1).BorderColor("#9aa7c8").PaddingBottom(3).Text("SUMMARY").Bold().FontColor("#2f488f");
                    AddTotal(totals, "Subtotal", invoice.Subtotal, company.CurrencySymbol);
                    AddTotal(totals, "Discount", invoice.TotalDiscount, company.CurrencySymbol);
                    AddTotal(totals, "Tax", invoice.TotalTax, company.CurrencySymbol);
                    AddTotal(totals, "Shipping", invoice.ShippingCharges, company.CurrencySymbol);
                    if (invoice.AdditionalCharges != 0)
                        AddTotal(totals, "Additional charges", invoice.AdditionalCharges, company.CurrencySymbol);
                    if (invoice.RoundingAdjustment != 0)
                        AddTotal(totals, "Rounding adjustment", invoice.RoundingAdjustment, company.CurrencySymbol);
                    AddTotal(totals, "Previous balance", invoice.PreviousBalance, company.CurrencySymbol);
                    AddTotal(totals, "Amount paid", invoice.AmountPaid, company.CurrencySymbol);
                    totals.Item().PaddingTop(3).LineHorizontal(1).LineColor("#9aa7c8");
                    AddTotal(totals, "TOTAL", invoice.GrandTotal, company.CurrencySymbol, true);
                    AddTotal(totals, "BALANCE", invoice.RemainingBalance, company.CurrencySymbol, true);
                });
            });

            col.Item().PaddingTop(28).Row(row =>
            {
                row.RelativeItem().Text($"Make all checks payable to {company.CompanyName}");
                row.ConstantItem(230).Text("Signature / Stamp: __________________");
            });
            col.Item().PaddingTop(12).AlignCenter().Text("Thank You For Your Business!").Italic().Bold();
        });
    }

    private static void AddLogo(IContainer container, string? logoPath)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            container.Image(logoPath).FitArea();
        else
            container.Text("Company Logo").FontColor("#1b75bb").Bold();
    }

    private static void AddOptionalText(ColumnDescriptor col, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            col.Item().Text(value);
    }

    private static void AddOptionalLabeledText(ColumnDescriptor col, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            col.Item().Text($"{label}: {value}");
    }

    private static void AddContactLine(ColumnDescriptor col, params (string Label, string? Value)[] items)
    {
        var parts = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Label}: {x.Value}")
            .ToArray();

        if (parts.Length > 0)
            col.Item().Text(string.Join("   ", parts));
    }

    private static void AddMetaRow(ColumnDescriptor col, string label, string value)
        => col.Item().Row(row =>
        {
            row.ConstantItem(72).AlignLeft().Text(label).FontSize(7).Bold();
            row.RelativeItem().AlignLeft().Text(value).FontSize(9);
        });

    private static void ReceiptMetaRow(ColumnDescriptor col, string label, string value)
        => col.Item().PaddingBottom(10).Row(row =>
        {
            row.ConstantItem(42).Text(label).FontSize(9);
            row.ConstantItem(10).Text(":").FontSize(9);
            row.RelativeItem().BorderBottom(1).BorderColor("#9cc3ec").PaddingBottom(3).Text(value).FontSize(9);
        });

    private static void ReceiptLine(ColumnDescriptor col, string label, string value)
        => col.Item().PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(82).Text(label).FontSize(9);
            row.ConstantItem(10).Text(":").FontSize(9);
            row.RelativeItem().BorderBottom(1).BorderColor("#9cc3ec").PaddingBottom(3).Text(value).FontSize(9).FontColor(label == "Amount" ? "#0b70bd" : Colors.Black);
        });

    private static void HeaderCell(TableCellDescriptor header, string text)
        => header.Cell().Background("#2f488f").Border(1).BorderColor("#2f488f").Padding(3).Text(text).FontColor(Colors.White).Bold().AlignCenter();

    private static void HeaderCellBlack(TableDescriptor table, string text)
        => table.Cell().Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().Text(text).FontSize(11).Bold();

    private static void BodyCell(TableDescriptor table, string text, string background)
        => table.Cell().Background(background).BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor("#9c9c9c").Padding(4).Text(text);

    private static void ReceiptTotal(ColumnDescriptor col, string label, string value)
        => col.Item().Row(row =>
        {
            row.RelativeItem().AlignRight().Text(label).FontSize(12).Bold();
            row.ConstantItem(110).AlignRight().Text(value).FontSize(12);
        });

    private static void AddTotal(ColumnDescriptor col, string label, decimal amount, string symbol, bool bold = false)
        => col.Item().PaddingTop(2).Row(row =>
        {
            var labelText = row.RelativeItem().Text(label);
            var valueText = row.ConstantItem(92).AlignRight().Text($"{symbol}{amount:N2}");
            if (bold)
            {
                labelText.Bold();
                valueText.Bold();
            }
        });

    private static string AmountToWords(decimal amount, string currency)
    {
        var whole = (long)Math.Floor(amount);
        var cents = (int)Math.Round((amount - whole) * 100m, MidpointRounding.AwayFromZero);
        var words = $"{NumberToWords(whole)} {currency}";
        if (cents > 0) words += $" and {NumberToWords(cents)} Cents";
        return words;
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string[] units =
        [
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        ];
        string[] tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

        if (number < 20) return units[number];
        if (number < 100) return tens[number / 10] + (number % 10 > 0 ? "-" + units[number % 10] : "");
        if (number < 1_000) return units[number / 100] + " Hundred" + (number % 100 > 0 ? " " + NumberToWords(number % 100) : "");
        if (number < 1_000_000) return NumberToWords(number / 1_000) + " Thousand" + (number % 1_000 > 0 ? " " + NumberToWords(number % 1_000) : "");
        if (number < 1_000_000_000) return NumberToWords(number / 1_000_000) + " Million" + (number % 1_000_000 > 0 ? " " + NumberToWords(number % 1_000_000) : "");
        return NumberToWords(number / 1_000_000_000) + " Billion" + (number % 1_000_000_000 > 0 ? " " + NumberToWords(number % 1_000_000_000) : "");
    }
}
