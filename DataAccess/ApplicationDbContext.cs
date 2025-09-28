using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Models.Models;

namespace DataAccess;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public virtual DbSet<Chat> Chats { get; set; }
    public virtual DbSet<Rating> Ratings { get; set; }
    public virtual DbSet<Report> Reports { get; set; }
    public virtual DbSet<Revenue> Revenues { get; set; }
    public virtual DbSet<Ticket> Tickets { get; set; }
    public virtual DbSet<Tour> Tours { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable sensitive data logging
        optionsBuilder.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var companyId = Guid.Parse("31d326c6-4505-4f51-0d5e-08ddfdf51a6c");

        modelBuilder.Entity<Tour>().HasData(
            new Tour
            {
                TourId = Guid.NewGuid(),
                TourName = "Khám phá Hà Nội",
                Description = "Tour tham quan các địa điểm nổi tiếng ở Hà Nội",
                About = "Tour 3 ngày 2 đêm khám phá Thủ đô",
                NumberOfPassenger = 20,
                TourStartDate = new DateOnly(2025, 10, 1),
                TourEndDate = new DateOnly(2025, 10, 3),
                TourScheduleDescription = "Ngày 1: Hồ Gươm, Ngày 2: Lăng Bác, Ngày 3: Văn Miếu",
                Sale = 0,
                Destination = "Hà Nội",
                HotelStars = 4,
                Price = 2500000,
                Picture = "images/tours/hanoi.jpg",
                Discount = 10m,
                SupportTourMatching = true,
                Commission = 5,
                Views = 0,
                ConversionRate = 0,
                CompanyId = companyId
            },
            new Tour
            {
                TourId = Guid.NewGuid(),
                TourName = "Tour Hạ Long",
                Description = "Khám phá vịnh Hạ Long tuyệt đẹp",
                About = "Tour 2 ngày 1 đêm, du thuyền Hạ Long",
                NumberOfPassenger = 15,
                TourStartDate = new DateOnly(2025, 10, 5),
                TourEndDate = new DateOnly(2025, 10, 6),
                TourScheduleDescription = "Ngày 1: Du thuyền, Ngày 2: Tham quan hang động",
                Sale = 0,
                Destination = "Hạ Long",
                HotelStars = 3,
                Price = 1800000,
                Picture = "images/tours/halong.jpg",
                Discount = 5m,
                SupportTourMatching = true,
                Commission = 4,
                Views = 0,
                ConversionRate = 0,
                CompanyId = companyId
            },
            new Tour
            {
                TourId = Guid.NewGuid(),
                TourName = "Tour Hội An - Làng gốm Thanh Hà",
                Description = "Khám phá Hội An và trải nghiệm làm gốm",
                About = "Tour 1 ngày, tham quan phố cổ và làng gốm",
                NumberOfPassenger = 12,
                TourStartDate = new DateOnly(2025, 10, 10),
                TourEndDate = new DateOnly(2025, 10, 10),
                TourScheduleDescription = "Sáng: phố cổ Hội An, Chiều: làng gốm Thanh Hà",
                Sale = 0,
                Destination = "Hội An",
                HotelStars = 3,
                Price = 900000,
                Picture = "images/tours/hoian.jpg",
                Discount = 0m,
                SupportTourMatching = true,
                Commission = 3,
                Views = 0,
                ConversionRate = 0,
                CompanyId = companyId
            },
            new Tour
            {
                TourId = Guid.NewGuid(),
                TourName = "Tour Sapa - Trekking bản làng",
                Description = "Khám phá núi rừng và văn hóa dân tộc Sapa",
                About = "Tour 2 ngày 1 đêm, trekking và tham quan bản làng",
                NumberOfPassenger = 10,
                TourStartDate = new DateOnly(2025, 10, 15),
                TourEndDate = new DateOnly(2025, 10, 16),
                TourScheduleDescription = "Ngày 1: trekking bản Cát Cát, Ngày 2: thác Bạc",
                Sale = 0,
                Destination = "Sapa",
                HotelStars = 4,
                Price = 1500000,
                Picture = "images/tours/sapa.jpg",
                Discount = 15m,
                SupportTourMatching = true,
                Commission = 5,
                Views = 0,
                ConversionRate = 0,
                CompanyId = companyId
            },
            new Tour
            {
                TourId = Guid.NewGuid(),
                TourName = "Tour Đà Lạt - Thác Elephant & Canyoning",
                Description = "Trải nghiệm mạo hiểm và khám phá Đà Lạt",
                About = "Tour 1 ngày, tham quan thác và canyoning",
                NumberOfPassenger = 8,
                TourStartDate = new DateOnly(2025, 10, 20),
                TourEndDate = new DateOnly(2025, 10, 20),
                TourScheduleDescription = "Sáng: canyoning, Chiều: thác Elephant",
                Sale = 0,
                Destination = "Đà Lạt",
                HotelStars = 3,
                Price = 800000,
                Picture = "images/tours/dalat.jpg",
                Discount = 5m,
                SupportTourMatching = true,
                Commission = 4,
                Views = 0,
                ConversionRate = 0,
                CompanyId = companyId
            }
        );
        
        //Unique Constrains
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.UserName).IsUnique();
        modelBuilder.Entity<Tour>().HasIndex(u => u.TourName).IsUnique();
        
        //Primary keys
        modelBuilder.Entity<Chat>().HasKey(c => c.ChatId);
        modelBuilder.Entity<Rating>().HasKey(c => c.RatingId);
        modelBuilder.Entity<Report>().HasKey(r => r.ReportId);
        modelBuilder.Entity<Revenue>().HasKey(r => r.RevenueId);
        modelBuilder.Entity<Ticket>().HasKey(t => t.TicketId);
        modelBuilder.Entity<Tour>().HasKey(t => t.TourId);
        modelBuilder.Entity<User>().HasKey(t => t.Id);
        
        //Relationship
        // User <-> Partner (self-referencing many-many)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Partners)
            .WithMany(u => u.PartnerOf)
            .UsingEntity<Dictionary<string, object>>(
                "UserPartners",
                j => j.HasOne<User>().WithMany().HasForeignKey("PartnerId"),
                j => j.HasOne<User>().WithMany().HasForeignKey("UserId")
            );
        // User (Company) <-> Tour (1-many)
        modelBuilder.Entity<Tour>()
            .HasOne(t => t.Company)
            .WithMany(u => u.Tours)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // User (Company) <-> Revenue (1-many)
        modelBuilder.Entity<Revenue>()
            .HasOne(r => r.Company)
            .WithMany(u => u.Revenues)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // User <-> Rating (1-many)
        modelBuilder.Entity<Rating>()
            .HasOne(r => r.User)
            .WithMany(u => u.Ratings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tour <-> Rating (1-many)
        modelBuilder.Entity<Rating>()
            .HasOne(r => r.Tour)
            .WithMany(t => t.Ratings)
            .HasForeignKey(r => r.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        // User <-> Ticket (1-many)
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tickets)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tour <-> Ticket (1-many)
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Tour)
            .WithMany(tr => tr.Tickets)
            .HasForeignKey(t => t.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        // User <-> Report (1-many)
        modelBuilder.Entity<Report>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reports)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tour <-> Report (1-many)
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Tour)
            .WithMany(t => t.Reports)
            .HasForeignKey(r => r.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chat (Sender <-> Receiver both are Users)
        modelBuilder.Entity<Chat>()
            .HasOne(c => c.Sender)
            .WithMany()
            .HasForeignKey(c => c.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Chat>()
            .HasOne(c => c.Receiver)
            .WithMany()
            .HasForeignKey(c => c.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}