using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialManagement.DAL.Enums
{
    public enum InvoiceStatus
    {
        Pending = 1,
        Paid = 2,
        PartiallyPaid = 3,
        Cancelled = 4
    }

    public enum ExpenseCategory
    {
        Office = 1,
        Equipment = 2,
        Salary = 3,
        Maintenance = 4,
        Utilities = 5,
        Other = 6
    }

    public enum EmployeePosition
    {
        Driver = 1,
        Accountant = 2,
        Warehouse = 3,
        Other = 4
    }

    public enum ReservationStatus
    {
        Active,
        Fulfilled, 
        Cancelled
    }

    public enum SalesInvoicePartyMode
    {
        RegisteredClient = 1,
        WalkInCustomer = 2
    }

    public enum PurchaseInvoicePartyMode
    {
        RegisteredSupplier = 1,
        OneTimeSupplier = 2,
        RegisteredClientReturn = 3
    }
}
