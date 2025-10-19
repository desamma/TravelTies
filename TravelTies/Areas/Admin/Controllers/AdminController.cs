using System.Security.Claims;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Models;
using TravelTies.Areas.Admin.ViewModels;
using Utilities.Constants;
using Utilities.Utils;

namespace TravelTies.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly ITourRepository _tourRepo;
        private readonly ITicketRepository _ticketRepo;
        private readonly IRevenueRepository _revenueRepo;
        private readonly UserManager<User> _userManager;
        private readonly CloudinaryUploader _cloudinary;

        public AdminController(
            IUserRepository userRepo,
            ITourRepository tourRepo,
            ITicketRepository ticketRepo,
            IRevenueRepository revenueRepo,
            UserManager<User> userManager,
            CloudinaryUploader cloudinary)
        {
            _userRepo = userRepo;
            _tourRepo = tourRepo;
            _ticketRepo = ticketRepo;
            _revenueRepo = revenueRepo;
            _userManager = userManager;
            _cloudinary = cloudinary;
        }

        #region Dashboard

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.Now;
            var lastMonth = now.AddMonths(-1);

            // Total counts
            var totalUsers = await _userRepo.GetAllQueryable(u => !u.IsCompany).CountAsync();
            var totalCompanies = await _userRepo.GetAllQueryable(u => u.IsCompany).CountAsync();

            // User growth
            var lastMonthUsers = await _userRepo.GetAllQueryable(u => 
                !u.IsCompany && u.CreatedDate >= lastMonth && u.CreatedDate < now).CountAsync();
            var previousMonthUsers = await _userRepo.GetAllQueryable(u => 
                !u.IsCompany && u.CreatedDate < lastMonth).CountAsync();
            
            decimal userGrowth = previousMonthUsers > 0 
                ? Math.Round((decimal)lastMonthUsers / previousMonthUsers * 100, 1) 
                : 0;

            // Monthly revenue for current year
            var currentYear = now.Year;
            var revenueData = new List<decimal>();
            var monthLabels = new List<string>();

            for (int month = 1; month <= 12; month++)
            {
                var revenue = await _revenueRepo.GetAllQueryable(r => 
                    r.Year == currentYear && r.Month == month)
                    .SumAsync(r => (decimal?)r.Amount) ?? 0m;
                
                revenueData.Add(revenue);
                monthLabels.Add($"T{month}");
            }

            // Recent activities (last 20)
            var recentUsers = await _userRepo.GetAllQueryable()
                .OrderByDescending(u => u.CreatedDate)
                .Take(10)
                .Select(u => new ActivityItem
                {
                    Type = "User Registration",
                    Description = $"User '{u.UserName}' registered",
                    Timestamp = u.CreatedDate ?? DateTime.Now,
                    Icon = "fa-user-plus",
                    Color = "text-blue-600"
                })
                .ToListAsync();

            var recentTickets = await _ticketRepo.GetAllQueryable()
                .Include(t => t.User)
                .Include(t => t.Tour)
                .OrderByDescending(t => t.CancellationDateTime)
                .Take(10)
                .Select(t => new ActivityItem
                {
                    Type = "Tour Purchase",
                    Description = $"{t.User.UserName} purchased {t.NumberOfSeats} ticket(s) for {t.Tour.TourName}",
                    Timestamp = t.CancellationDateTime,
                    Icon = "fa-ticket",
                    Color = "text-green-600"
                })
                .ToListAsync();

            var allActivities = recentUsers.Concat(recentTickets)
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
                .ToList();

            var viewModel = new DashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalCompanies = totalCompanies,
                UserGrowthPercentage = userGrowth,
                MonthlyRevenue = revenueData,
                MonthLabels = monthLabels,
                RecentActivities = allActivities
            };

            return View(viewModel);
        }

        #endregion

        #region User Management

        [HttpGet]
        public async Task<IActionResult> Users(int page = 1, string? search = null, 
            string? gender = null, DateTime? from = null, DateTime? to = null)
        {
            const int pageSize = 30;
            
            var query = _userRepo.GetAllQueryable(u => !u.IsCompany);

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u => 
                    u.UserName.ToLower().Contains(search) || 
                    u.Email.ToLower().Contains(search));
            }

            // Gender filter
            if (!string.IsNullOrWhiteSpace(gender) && gender != "All")
            {
                bool genderBool = gender == "Male";
                query = query.Where(u => u.Gender == genderBool);
            }

            // Date range filter
            if (from.HasValue)
                query = query.Where(u => u.CreatedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(u => u.CreatedDate <= to.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var users = await query
                .OrderByDescending(u => u.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new UserManagementViewModel
            {
                Users = users,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                SearchTerm = search,
                SelectedGender = gender,
                FromDate = from,
                ToDate = to
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(string userId)
        {
            if (!Guid.TryParse(userId, out var id))
                return BadRequest();

            var user = await _userRepo.GetAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.IsBanned = !user.IsBanned;
            
            // Update role
            if (user.IsBanned)
            {
                await _userManager.RemoveFromRoleAsync(user, RoleConstants.User);
                await _userManager.AddToRoleAsync(user, RoleConstants.Banned);
            }
            else
            {
                await _userManager.RemoveFromRoleAsync(user, RoleConstants.Banned);
                await _userManager.AddToRoleAsync(user, RoleConstants.User);
            }

            await _userRepo.UpdateAsync(user);
            
            TempData["Success"] = user.IsBanned ? "User banned successfully" : "User unbanned successfully";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (!Guid.TryParse(userId, out var id))
                return BadRequest();

            var user = await _userRepo.GetAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            await _userRepo.DeleteAsync(id);
            
            TempData["Success"] = "User deleted successfully";
            return RedirectToAction(nameof(Users));
        }

        #endregion

        #region Company Management

        [HttpGet]
        public async Task<IActionResult> Companies(int page = 1, string? search = null, 
            DateTime? from = null, DateTime? to = null)
        {
            const int pageSize = 30;
            
            var query = _userRepo.GetAllQueryable(u => u.IsCompany);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u => 
                    u.UserName.ToLower().Contains(search) || 
                    u.Email.ToLower().Contains(search));
            }

            if (from.HasValue)
                query = query.Where(u => u.CreatedDate >= from.Value);
            if (to.HasValue)
                query = query.Where(u => u.CreatedDate <= to.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var companies = await query
                .OrderByDescending(u => u.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new CompanyManagementViewModel
            {
                Companies = companies,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                SearchTerm = search,
                FromDate = from,
                ToDate = to
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult CreateCompany()
        {
            return View(new CreateCompanyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCompany(CreateCompanyViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Email already exists");
                return View(model);
            }

            string? avatarUrl = null;
            if (model.AvatarFile != null)
            {
                avatarUrl = await _cloudinary.UploadMediaAsync(model.AvatarFile);
                if (avatarUrl == "Unsupported file type")
                {
                    ModelState.AddModelError("AvatarFile", "Invalid file type");
                    return View(model);
                }
            }

            var company = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                EmailConfirmed = true,
                UserDOB = model.CompanyCreatedDate,
                UserAvatar = avatarUrl ?? GeneralConstants.DefaultAvatar,
                Description = model.Description,
                ContactInfo = model.ContactInfo,
                IsCompany = true,
                IsBanned = false,
                Gender = false,
                CreatedDate = DateTime.Now
            };

            var result = await _userManager.CreateAsync(company, model.Password);
            
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(company, RoleConstants.Company);
                TempData["Success"] = "Company created successfully";
                return RedirectToAction(nameof(Companies));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanCompany(string companyId)
        {
            if (!Guid.TryParse(companyId, out var id))
                return BadRequest();

            var company = await _userRepo.GetAsync(u => u.Id == id && u.IsCompany);
            if (company == null)
                return NotFound();

            company.IsBanned = !company.IsBanned;
            
            if (company.IsBanned)
            {
                await _userManager.RemoveFromRoleAsync(company, RoleConstants.Company);
                await _userManager.AddToRoleAsync(company, RoleConstants.Banned);
            }
            else
            {
                await _userManager.RemoveFromRoleAsync(company, RoleConstants.Banned);
                await _userManager.AddToRoleAsync(company, RoleConstants.Company);
            }

            await _userRepo.UpdateAsync(company);
            
            TempData["Success"] = company.IsBanned ? "Company banned successfully" : "Company unbanned successfully";
            return RedirectToAction(nameof(Companies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCompany(string companyId)
        {
            if (!Guid.TryParse(companyId, out var id))
                return BadRequest();

            var company = await _userRepo.GetAsync(u => u.Id == id && u.IsCompany);
            if (company == null)
                return NotFound();

            await _userRepo.DeleteAsync(id);
            
            TempData["Success"] = "Company deleted successfully";
            return RedirectToAction(nameof(Companies));
        }

        #endregion

        #region Tour Management

        [HttpGet]
        public async Task<IActionResult> Tours(int page = 1, string? search = null, 
            string? category = null, DateTime? from = null, DateTime? to = null)
        {
            const int pageSize = 30;
            
            IQueryable<Tour> query = _tourRepo.GetAllQueryable()
                .Include(t => t.Company);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(t => t.TourName.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(t => t.Category == category);
            }

            if (from.HasValue)
            {
                var fromDate = DateOnly.FromDateTime(from.Value);
                query = query.Where(t => t.TourStartDate >= fromDate);
            }
            if (to.HasValue)
            {
                var toDate = DateOnly.FromDateTime(to.Value);
                query = query.Where(t => t.TourStartDate <= toDate);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var tours = await query
                .OrderByDescending(t => t.TourStartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _tourRepo.GetAllQueryable()
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToListAsync();

            var viewModel = new TourManagementViewModel
            {
                Tours = tours,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                SearchTerm = search,
                SelectedCategory = category,
                FromDate = from,
                ToDate = to,
                Categories = categories
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTour(string tourId)
        {
            if (!Guid.TryParse(tourId, out var id))
                return BadRequest();

            var tour = await _tourRepo.GetAsync(t => t.TourId == id);
            if (tour == null)
                return NotFound();

            await _tourRepo.DeleteAsync(id);
            
            TempData["Success"] = "Tour deleted successfully";
            return RedirectToAction(nameof(Tours));
        }

        #endregion
    }
}