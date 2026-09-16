using InvoiceSoftware.Core.Models;
using InvoiceSoftware.Reporting;

namespace InvoiceSoftware.Tests;

public sealed class ArabicPdfTests
{
    [Fact]
    public void Invoice_and_receipt_pdfs_accept_arabic_customer_and_description()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"arabic-pdf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var company = new CompanySettings { CompanyName = "Test Company", Currency = "USD", CurrencySymbol = "$" };
            var invoice = new Invoice
            {
                ReferenceNumber = "INV-TEST-ARABIC",
                CustomerNameSnapshot = "عميل تجريبي",
                Notes = "ملاحظات باللغة العربية",
                GrandTotal = 50,
                RemainingBalance = 50,
                Subtotal = 50,
                Items = [new InvoiceItem { SortOrder = 1, Description = "مستشعر درجة الحرارة", Quantity = 2, UnitPrice = 25, LineTotal = 50 }]
            };
            var receipt = new Receipt
            {
                ReferenceNumber = "REC-TEST-ARABIC",
                Invoice = invoice,
                Notes = "دفعة تجريبية",
                PaymentAmount = 50
            };
            var service = new InvoicePdfService();
            var invoicePath = Path.Combine(folder, "invoice.pdf");
            var receiptPath = Path.Combine(folder, "receipt.pdf");
            service.ExportInvoice(invoice, company, invoicePath);
            service.ExportReceipt(receipt, company, receiptPath);

            Assert.True(new FileInfo(invoicePath).Length > 1_000);
            Assert.True(new FileInfo(receiptPath).Length > 1_000);
            var qaFolder = Environment.GetEnvironmentVariable("ARABIC_PDF_QA_FOLDER");
            if (!string.IsNullOrWhiteSpace(qaFolder))
            {
                Directory.CreateDirectory(qaFolder);
                File.Copy(invoicePath, Path.Combine(qaFolder, "arabic-invoice.pdf"), true);
                File.Copy(receiptPath, Path.Combine(qaFolder, "arabic-receipt.pdf"), true);
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
