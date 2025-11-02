using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DataAccess;               // <-- đúng namespace DbContext
// using Models.Models;        // nếu cần đến kiểu Tour trực tiếp

namespace TravelTies.AI
{
    /// <summary>
    /// Đọc dữ liệu Tours khi app khởi động và mỗi giờ.
    /// Lưu chuỗi tổng hợp vào LatestTourData để GeminiRestAiService đính kèm vào systemInstruction.
    /// </summary>
    public sealed class TourDataUpdater : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TourDataUpdater> _logger;

        // Dữ liệu tour mới nhất (để dùng kèm system prompts)
        public static string LatestTourData { get; private set; } = "Chưa có dữ liệu tour.";

        // Giới hạn để tránh payload quá lớn gửi lên LLM
        private const int MaxChars = 20_000;

        public TourDataUpdater(IServiceProvider services, ILogger<TourDataUpdater> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Đọc ngay khi app vừa khởi động
            await UpdateTourDataAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (TaskCanceledException) { break; }

                await UpdateTourDataAsync();
            }
        }

        private async Task UpdateTourDataAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Chỉ đọc nhẹ & không track
                var tours = await db.Tours
                    .AsNoTracking()
                    .Select(t => new
                    {
                        t.TourName,
                        t.Price,
                        t.Discount,
                        t.TourStartDate,
                        t.TourEndDate,
                        t.Destination,
                        t.HotelStars,
                        t.NumberOfPassenger
                    })
                    .OrderByDescending(t => t.Discount) // ưu tiên tour có ưu đãi
                    .ThenBy(t => t.TourStartDate)
                    .ToListAsync();

                if (tours.Count == 0)
                {
                    LatestTourData = "Hiện chưa có tour nào trong cơ sở dữ liệu.";
                    _logger.LogWarning("[TourDataUpdater] Không có tour trong bảng Tours.");
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("=== DỮ LIỆU TOUR HIỆN CÓ (rút gọn) ===");
                foreach (var t in tours)
                {
                    sb.Append("• ");
                    sb.Append(t.TourName);
                    sb.Append(" | Giá: ").Append(t.Price.ToString("N0"));
                    sb.Append(" | Giảm: ").Append(t.Discount).Append("%");
                    sb.Append(" | Điểm đến: ").Append(t.Destination);
                    sb.Append(" | Khởi hành: ").Append(t.TourStartDate.ToString("dd/MM/yyyy"));
                    if (t.TourEndDate != default) sb.Append(" - ").Append(t.TourEndDate.ToString("dd/MM/yyyy"));
                    sb.Append(" | Sao KS: ").Append(t.HotelStars);
                    sb.Append(" | Số chỗ: ").Append(t.NumberOfPassenger);
                    sb.AppendLine();
                }

                var full = sb.ToString();
                // Chặn quá dài để tránh 413/ResourceExhausted
                LatestTourData = full.Length > MaxChars ? full[..MaxChars] + "\n...(đã cắt bớt)" : full;

                _logger.LogInformation("[TourDataUpdater] Đã đọc {Count} tour. tourdata length={Len}.",
                    tours.Count, LatestTourData.Length);
                _logger.LogDebug("[TourDataUpdater] Preview tourdata:\n{Preview}",
                    LatestTourData.Length > 800 ? LatestTourData[..800] + "..." : LatestTourData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TourDataUpdater] Lỗi khi cập nhật dữ liệu tour từ SQL.");
            }
        }
    }
}
