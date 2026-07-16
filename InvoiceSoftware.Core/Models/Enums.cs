namespace InvoiceSoftware.Core.Models;

public enum InvoiceStatus { Draft, Finalized, Cancelled }
public enum PaymentStatus { Unpaid, PartiallyPaid, Paid, Overpaid }
public enum ProductType { Product, Service }
public enum PaymentMethodType { Cash, CreditCard, BankTransfer, Cheque, OnlinePayment, Other }
public enum SequenceKind { Invoice, Receipt, Quotation }
public enum QuotationStatus { Draft, Sent, UnderReview, Accepted, Rejected, Expired, ConvertedToInvoice, Cancelled }
public enum TaxMode { Exclusive, Inclusive, Exempt }
public enum DiscountType { Percentage, FixedAmount }
public enum QuotationItemType { StockItem, ManualItem, Service, SectionHeader }
public enum QuotationValidityFilter { All, Valid, Expired }
public enum StockMovementType
{
    OpeningStock,
    ManualStockAddition,
    ManualStockDeduction,
    Purchase,
    Sale,
    InvoiceModification,
    InvoiceCancellation,
    CustomerReturn,
    StockCorrection
}
