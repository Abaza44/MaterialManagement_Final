using AutoMapper;
using MaterialManagement.BLL.ModelVM.Client;
using MaterialManagement.BLL.ModelVM.Employee;
using MaterialManagement.BLL.ModelVM.Equipment;
using MaterialManagement.BLL.ModelVM.Expense;
using MaterialManagement.BLL.ModelVM.Invoice;
using MaterialManagement.BLL.ModelVM.Maintenance;
using MaterialManagement.BLL.ModelVM.Material;
using MaterialManagement.BLL.ModelVM.Payment;
using MaterialManagement.BLL.ModelVM.Reservation;
using MaterialManagement.BLL.ModelVM.Supplier;
using MaterialManagement.DAL.DTOs;
using MaterialManagement.DAL.Entities;

namespace MaterialManagement.BLL.Helper
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // === Client Mappings ===
            CreateMap<Client, ClientViewModel>();
            CreateMap<ClientCreateModel, Client>();
            CreateMap<ClientUpdateModel, Client>();
            CreateMap<ClientInvoiceSummaryDto, ClientInvoiceSummaryViewModel>();
            // === Supplier Mappings ===
            CreateMap<Supplier, SupplierViewModel>();
            CreateMap<SupplierCreateModel, Supplier>();
            CreateMap<SupplierUpdateModel, Supplier>();

            // === Material Mappings ===
            CreateMap<Material, MaterialViewModel>()
                .ForMember(dest => dest.AvailableQuantity,
                    opt => opt.MapFrom(src => src.Quantity - src.ReservedQuantity));
            CreateMap<MaterialCreateModel, Material>();
            CreateMap<MaterialUpdateModel, Material>();

            // === Employee Mappings ===
            CreateMap<Employee, EmployeeViewModel>();
            CreateMap<EmployeeCreateModel, Employee>();
            CreateMap<EmployeeUpdateModel, Employee>();
            CreateMap<EmployeeViewModel, EmployeeUpdateModel>();

            // === Equipment & Maintenance Mappings ===
            CreateMap<Equipment, EquipmentViewModel>()
                .ForMember(dest => dest.MaintenanceHistory, opt => opt.MapFrom(src => src.MaintenanceHistory))
                .ForMember(dest => dest.LastMaintenanceDate, opt => opt.MapFrom(src =>
                    src.MaintenanceHistory.Any()
                    ? src.MaintenanceHistory.OrderByDescending(m => m.MaintenanceDate).FirstOrDefault().MaintenanceDate
                    : (DateTime?)null
                ));
            CreateMap<EquipmentCreateModel, Equipment>();

            CreateMap<EquipmentUpdateModel, Equipment>();
            CreateMap<EquipmentViewModel, EquipmentUpdateModel>();

            CreateMap<MaintenanceRecord, MaintenanceRecordViewModel>();
            CreateMap<MaintenanceRecordCreateModel, MaintenanceRecord>();

            // === Expense Mappings ===
            CreateMap<Expense, ExpenseViewModel>()
                .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee != null ? src.Employee.Name : null));
            CreateMap<ExpenseCreateModel, Expense>();
            CreateMap<ExpenseUpdateModel, Expense>();
            CreateMap<ExpenseViewModel, ExpenseUpdateModel>();

            // === Sales Invoice Mappings ===
            CreateMap<SalesInvoice, SalesInvoiceViewModel>()
                .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client.Name))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.SalesInvoiceItems));

            CreateMap<SalesInvoiceItem, SalesInvoiceItemViewModel>()
                .ForMember(dest => dest.MaterialCode, opt => opt.MapFrom(src => src.Material.Code))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.Name));
            // ...
            CreateMap<SalesInvoiceCreateModel, SalesInvoice>();
            CreateMap<SalesInvoiceItemCreateModel, SalesInvoiceItem>();
            CreateMap<SalesInvoice, InvoiceSummaryViewModel>();
            // === Purchase Invoice Mappings (مع دعم المرتجعات) ===
            CreateMap<PurchaseInvoice, PurchaseInvoiceViewModel>()
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : null))
                .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client != null ? src.Client.Name : null)) 
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.PurchaseInvoiceItems)); // <<< هذا هو الحل
            CreateMap<PurchaseInvoiceItem, PurchaseInvoiceItemViewModel>()
                .ForMember(dest => dest.MaterialCode, opt => opt.MapFrom(src => src.Material.Code))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.Name));
            CreateMap<PurchaseInvoiceCreateModel, PurchaseInvoice>();
            CreateMap<PurchaseInvoiceItemCreateModel, PurchaseInvoiceItem>();
            CreateMap<SupplierInvoicesDto, SupplierInvoicesViewModel>();
            // Inside the public AutoMapperProfile() constructor

            // This is the map for the Details page (you already have this)
            CreateMap<Reservation, ReservationDetailsViewModel>()
                .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client.Name));

            // --- ADD THIS MISSING MAP FOR THE INDEX PAGE ---
            CreateMap<Reservation, ReservationIndexViewModel>()
                .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client.Name));

            // This is the map for the items (you already have this)
            CreateMap<ReservationItem, ReservationItemModel>()
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.Name));

            // === Payment Mappings ===
            CreateMap<ClientPayment, ClientPaymentViewModel>();
            CreateMap<ClientPaymentCreateModel, ClientPayment>();
            CreateMap<SupplierPayment, SupplierPaymentViewModel>();
            CreateMap<SupplierPaymentCreateModel, SupplierPayment>();
            CreateMap<SupplierInvoicesDto, SupplierInvoicesViewModel>();
            CreateMap<SupplierInvoicesDto, SupplierInvoiceSummaryViewModel>();
            CreateMap<ReservationCreateModel, Reservation>();
        }
    }
}