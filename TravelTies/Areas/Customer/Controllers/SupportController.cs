using Microsoft.AspNetCore.Mvc;

namespace TravelTies.Areas.Customer.Controllers
{
    [Area("Customer")]

public class SupportController : Controller
{
    [HttpGet("/support")]
    public IActionResult Index() => View();
}
}
