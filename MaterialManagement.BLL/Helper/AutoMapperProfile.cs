using AutoMapper;
using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.BLL.ModelVM.Equipment;
using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Maintenance;
using MaterialManagement.BLL.ModelVM.Material;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.DAL.Entities;

namespace MaterialManagement.BLL.Helper
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // === Client Mappings ===
            CreateMap<Client, ClientViewModel>().ReverseMap(); // ReverseMap for Edit
            CreateMap<ClientCreateModel, Client>();
            CreateMap<ClientUpdateModel, Client>();
            
            // === Supplier Mappings ===
            CreateMap<Supplier, SupplierViewModel>().ReverseMap();
            CreateMap<SupplierCreateModel, Supplier>();
            CreateMap<SupplierUpdateModel, Supplier>();

            // === Material Mappings ===
            CreateMap<Material, MaterialViewModel>().ReverseMap();
            CreateMap<MaterialCreateModel, Material>();
            CreateMap<MaterialUpdateModel, Material>();

            // === Employee Mappings ===
            CreateMap<Employee, EmployeeViewModel>();
            CreateMap<EmployeeCreateModel, Employee>();
            CreateMap<EmployeeUpdateModel, Employee>();
            CreateMap<EmployeeViewModel, EmployeeUpdateModel>(); // For Edit GET
            CreateMap<Equipment, EquipmentViewModel>()
    // <<< هذا السطر هو الأهم >>>
            .ForMember(dest => dest.MaintenanceHistory, opt => opt.MapFrom(src => src.MaintenanceHistory));

            CreateMap<MaintenanceRecord, MaintenanceRecordViewModel>();
            // === Equipment & Maintenance Mappings ===
            CreateMap<Equipment, EquipmentViewModel>()
                .ForMember(dest => dest.MaintenanceHistory, opt => opt.MapFrom(src => src.MaintenanceHistory));
            CreateMap<EquipmentCreateModel, Equipment>();
            CreateMap<EquipmentUpdateModel, Equipment>();
            CreateMap<EquipmentViewModel, EquipmentUpdateModel>(); // For Edit GET

            CreateMap<MaintenanceRecord, MaintenanceRecordViewModel>();
            CreateMap<MaintenanceRecordCreateModel, MaintenanceRecord>();

            // === Expense Mappings ===
            CreateMap<Expense, ExpenseViewModel>()
                .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee != null ? src.Employee.Name : null));
            CreateMap<ExpenseCreateModel, Expense>();
            CreateMap<ExpenseUpdateModel, Expense>();
            CreateMap<ExpenseViewModel, ExpenseUpdateModel>(); // For Edit GET

            // === Sales Invoice Mappings ===
            CreateMap<SalesInvoice, SalesInvoiceViewModel>()
                .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client.Name))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.SalesInvoiceItems));

            CreateMap<SalesInvoiceItem, SalesInvoiceItemViewModel>()
                .ForMember(dest => dest.MaterialCode, opt => opt.MapFrom(src => src.Material.Code))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.Name));

            CreateMap<SalesInvoiceCreateModel, SalesInvoice>();
            CreateMap<SalesInvoiceItemCreateModel, SalesInvoiceItem>();

            // === Purchase Invoice Mappings ===
            CreateMap<PurchaseInvoice, PurchaseInvoiceViewModel>()
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier.Name))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.PurchaseInvoiceItems));

            CreateMap<PurchaseInvoiceItem, PurchaseInvoiceItemViewModel>()
                .ForMember(dest => dest.MaterialCode, opt => opt.MapFrom(src => src.Material.Code))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.Name));

            CreateMap<PurchaseInvoiceCreateModel, PurchaseInvoice>();
            CreateMap<PurchaseInvoiceItemCreateModel, PurchaseInvoiceItem>();

            // === Payment Mappings ===
            CreateMap<ClientPayment, ClientPaymentViewModel>();
            CreateMap<ClientPaymentCreateModel, ClientPayment>();

            CreateMap<SupplierPayment, SupplierPaymentViewModel>();
            CreateMap<SupplierPaymentCreateModel, SupplierPayment>();
        }
    }
}