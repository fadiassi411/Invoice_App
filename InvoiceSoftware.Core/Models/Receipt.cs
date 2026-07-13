namespace InvoiceSoftware.Core.Models;

public sealed class Receipt : BaseEntity
{
    public string ReferenceNumber { get; set; } = "";
    public DateTime ReceiptDate { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal PaymentAmount { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public string? ReceivedBy { get; set; }
}

public sealed class ReceiptPayment : BaseEntity
{
    public int ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
}

public sealed class PaymentMethod : BaseEntity
{
    public string Name { get; set; } = "";
    public PaymentMethodType MethodType { get; set; }
    public bool IsActive { get; set; } = true;
}
