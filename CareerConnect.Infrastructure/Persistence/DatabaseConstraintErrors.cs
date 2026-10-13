using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerConnect.Infrastructure.Persistence;

internal static class DatabaseConstraintErrors
{
    public const string CompanyTaxCodeUniqueIndex = "IX_companies_profile_TaxCode";

    public static bool IsTaxCodeUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == CompanyTaxCodeUniqueIndex;
    }
}
