using MaterialManagement.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Repo.Abstractions
{
    public interface IClientRepo
    {
        Task<IEnumerable<Client>> GetAllAsync();
        Task<Client?> GetByIdAsync(int id);
        Task<Client> AddAsync(Client client);
        Task<Client> UpdateAsync(Client client);
        Task DeleteAsync(int id);
        Task<IEnumerable<Client>> SearchAsync(string searchTerm);
        Task<IEnumerable<Client>> GetClientsWithBalanceAsync();
        Task<bool> PhoneExistsAsync(string phone, int excludeClientId = 0);
        IQueryable<Client> GetAsQueryable();
        Task<Client?> GetByIdForUpdateAsync(int id);
    }
}
