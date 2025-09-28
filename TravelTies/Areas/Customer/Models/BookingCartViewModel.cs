namespace TravelTies.Areas.Customer.Models;

public class BookingCartViewModel
{
    public List<TicketViewModel> Tickets { get; set; } = new();
    public decimal Subtotal { get; set; }
    public int DiscountPercent { get; set; } = 0;
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotal { get; set; }
    public string? CouponCode { get; set; }
}