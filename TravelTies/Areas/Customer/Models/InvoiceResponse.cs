using Newtonsoft.Json;

namespace TravelTies.Areas.Customer.Models;

// Models for invoice response deserialization
public class InvoiceResponse
{
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonProperty("data")]
    public InvoiceData? Data { get; set; }

    [JsonProperty("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class InvoiceData
{
    [JsonProperty("invoices")]
    public List<Invoice>? Invoices { get; set; }
}

public class Invoice
{
    [JsonProperty("invoiceId")]
    public string InvoiceId { get; set; } = string.Empty;

    [JsonProperty("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonProperty("issuedTimestamp")]
    public long? IssuedTimestamp { get; set; }

    [JsonProperty("issuedDatetime")]
    public DateTime? IssuedDatetime { get; set; }

    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }

    [JsonProperty("reservationCode")]
    public string? ReservationCode { get; set; }

    [JsonProperty("codeOfTax")]
    public string? CodeOfTax { get; set; }
}