using MaterialManagement.BLL.ModelVM.Reports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaterialManagement.BLL.Service.Abstractions
{
    public interface IReportService
    {
        Task<List<AccountStatementViewModel>> GetClientAccountStatementAsync(int clientId, DateTime? fromDate, DateTime? toDate);
        Task<List<AccountStatementViewModel>> GetSupplierAccountStatementAsync(int supplierId, DateTime? fromDate, DateTime? toDate);
        Task<List<MaterialMovementViewModel>> GetMaterialMovementAsync(int materialId, DateTime? fromDate, DateTime? toDate);
        Task<List<ProfitReportViewModel>> GetProfitReportAsync(DateTime fromDate, DateTime toDate);

    }
}
