using AutoMapper;
using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.Service.Abstractions;
using MaterialManagement.DAL.DB;
using MaterialManagement.DAL.Entities;
using MaterialManagement.DAL.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MaterialManagement.BLL.Service.Implementations
{
    public class ClientService : IClientService
    {
        private readonly IClientRepo _clientRepo;
        private readonly IMapper _mapper;
        private readonly MaterialManagementContext _context;
        public ClientService(IClientRepo clientRepo, MaterialManagementContext context, IMapper mapper)
        {
            _clientRepo = clientRepo;
            _context = context; // Store the injected context here
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
            // 1. تحقق أولاً من وجود فواتير مرتبطة مباشرة في قاعدة البيانات
            bool hasSalesInvoices = await _context.SalesInvoices
                                            .AnyAsync(inv => inv.ClientId == id); // ممكن تضيف && inv.IsActive لو بتستخدم soft delete

            // (أضف التحقق من مرتجعات البيع هنا لو محتاج)
            // bool hasReturnInvoices = await _context.PurchaseInvoices
            //                                  .AnyAsync(inv => inv.ClientId == id);

            // 2. امنع الحذف لو فيه فواتير
            if (hasSalesInvoices /* || hasReturnInvoices */)
            {
                throw new InvalidOperationException("❌ لا يمكن حذف العميل لأنه مرتبط بفواتير.");
            }

            // 3. لو مفيش فواتير، كمل الحذف
            var clientToDelete = await _clientRepo.GetByIdAsync(id); // جلب العميل للحذف
            if (clientToDelete == null)
            {
                throw new InvalidOperationException("❌ العميل المراد حذفه غير موجود أصلاً.");
            }

            // استخدم الحذف الفعلي أو الحذف الناعم (Soft Delete)
            await _clientRepo.DeleteAsync(id); // أو _clientRepo.Delete(clientToDelete); await _context.SaveChangesAsync();
                                               // أو clientToDelete.IsActive = false; _clientRepo.Update(clientToDelete); await _context.SaveChangesAsync();

            // لا تحتاج SaveChangesAsync هنا لو DeleteAsync بتعمل كده
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
        public IQueryable<Client> GetClientsAsQueryable()
        {
            return _clientRepo.GetAsQueryable();
        }
    }
}