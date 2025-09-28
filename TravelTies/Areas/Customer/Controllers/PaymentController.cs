using System.Globalization;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using Net.payOS;
using Net.payOS.Types;
using Newtonsoft.Json;
using TravelTies.Areas.Customer.Models;

namespace TravelTies.Areas.Customer.Controllers
{
    [Route("payment")]
    [Area("Customer")]
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly PayOS _payOS;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _config;
        private readonly ITicketRepository _ticketRepo;
        private readonly ITourRepository _tourRepo;
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;

        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly string _checksumKey;
        private readonly string _returnUrl;
        private readonly string _cancelUrl;

        public PaymentController(
            ILogger<PaymentController> logger,
            IConfiguration config,
            ITicketRepository ticketRepo,
            ITourRepository tourRepo, PayOS payOs, UserManager<User> userManager, IEmailSender emailSender)
        {
            _logger = logger;
            _config = config;
            _ticketRepo = ticketRepo;
            _tourRepo = tourRepo;
            _userManager = userManager;
            _emailSender = emailSender;

            _clientId = _config["PayOS:PAYOS_CLIENT_ID"] ?? throw new ArgumentNullException("PayOS:PAYOS_CLIENT_ID missing");
            _apiKey = _config["PayOS:PAYOS_API_KEY"] ?? throw new ArgumentNullException("PayOS:ApiKey missing");
            _checksumKey = _config["PayOS:PAYOS_CHECKSUM_KEY"] ?? throw new ArgumentNullException("PayOS:ChecksumKey missing");

            _returnUrl = string.Empty;
            _cancelUrl = string.Empty;

            _payOS = new PayOS(_clientId, _apiKey, _checksumKey);
        }

        /// <summary>
        /// Start payment: tạo payment link trên PayOS cho các ticket được truyền lên.
        /// Query: ticketIds (comma separated GUID), amount (decimal)
        /// Sau khi tạo link thành công: lưu orderCode vào mỗi Ticket.PaymentOrderCode rồi redirect đến checkoutUrl.
        /// </summary>
        [HttpGet("start")]
        public async Task<IActionResult> Start(string ticketIds, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(ticketIds))
                return BadRequest("ticketIds is required");

            var idStrings = ticketIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            var guidIds = new List<Guid>();
            foreach (var s in idStrings)
            {
                if (Guid.TryParse(s, out var g))
                    guidIds.Add(g);
            }

            if (!guidIds.Any())
                return BadRequest("No valid ticket ids provided.");

            // Lấy tickets (cần asNoTracking: false để có thể Update)
            var ticketsQuery = _ticketRepo.GetAllQueryable(t => guidIds.Contains(t.TicketId), asNoTracking: false)
                .Include(t => t.Tour);
            var tickets = await ticketsQuery.ToListAsync();

            if (!tickets.Any())
                return BadRequest("Tickets not found.");

            // Server-side compute total để an toàn
            decimal serverSubtotal = tickets.Sum(t => t.TicketPrice);
            if (serverSubtotal != amount)
            {
                _logger.LogWarning("Client amount {client} differs from server computed {server}. Using server value.", amount, serverSubtotal);
                amount = serverSubtotal;
            }

            // Tạo orderCode (unique long)
            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Chuẩn bị ItemData
            var items = new List<ItemData>();
            foreach (var t in tickets)
            {
                var tourName = t.Tour?.TourName ?? $"Ticket-{t.TicketId}";
                int qty = Math.Max(1, t.NumberOfSeats);
                // Nếu TicketPrice là tổng cho số ghế, ta chia ra price per seat
                int pricePerUnit = (int)Math.Round(t.TicketPrice / Math.Max(1, t.NumberOfSeats));
                items.Add(new ItemData(tourName, qty, pricePerUnit));
            }

            int amountInt = (int)Math.Round(amount);
            string description = $"Order {orderCode}";

