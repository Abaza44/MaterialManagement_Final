using System.Security.Cryptography;
using System.Text;
using MaterialManagement.PL.Models;
using Microsoft.Extensions.Options;

namespace MaterialManagement.PL.Services
{
    public class SupervisorAuthorizationService : ISupervisorAuthorizationService
    {
        private const string HashPrefix = "pbkdf2-sha256";
        private readonly SupervisorAuthorizationOptions _options;

        public SupervisorAuthorizationService(IOptions<SupervisorAuthorizationOptions> options)
        {
            _options = options.Value;
        }

        public bool TryAuthorize(string? password, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(_options.PasswordHash) && string.IsNullOrWhiteSpace(_options.Password))
            {
                errorMessage = "لم يتم إعداد كلمة مرور المشرف للحذف. يرجى إعدادها في الإعدادات قبل تنفيذ الحذف.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "يجب إدخال كلمة مرور المشرف لإتمام الحذف.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_options.PasswordHash))
            {
                if (VerifyPbkdf2Hash(password, _options.PasswordHash))
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "كلمة مرور المشرف غير صحيحة. لم يتم تنفيذ الحذف.";
                return false;
            }

            if (FixedTimeEquals(password, _options.Password!))
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "كلمة مرور المشرف غير صحيحة. لم يتم تنفيذ الحذف.";
            return false;
        }

        private static bool VerifyPbkdf2Hash(string password, string configuredHash)
        {
            var parts = configuredHash.Split(':');
            if (parts.Length != 4 || !string.Equals(parts[0], HashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations) || iterations < 100_000)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(string actual, string expected)
        {
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            return actualBytes.Length == expectedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
    }
}
