using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class UserRepository(AppDbContext dbContext) : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            dbContext.Users.Add(user);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            UsersWithTokens().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        public Task<User?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default) =>
            UsersWithTokens().FirstOrDefaultAsync(u => u.GoogleSubject == googleSubject, cancellationToken);

        public Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            UsersWithTokens().FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.TokenHash == tokenHash), cancellationToken);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            // User is already tracked: Update() would mark new tokens as Modified
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        private IQueryable<User> UsersWithTokens() => dbContext.Users.Include(u => u.RefreshTokens);
    }
}
