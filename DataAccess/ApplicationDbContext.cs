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