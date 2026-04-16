namespace MaterialManagement.PL.Services
{
    public interface ISupervisorAuthorizationService
    {
        bool TryAuthorize(string? password, out string errorMessage);
    }
}
