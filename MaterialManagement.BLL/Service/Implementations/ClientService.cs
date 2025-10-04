using AutoMapper;
using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ClientService : IClientService
    {
        private readonly IClientRepo _clientRepo;
        private readonly IMapper _mapper;

        public ClientService(IClientRepo clientRepo, IMapper mapper)
        {
            _clientRepo = clientRepo;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ClientViewModel>> GetAllClientsAsync()
        {
            var clients = await _clientRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<ClientViewModel>>(clients);
        }

        public async Task<ClientViewModel?> GetClientByIdAsync(int id)
        {
            var client = await _clientRepo.GetByIdAsync(id);
            return client != null ? _mapper.Map<ClientViewModel>(client) : null;
        }

        public async Task<ClientViewModel> CreateClientAsync(ClientCreateModel model)
        {
            // تحقق لو فيه عميل بنفس رقم الهاتف
            var allClients = await _clientRepo.GetAllAsync();
            if (!string.IsNullOrEmpty(model.Phone) && allClients.Any(c => c.Phone == model.Phone))
            {
                throw new InvalidOperationException("❌ يوجد عميل مسجل بنفس رقم الهاتف بالفعل");
            }

            var client = _mapper.Map<Client>(model);
            client.CreatedDate = DateTime.Now;

            var createdClient = await _clientRepo.AddAsync(client);
            return _mapper.Map<ClientViewModel>(createdClient);
        }

        public async Task<ClientViewModel> UpdateClientAsync(int id, ClientUpdateModel model)
        {
            var existingClient = await _clientRepo.GetByIdAsync(id);
            if (existingClient == null)
                throw new InvalidOperationException("❌ العميل غير موجود");

            // تحقق عند التعديل
            var allClients = await _clientRepo.GetAllAsync();
            if (!string.IsNullOrEmpty(model.Phone) && allClients.Any(c => c.Phone == model.Phone && c.Id != id))
            {
                throw new InvalidOperationException("❌ رقم الهاتف مستخدم بالفعل من عميل آخر");
            }

            _mapper.Map(model, existingClient);
            var updatedClient = await _clientRepo.UpdateAsync(existingClient);
            return _mapper.Map<ClientViewModel>(updatedClient);
        }

        public async Task DeleteClientAsync(int id)
        {
            var client = await _clientRepo.GetByIdAsync(id);
            if (client == null)
                throw new InvalidOperationException("❌ العميل غير موجود");

            // تحقق لو عنده فواتير بيع
            if (client.SalesInvoices != null && client.SalesInvoices.Any())
                throw new InvalidOperationException("❌ لا يمكن حذف العميل لأنه مرتبط بفواتير مبيعات");

            await _clientRepo.DeleteAsync(id);
        }

        public async Task<IEnumerable<ClientViewModel>> SearchClientsAsync(string searchTerm)
        {
            var clients = string.IsNullOrEmpty(searchTerm)
                ? await _clientRepo.GetAllAsync()
                : await _clientRepo.SearchAsync(searchTerm);

            return _mapper.Map<IEnumerable<ClientViewModel>>(clients);
        }

        public async Task<IEnumerable<ClientViewModel>> GetClientsWithBalanceAsync()
        {
            var clients = await _clientRepo.GetClientsWithBalanceAsync();
            return _mapper.Map<IEnumerable<ClientViewModel>>(clients);
        }
        public async Task ReactivateClientAsync(int id)
        {
            var client = await _clientRepo.GetByIdAsync(id);
            if (client == null)
                throw new InvalidOperationException("العميل غير موجود");

            if (client.IsActive)
                throw new InvalidOperationException("العميل نشط بالفعل");

            client.IsActive = true;
            await _clientRepo.UpdateAsync(client);
        }

    }
}