namespace InvoiceSoftware.Core.Models;

public enum InvoiceStatus { Draft, Finalized, Cancelled }
public enum PaymentStatus { Unpaid, PartiallyPaid, Paid, Overpaid }
public enum ProductType { Product, Service }
public enum PaymentMethodType { Cash, CreditCard, BankTransfer, Cheque, OnlinePayment, Other }
public enum SequenceKind { Invoice, Receipt }
