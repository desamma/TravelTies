using DataAccess;
using DataAccess.Repositories;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using Net.payOS;
using TravelTies.Hubs;   // <— cho Chat
using TravelTies.AI;
using Utilities.Utils;

DotNetEnv.Env.Load();

// Import payOS
IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
PayOS payOS = new PayOS(configuration["PayOS:PAYOS_CLIENT_ID"] ?? throw new Exception("Cannot find environment"),
    configuration["PayOS:PAYOS_API_KEY"] ?? throw new Exception("Cannot find environment"),
    configuration["PayOS:PAYOS_CHECKSUM_KEY"] ?? throw new Exception("Cannot find environment"));

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add payOS service
builder.Services.AddSingleton(payOS);

//SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Cloudinary
builder.Services.AddSingleton<CloudinaryUploader>();

// Add database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
}, ServiceLifetime.Transient);

// Identity and roles
builder.Services
    .AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ àáảãạâầấẩẫậăằắẳẵặèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ()";
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

//Configure .env config binding
builder.Configuration["EmailSettings:FromEmail"] = Environment.GetEnvironmentVariable("EMAILSETTINGS__FROMEMAIL");
builder.Configuration["EmailSettings:FromPassword"] = Environment.GetEnvironmentVariable("EMAILSETTINGS__FROMPASSWORD");
Console.WriteLine("EMAIL: " + builder.Configuration["EmailSettings:FromEmail"]);
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Repositories
builder.Services.AddScoped(typeof(IGenericInterface<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IRevenueRepository, RevenueRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITourRepository, TourRepository>();
builder.Services.AddScoped<ITourRepository, TourRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Configure default routes (This should be after configured the Identity)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Enable razor page
builder.Services.AddRazorPages();
builder.Services.AddMvc();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Set session timeout
});

// External login (Google)
builder.Services.AddAuthentication()
    .AddGoogle(GoogleDefaults.AuthenticationScheme, option =>
    {
        option.ClientId = Environment.GetEnvironmentVariable("GOOGLESETTINGS__CLIENTID");
        option.ClientSecret = Environment.GetEnvironmentVariable("GOOGLESETTINGS__CLIENTSECRET");
    });


builder.Services.AddHttpClient<IAiService, GeminiRestAiService>();

var app = builder.Build();

// Seed data for roles and admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Initialize the database and seed data
        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        // Log the error (uncomment ex variable name and write a log)
        Console.WriteLine($"An error occurred while seeding role in the database: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//todo:app.MapHub<ChatHub>("/ChatHub");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<ChatHub>("/hubs/chat");   //endpoint Six No R :)

//todo:change mapping route
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
app.MapGet("/ai/test", async (IConfiguration cfg, ILoggerFactory lf) =>
{
    var logger = lf.CreateLogger("AiTest");
    var key = cfg["GoogleAI:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    if (string.IsNullOrWhiteSpace(key)) return Results.Text("No API key");

    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}";
    var payload = new
    {
        system_instruction = new { parts = new[] { new { text = "You are a helpful assistant." } } },
        contents = new[] { new { role = "user", parts = new[] { new { text = "Say hello in Vietnamese." } } } }
    };

    using var http = new HttpClient();
    var res = await http.PostAsJsonAsync(url, payload);
    var body = await res.Content.ReadAsStringAsync();

    logger.LogInformation("AI test status={Status} body={Body}", (int)res.StatusCode, body);
    return Results.Text($"status={(int)res.StatusCode}\n\n{body}", "text/plain");
});

app.Run();
