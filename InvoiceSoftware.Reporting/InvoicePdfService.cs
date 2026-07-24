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
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(30);
                page.MarginVertical(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI").FontColor("#111111"));
                page.Content().Element(c => ComposePaymentReceipt(c, receipt, company));
                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor("#D8D8D8");
                    footer.Item().DefaultTextStyle(x => x.FontSize(8)).PaddingTop(10).AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void ComposePaymentReceipt(IContainer container, Receipt receipt, CompanySettings company)
    {
        var invoice = receipt.Invoice;
        var customer = receipt.Customer;
        var customerName = invoice?.CustomerNameSnapshot ?? customer?.Name ?? "Customer";
        var customerAddress = invoice?.CustomerAddressSnapshot ?? customer?.Address;
        var customerPhone = invoice?.CustomerTelephoneSnapshot ?? customer?.Telephone ?? customer?.Mobile;
        var paidDate = receipt.ReceiptDate.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var amount = ReceiptCurrency(receipt.PaymentAmount, company);
        var description = !string.IsNullOrWhiteSpace(receipt.Notes)
            ? receipt.Notes
            : invoice is not null
                ? $"Payment for invoice {invoice.ReferenceNumber}"
                : "Payment received";

        container.Column(page =>
        {
            page.Item().Row(header =>
            {
                header.RelativeItem().Text("Receipt").FontSize(26).Bold();
                header.ConstantItem(160).Height(64).Element(logo => ReceiptLogo(logo, company));
            });

            page.Item().PaddingTop(20).Column(meta =>
            {
                ReceiptMeta(meta, "Invoice number", invoice?.ReferenceNumber ?? "-");
                ReceiptMeta(meta, "Receipt number", receipt.ReferenceNumber);
                ReceiptMeta(meta, "Date paid", paidDate);
            });

            page.Item().PaddingTop(20).Row(parties =>
            {
                parties.RelativeItem().PaddingRight(28).Column(from =>
                {
                    from.Item().Text(company.CompanyName).Bold();
                    ReceiptOptionalLine(from, company.Address);
                    ReceiptOptionalLine(from, company.Telephone ?? company.Mobile);
                    ReceiptOptionalLine(from, company.Email);
                    ReceiptOptionalLine(from, company.Website);
                });
                parties.RelativeItem().Column(to =>
                {
                    to.Item().Text("Bill to").Bold();
                    to.Item().PaddingTop(5).Text(customerName);
                    ReceiptOptionalLine(to, customerAddress);
                    ReceiptOptionalLine(to, customerPhone);
                    ReceiptOptionalLine(to, customer?.Email);
                });
            });

            page.Item().PaddingTop(26).Text($"{amount} paid on {paidDate}").FontSize(18).Bold();

            page.Item().PaddingTop(24).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(82);
                    columns.ConstantColumn(82);
                });

                ReceiptTableHeader(table, "Description", false);
                ReceiptTableHeader(table, "Qty", true);
                ReceiptTableHeader(table, "Unit price", true);
                ReceiptTableHeader(table, "Amount", true);

                table.Cell().PaddingTop(7).PaddingRight(8).Column(details =>
                {
                    details.Item().Text(description);
                    if (!string.IsNullOrWhiteSpace(invoice?.ProjectName))
                        details.Item().PaddingTop(2).Text(invoice.ProjectName).FontSize(9).FontColor("#555555");
                });
                ReceiptTableValue(table, "1", true);
                ReceiptTableValue(table, amount, true);
                ReceiptTableValue(table, amount, true);
            });

            page.Item().PaddingTop(18).AlignRight().Width(278).Column(totals =>
            {
                ReceiptSummary(totals, "Subtotal", amount);
                ReceiptSummary(totals, "Total", amount);
                ReceiptSummary(totals, "Amount paid", amount, true);
            });

            page.Item().PaddingTop(28).Text("Payment history").FontSize(18).Bold();
            page.Item().PaddingTop(18).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.55f);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.6f);
                });

                ReceiptTableHeader(table, "Payment method", false);
                ReceiptTableHeader(table, "Date", false);
                ReceiptTableHeader(table, "Amount paid", false);
                ReceiptTableHeader(table, "Receipt number", true);

                var method = ReceiptPaymentMethod(receipt);
                ReceiptTableValue(table, method, false);
                ReceiptTableValue(table, paidDate, false);
                ReceiptTableValue(table, amount, false);
                ReceiptTableValue(table, receipt.ReferenceNumber, true);
            });
        });
    }

    private static void ReceiptLogo(IContainer container, CompanySettings company)
    {
        if (!string.IsNullOrWhiteSpace(company.LogoPath) && File.Exists(company.LogoPath))
            container.AlignRight().Image(company.LogoPath).FitArea();
        else
            container.AlignRight().AlignMiddle().Text(company.CompanyName).FontSize(9).Bold();
    }

    private static void ReceiptMeta(ColumnDescriptor column, string label, string value)
        => column.Item().PaddingBottom(2).Row(row =>
        {
            row.ConstantItem(104).Text(label).Bold();
            row.RelativeItem().Text(value);
        });

    private static void ReceiptOptionalLine(ColumnDescriptor column, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            column.Item().PaddingTop(2).Text(value);
    }

    private static void ReceiptTableHeader(TableDescriptor table, string text, bool alignRight)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor("#222222").PaddingBottom(5);
        if (alignRight) cell = cell.AlignRight();
        cell.Text(text).FontSize(9);
    }

    private static void ReceiptTableValue(TableDescriptor table, string text, bool alignRight)
    {
        var cell = table.Cell().PaddingTop(7);
        if (alignRight) cell = cell.AlignRight();
        cell.Text(text);
    }

    private static void ReceiptSummary(ColumnDescriptor column, string label, string value, bool bold = false)
        => column.Item().BorderTop(0.5f).BorderColor("#D8D8D8").PaddingVertical(3).Row(row =>
        {
            var left = row.RelativeItem().Text(label);
            var right = row.ConstantItem(92).AlignRight().Text(value);
            if (bold)
            {
                left.Bold();
                right.Bold();
            }
        });

    private static string ReceiptCurrency(decimal amount, CompanySettings company)
    {
        var prefix = string.IsNullOrWhiteSpace(company.CurrencySymbol) ? $"{company.Currency} " : company.CurrencySymbol;
        return $"{prefix}{amount:N2}";
    }

    private static string ReceiptPaymentMethod(Receipt receipt)
    {
        var method = receipt.PaymentMethod switch
        {
            PaymentMethodType.BankTransfer => "Bank transfer",
            PaymentMethodType.CreditCard => "Credit card",
            _ => receipt.PaymentMethod.ToString()
        };
        return string.IsNullOrWhiteSpace(receipt.TransactionReference) ? method : $"{method} - {receipt.TransactionReference}";
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
