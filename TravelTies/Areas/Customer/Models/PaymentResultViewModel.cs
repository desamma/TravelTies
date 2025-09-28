namespace TravelTies.Areas.Customer.Models;

public class PaymentResultViewModel
{
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public int AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Thông tin rút gọn để hiển thị
    public int TicketsCount { get; set; }
    public IEnumerable<string> TransactionsSummary { get; set; } = new List<string>();
}
