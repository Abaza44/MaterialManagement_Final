using MaterialManagement.BLL.ModelVM.Client;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IClientService
    {
        Task<IEnumerable<ClientViewModel>> GetAllClientsAsync();
        Task<ClientViewModel?> GetClientByIdAsync(int id);
        Task<ClientViewModel> CreateClientAsync(ClientCreateModel model);
        Task<ClientViewModel> UpdateClientAsync(int id, ClientUpdateModel model);
        Task DeleteClientAsync(int id);
        Task<IEnumerable<ClientViewModel>> SearchClientsAsync(string searchTerm);
        Task<IEnumerable<ClientViewModel>> GetClientsWithBalanceAsync();
        Task ReactivateClientAsync(int id);
    }
}