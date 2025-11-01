namespace TravelTies.AI;

public interface IAiService
{
    Task<string> AskAsync(string userMessage, Guid userId);
}