            // Nếu appsettings không cung cấp return/cancel url, fallback tạo URL đối với action Callback/CancelReturn nếu muốn
            string returnUrl = _returnUrl;
            string cancelUrl = _cancelUrl;
            if (string.IsNullOrEmpty(returnUrl))
            {
                returnUrl = Url.Action("Return", "Payment", null, Request.Scheme) ?? "";
            }

            if (string.IsNullOrEmpty(cancelUrl))
            {
                cancelUrl = Url.Action("Cancel", "Payment", null, Request.Scheme) ?? "";
            }

            var paymentData = new PaymentData(orderCode, amountInt, description, items, cancelUrl: cancelUrl, returnUrl: returnUrl);

            try
            {
                CreatePaymentResult createResult = await _payOS.createPaymentLink(paymentData);

                _logger.LogInformation("PayOS createPaymentLink result: {r}", JsonConvert.SerializeObject(createResult));

                // Lưu orderCode vào Ticket.PaymentOrderCode cho tất cả ticket liên quan
                foreach (var tk in tickets)
                {
                    tk.PaymentOrderCode = orderCode;
                    await _ticketRepo.UpdateAsync(tk);
                }

                // Redirect user tới trang checkout của PayOS
                if (!string.IsNullOrWhiteSpace(createResult?.checkoutUrl))
                {
                    return Redirect(createResult.checkoutUrl);
                }

                return Json(new { success = false, result = createResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayOS payment link for order {order}", orderCode);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin link thanh toán (có thể dùng để kiểm tra trạng thái)
        /// </summary>
        [HttpGet("info/{orderCode:long}")]
        public async Task<IActionResult> GetPaymentInfo(long orderCode)
        {
            try
            {
                PaymentLinkInformation info = await _payOS.getPaymentLinkInformation(orderCode);
                return Json(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPaymentInfo failed for {order}", orderCode);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // A simple cancel page for user when they click cancel on payment page
        [HttpGet("cancel")]
        [AllowAnonymous]
        public IActionResult Cancel()
        {
            var vm = new PaymentResultViewModel
            {
                OrderCode = 0,
                Status = "CANCELLED",
                Message = "Bạn đã huỷ thanh toán."
            };
            return View("Failed", vm); // reuse failed view or make a dedicated Cancel view
        }

        [HttpGet("return")]
        public async Task<IActionResult> Return(long? orderCode, string? status)
        {
            if (orderCode == null) return RedirectToAction("Failed", new { orderCode = 0L });

            try
            {
                var info = await _payOS.getPaymentLinkInformation(orderCode.Value);

                // Kiểm tra đã thanh toán hay chưa
                bool paid = (info.status ?? "").Equals("PAID", StringComparison.OrdinalIgnoreCase)
                            || (info.amountPaid >= info.amount && info.amount > 0);

                if (!paid)
                {
                    return RedirectToAction("Failed", new { orderCode = orderCode.Value });
                }

                // Lấy tất cả ticket liên quan tới orderCode và cập nhật
                var tickets = await _ticketRepo.GetAllQueryable(t => t.PaymentOrderCode == orderCode.Value, asNoTracking: false).ToListAsync();

                // Cập nhật từng ticket: IsPayed = true, cập nhật PaymentOrderCode
                foreach (var tk in tickets)
                {
                    tk.IsPayed = true;
                    tk.PaymentOrderCode = orderCode.Value; // chắc chắn lưu
                    await _ticketRepo.UpdateAsync(tk);
                }

                // Gửi email xác nhận - gom theo user để gửi 1 email cho 1 user (nếu user mua nhiều ticket)
                var ticketsByUser = tickets
                    .Where(t => t.UserId != null)
                    .GroupBy(t => t.UserId);

                foreach (var group in ticketsByUser)
                {
                    try
                    {
                        User? user = null;
                        if (group.Key != null)
                        {
                            user = await _userManager.FindByIdAsync(group.Key.ToString());
                        }

                        string? email = user?.Email;
                        if (string.IsNullOrEmpty(email))
                        {
                            _logger.LogWarning("Cannot send payment email: user {UserId} has no email", group.Key);
                            continue;
                        }

                        // Tạo nội dung email
                        var subject = $"Xác nhận thanh toán - Đơn hàng #{orderCode.Value}";
                        var body = BuildPaymentConfirmationHtml(group.ToList(), orderCode.Value, info);

                        await _emailSender.SendEmailAsync(email, subject, body);
                    }
                    catch (Exception exEmail)
                    {
                        _logger.LogError(exEmail, "Failed to send payment confirmation email for order {OrderCode}", orderCode.Value);
                    }
                }

                // Chuyển tới trang success (hoặc hiển thị thông tin theo viewmodel)
                return RedirectToAction("Success", new { orderCode = orderCode.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment callback error for order {Order}", orderCode);
                return RedirectToAction("Failed", new { orderCode = orderCode.Value });
            }
        }

        private string FormatCurrency(long amount)
        {
            return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0}đ", amount);
        }

        // Tạo HTML email
        private string BuildPaymentConfirmationHtml(List<Ticket> tickets, long orderCode, PaymentLinkInformation info)
        {
            var tour = tickets.FirstOrDefault()?.Tour;
            var sb = new System.Text.StringBuilder();

            sb.Append($@"<h2>Thanh toán thành công - Mã đơn hàng #{orderCode}</h2>");
            sb.Append("<p>Cảm ơn bạn đã đặt tour tại Travel Ties. Dưới đây là thông tin đơn hàng:</p>");
            sb.Append("<ul>");
            foreach (var t in tickets)
            {
                var tourName = t.Tour?.TourName ?? "Tên tour";
                var tourDate = t.TourDate.ToString("dd/MM/yyyy");
                var seats = t.NumberOfSeats;
                var price = FormatCurrency((long)t.TicketPrice);
                sb.Append($"<li><strong>{tourName}</strong> - Ngày: {tourDate} - Số ghế: {seats} - Giá: {price}</li>");
            }

            sb.Append("</ul>");

            sb.Append($"<p><strong>Tổng: {FormatCurrency(info.amountPaid > 0 ? info.amountPaid : info.amount)}</strong></p>");
            sb.Append($"<p>Mã đơn hàng: <strong>{orderCode}</strong></p>");

            // Thông tin hủy: cancellation datetime của ticket (giả sử tất cả ticket cùng giá trị cancellationdatetime)
            var cancelDt = tickets.First().CancellationDateTime;
            sb.Append($"<p>Thời hạn hủy miễn phí: trước {cancelDt.ToString("dd/MM/yyyy HH:mm")}</p>");

            sb.Append(
                "<p>Chúng tôi đã gửi thông tin chi tiết vé vào tài khoản của bạn. Nếu cần hỗ trợ, vui lòng phản hồi lại email này hoặc liên hệ hotline.</p>");

            sb.Append("<p>Trân trọng,<br/>Travel Ties</p>");

            return sb.ToString();
        }

// Direct endpoint if you want to redirect manually
        [HttpGet("success/{orderCode:long}")]
        public async Task<IActionResult> Success(long orderCode)
        {
            var info = await _payOS.getPaymentLinkInformation(orderCode);

            var count = await _ticketRepo.GetAllQueryable()
                .CountAsync(t => t.PaymentOrderCode == orderCode);
            
            var vm = new PaymentResultViewModel
            {
                OrderCode = orderCode,
                Status = info.status ?? "UNKNOWN",
                Message = (info.status ?? "").Equals("PAID", StringComparison.OrdinalIgnoreCase)
                    ? "Thanh toán thành công."
                    : "Thanh toán chưa hoàn tất.",
                AmountPaid = info.amountPaid,
                Amount = info.amount,
                TicketsCount = count
            };

            return View("Return", vm);
        }


        [HttpGet("failed/{orderCode:long}")]
        public IActionResult Failed(long orderCode, string? reason)
        {
            var vm = new PaymentResultViewModel
            {
                OrderCode = orderCode,
                Status = "FAILED",
                Message = reason ?? "Thanh toán không thành công hoặc đã bị huỷ."
            };
            return View("Failed", vm);
        }
    }
}