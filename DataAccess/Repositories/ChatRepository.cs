using DataAccess.Repositories.IRepositories;
using Models.Models;

namespace DataAccess.Repositories;

public class ChatRepository : GenericRepository<Chat>, IChatRepository
{
    public ChatRepository(ApplicationDbContext db) : base(db) { }
}