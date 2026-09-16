using InvoiceSoftware.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InvoiceSoftware.Reporting;

public interface IQuotationPdfService
{
    void ExportQuotation(Quotation quotation, CompanySettings company, string filePath);
}

/// <summary>Creates the customer-facing A4 quotation document from persisted snapshots.</summary>
public sealed class QuotationPdfService : IQuotationPdfService
{
    private const string Navy = "#17365D";
    private const string Teal = "#1E7480";
    private const string Gold = "#C6942D";
    private const string Pale = "#EEF5F6";

    public QuotationPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public void ExportQuotation(Quotation quotation, CompanySettings company, string filePath)
    {
        ArgumentNullException.ThrowIfNull(quotation);
        ArgumentNullException.ThrowIfNull(company);
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(30);
            page.MarginVertical(26);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor("#202A35"));
            page.Header().Element(x => Header(x, quotation, company));
            page.Content().PaddingTop(14).Element(x => Content(x, quotation, company));
            page.Footer().Element(x => Footer(x, quotation));
        })).GeneratePdf(filePath);
    }

    private static void Header(IContainer container, Quotation quotation, CompanySettings company)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(company.LogoPath) && File.Exists(company.LogoPath))
                        left.Item().Height(52).Width(165).Image(company.LogoPath).FitArea();
                    else
                        left.Item().Text(company.CompanyName).FontSize(16).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(company.Address)) left.Item().PaddingTop(3).Text(company.Address);
                    left.Item().Text(Contact(company));
                    if (!string.IsNullOrWhiteSpace(company.TaxIdentificationNumber)) left.Item().Text($"Tax/VAT: {company.TaxIdentificationNumber}");
                });
                row.ConstantItem(220).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("QUOTATION").FontSize(28).Bold().FontColor(Teal).LetterSpacing(0.08f);
                    right.Item().AlignRight().Text(quotation.DisplayNumber).FontSize(11).Bold().FontColor(Navy);
                    right.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(90); });
                        Meta(table, "Quotation date", quotation.QuotationDate.ToString("dd MMM yyyy"));
                        Meta(table, "Valid until", quotation.ValidUntil.ToString("dd MMM yyyy"));
                        Meta(table, "Currency", quotation.CurrencyCode);
                        Meta(table, "Status", quotation.Status.ToString());
                    });
                });
            });
            column.Item().PaddingTop(9).LineHorizontal(2).LineColor(Gold);
        });
    }

    private static void Content(IContainer container, Quotation quotation, CompanySettings company)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(x => InformationCard(x, "QUOTATION TO", new[]
                {
                    quotation.CustomerCompanySnapshot ?? quotation.CustomerNameSnapshot,
                    quotation.ContactPersonSnapshot is null ? null : $"Attention: {quotation.ContactPersonSnapshot}",
                    quotation.BillingAddressSnapshot,
                    Join("Tel", quotation.TelephoneSnapshot, quotation.MobileSnapshot),
                    quotation.EmailSnapshot,
                    quotation.TaxNumberSnapshot is null ? null : $"Tax/VAT: {quotation.TaxNumberSnapshot}"
                }));
                row.ConstantItem(12);
                row.RelativeItem().Element(x => InformationCard(x, "PROJECT / ENQUIRY", new[]
                {
                    quotation.ProjectName is null ? null : $"Project: {quotation.ProjectName}",
                    quotation.ProjectLocation is null ? null : $"Location: {quotation.ProjectLocation}",
                    quotation.Subject is null ? null : $"Subject: {quotation.Subject}",
                    quotation.CustomerReference is null ? null : $"Customer ref: {quotation.CustomerReference}",
                    quotation.Salesperson is null ? null : $"Prepared by: {quotation.Salesperson}"
                }));
            });

            var visibleItems = quotation.Items.Where(x => !x.HideOnPdf).OrderBy(x => x.DisplayOrder).ToList();
            if (visibleItems.Count > 0) column.Item().PaddingTop(14).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.ConstantColumn(62);
                    columns.RelativeColumn(3.2f);
                    columns.ConstantColumn(38);
                    columns.ConstantColumn(31);
                    if (!quotation.HideItemPricesOnPdf)
                    {
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(39);
                        columns.ConstantColumn(61);
                    }
                });
                table.Header(header =>
                {
                    TableHeader(header, "No."); TableHeader(header, "Reference"); TableHeader(header, "Description");
                    TableHeader(header, "Qty"); TableHeader(header, "Unit");
                    if (!quotation.HideItemPricesOnPdf)
                    {
                        TableHeader(header, "Unit price"); TableHeader(header, "Discount");
                        TableHeader(header, "Tax"); TableHeader(header, "Total");
                    }
                });

                var shaded = false;
                foreach (var item in visibleItems)
                {
                    if (item.ItemType == QuotationItemType.SectionHeader)
                    {
                        table.Cell().ColumnSpan(quotation.HideItemPricesOnPdf ? 5u : 9u).Background(Navy).PaddingVertical(5).PaddingHorizontal(7)
                            .Text(item.DescriptionSnapshot).Bold().FontColor(Colors.White);
                        continue;
                    }
                    var background = shaded ? "#F7F9FA" : "#FFFFFF";
                    shaded = !shaded;
                    Body(table, item.LineNumber.ToString(), background, TextAlign.Center);
                    Body(table, item.PartNumberSnapshot ?? item.ItemReferenceSnapshot ?? "", background);
                    Body(table, Description(item, company.ShowAvailableStockOnPrintedQuotation), background);
                    Body(table, item.Quantity.ToString("0.####"), background, TextAlign.Right);
                    Body(table, item.UnitSnapshot, background, TextAlign.Center);
                    if (!quotation.HideItemPricesOnPdf)
                    {
                        Body(table, item.UnitPrice.ToString("N2"), background, TextAlign.Right);
                        Body(table, item.DiscountAmount.ToString("N2"), background, TextAlign.Right);
                        Body(table, item.TaxAmount.ToString("N2"), background, TextAlign.Right);
                        Body(table, item.LineTotal.ToString("N2"), background, TextAlign.Right);
                    }
                }
            });

            column.Item().PaddingTop(13).Row(row =>
            {
                row.RelativeItem().PaddingRight(18).Column(left =>
                {
                    left.Item().Text("AMOUNT IN WORDS").FontSize(7).Bold().FontColor(Teal);
                    left.Item().PaddingTop(3).Text(quotation.AmountInWords ?? "").Bold();
                    if (!string.IsNullOrWhiteSpace(quotation.PublicNotes))
                    {
                        left.Item().PaddingTop(12).Text("NOTES").FontSize(7).Bold().FontColor(Teal);
                        left.Item().PaddingTop(3).Text(quotation.PublicNotes);
                    }
                });
                row.ConstantItem(225).Background(Pale).BorderLeft(3).BorderColor(Teal).Padding(9).Column(totals =>
                {
                    if (!quotation.HideItemPricesOnPdf)
                    {
                        Total(totals, "Subtotal", quotation.Subtotal, quotation.CurrencyCode);
                        Total(totals, "Discount", -quotation.DiscountTotal, quotation.CurrencyCode);
                        Total(totals, "Net amount", quotation.NetAmount, quotation.CurrencyCode);
                        Total(totals, "Tax", quotation.TaxTotal, quotation.CurrencyCode);
                        if (quotation.ShippingCharge != 0) Total(totals, "Shipping / delivery", quotation.ShippingCharge, quotation.CurrencyCode);
                        if (quotation.AdditionalCharges != 0) Total(totals, "Additional charges", quotation.AdditionalCharges, quotation.CurrencyCode);
                        if (quotation.RoundingAdjustment != 0) Total(totals, "Rounding", quotation.RoundingAdjustment, quotation.CurrencyCode);
                    }
                    totals.Item().PaddingTop(5).LineHorizontal(1).LineColor(Teal);
                    totals.Item().PaddingTop(5).Row(total =>
                    {
                        total.RelativeItem().Text("GRAND TOTAL").FontSize(12).Bold().FontColor(Navy);
                        total.ConstantItem(118).AlignRight().Text($"{quotation.CurrencyCode} {quotation.GrandTotal:N2}").FontSize(12).Bold().FontColor(Navy);
                    });
                });
            });

            column.Item().PaddingTop(16).Element(x => CommercialTerms(x, quotation));
            column.Item().PaddingTop(15).Text(company.QuotationFooter).Italic().FontColor("#4C5A67");
            column.Item().PaddingTop(18).Row(row =>
            {
                row.RelativeItem().Element(x => SignatureBox(x, "PREPARED BY", quotation.Salesperson));
                row.ConstantItem(12);
                row.RelativeItem().Element(x => SignatureBox(x, "APPROVED BY", null));
                row.ConstantItem(12);
                row.RelativeItem().Element(x => SignatureBox(x, "CUSTOMER ACCEPTANCE", quotation.CustomerNameSnapshot));
            });
            column.Item().PaddingTop(9).AlignCenter().Text("This document is a quotation and is not a tax invoice.").Bold().FontColor(Navy);
        });
    }

    private static void Footer(IContainer container, Quotation quotation)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor("#BCC7CF");
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"{quotation.DisplayNumber}  •  Valid until {quotation.ValidUntil:dd MMM yyyy}").FontSize(7).FontColor("#63717D");
                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
                });
            });
        });
    }

    private static void InformationCard(IContainer container, string title, IEnumerable<string?> lines)
    {
        container.Border(1).BorderColor("#C8D2D9").Column(column =>
        {
            column.Item().Background(Teal).Padding(5).Text(title).Bold().FontColor(Colors.White);
            column.Item().MinHeight(78).Padding(7).Column(content =>
            {
                foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x))) content.Item().Text(line!);
            });
        });
    }

    private static void CommercialTerms(IContainer container, Quotation quotation)
    {
        container.Border(1).BorderColor("#C8D2D9").Padding(8).Column(column =>
        {
            column.Item().Text("COMMERCIAL TERMS").Bold().FontColor(Teal);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    Term(left, "Validity", $"Until {quotation.ValidUntil:dd MMM yyyy}");
                    Term(left, "Payment terms", quotation.PaymentTerms);
                    Term(left, "Delivery period", quotation.DeliveryPeriod);
                });
                row.ConstantItem(12);
                row.RelativeItem().Column(right =>
                {
                    Term(right, "Delivery terms", quotation.DeliveryTerms);
                    Term(right, "Warranty", quotation.Warranty);
                    Term(right, "Exchange rate", quotation.ExchangeRate == 1 ? null : quotation.ExchangeRate.ToString("N4"));
                });
            });
            if (!string.IsNullOrWhiteSpace(quotation.TermsAndConditions))
                column.Item().PaddingTop(7).Text(quotation.TermsAndConditions);
        });
    }

    private static void SignatureBox(IContainer container, string title, string? name)
    {
        container.Border(1).BorderColor("#C8D2D9").MinHeight(78).Padding(7).Column(column =>
        {
            column.Item().Text(title).FontSize(7).Bold().FontColor(Teal);
            column.Item().PaddingTop(4).Text(name ?? "");
            column.Item().PaddingTop(22).Text("Signature: __________________");
            column.Item().PaddingTop(4).Text("Date: ______________________");
        });
    }

    private static void Meta(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(1).AlignRight().Text(label).FontSize(7).FontColor("#65727E");
        table.Cell().PaddingVertical(1).AlignRight().Text(value).Bold();
    }

    private static void TableHeader(TableCellDescriptor header, string value)
        => header.Cell().Background(Navy).BorderRight(0.5f).BorderColor("#FFFFFF").PaddingVertical(5).PaddingHorizontal(3)
            .AlignCenter().Text(value).FontSize(7).Bold().FontColor(Colors.White);

    private enum TextAlign { Left, Center, Right }

    private static void Body(TableDescriptor table, string value, string background, TextAlign align = TextAlign.Left)
    {
        var cell = table.Cell().Background(background).BorderBottom(0.5f).BorderColor("#CCD5DB").PaddingVertical(5).PaddingHorizontal(3).ShowEntire();
        var aligned = align switch { TextAlign.Center => cell.AlignCenter(), TextAlign.Right => cell.AlignRight(), _ => cell };
        aligned.Text(value);
    }

    private static void Total(ColumnDescriptor column, string label, decimal amount, string currency)
        => column.Item().PaddingVertical(1).Row(row =>
        {
            row.RelativeItem().Text(label);
            row.ConstantItem(118).AlignRight().Text($"{currency} {amount:N2}");
        });

    private static void Term(ColumnDescriptor column, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        column.Item().PaddingBottom(3).Text(text => { text.Span($"{label}: ").SemiBold(); text.Span(value); });
    }

    private static string Description(QuotationItem item, bool showStock)
    {
        var details = new List<string> { item.DescriptionSnapshot };
        if (!string.IsNullOrWhiteSpace(item.Notes)) details.Add(item.Notes);
        if (showStock && item.StockItemId is not null) details.Add($"Available stock at quotation date: {item.AvailableStockSnapshot:0.####}");
        return string.Join("\n", details);
    }

    private static string Contact(CompanySettings company)
        => string.Join("  •  ", new[] { company.Telephone, company.Mobile, company.Email, company.Website }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string? Join(string label, params string?[] values)
    {
        var result = string.Join(" / ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        return result.Length == 0 ? null : $"{label}: {result}";
    }
}
