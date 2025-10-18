using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore; // مهم جداً عشان ToListAsync

namespace MaterialManagement.DAL.Repo.Implementations
{
    public class ClientRepo : IClientRepo
    {
        private readonly MaterialManagementContext _context;

        public ClientRepo(MaterialManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Client>> GetAllAsync()
        {
            return await _context.Clients
                .OrderBy(c => c.Name)    // يرتبهم بالاسم
                .ToListAsync();
        }

        public async Task<Client?> GetByIdAsync(int id)
        {
            return await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id );
        }

        public async Task<Client> AddAsync(Client client)
        {
            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task<Client> UpdateAsync(Client client)
        {
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task DeleteAsync(int id)
        {
            var client = await GetByIdAsync(id);
            if (client != null)
            {
                _context.Clients.Remove(client);      
                await _context.SaveChangesAsync();    
            }
        }

        public async Task<IEnumerable<Client>> SearchAsync(string searchTerm)
        {
            return await _context.Clients
                .Where(c => c.IsActive &&
                    (c.Name.Contains(searchTerm) ||
                     (c.Phone != null && c.Phone.Contains(searchTerm)) ||
                     (c.Address != null && c.Address.Contains(searchTerm))))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Client>> GetClientsWithBalanceAsync()
        {
            return await _context.Clients
                .Where(c => c.IsActive && c.Balance != 0)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        public async Task ReactivateAsync(int id)
        {
            var client = await GetByIdAsync(id);
            if (client != null && !client.IsActive)
            {
                client.IsActive = true;
                await UpdateAsync(client);
            }
        }

        public async Task<bool> PhoneExistsAsync(string phone, int excludeClientId = 0)
        {
            return await _context.Clients.AnyAsync(c => c.Phone == phone && c.Id != excludeClientId);
        }

        public IQueryable<Client> GetAsQueryable()
        {
            return _context.Clients.AsQueryable();
        }
        public async Task<Client?> GetByIdForUpdateAsync(int id)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}