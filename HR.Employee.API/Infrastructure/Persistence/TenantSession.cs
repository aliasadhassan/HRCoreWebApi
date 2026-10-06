namespace HR.Employee.API.Infrastructure.Persistence;

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

/// <summary>
/// Postgres RLS ke liye tenant context: har connection khulte hi <c>app.tenant_id</c> set hota hai.
/// Policies <c>tenancy.current_tenant_id()</c> padhti hain; khali value = NULL = koi row nahi (fail closed).
/// Pool se aaya connection pichle request ka tenant le kar na aaye, is liye har open pe dobara set (ya khali) hota hai.
/// </summary>
internal static class TenantSession
{
    private const string Sql = "SELECT set_config('app.tenant_id', @tenant, false)";

    public static void Apply(DbConnection connection, Guid? tenantId, DbTransaction? transaction = null)
    {
        using var command = CreateCommand(connection, tenantId, transaction);
        command.ExecuteNonQuery();
    }

    public static async Task ApplyAsync(DbConnection connection, Guid? tenantId, CancellationToken ct, DbTransaction? transaction = null)
    {
        await using var command = CreateCommand(connection, tenantId, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static DbCommand CreateCommand(DbConnection connection, Guid? tenantId, DbTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        command.Transaction = transaction;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.DbType = DbType.String;
        parameter.Value = tenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }

    /// <summary>
    /// Supavisor transaction mode (port 6543) har statement pe server connection badal deta hai, to session setting
    /// kisi aur client ke connection pe reh jati hai. RLS wale app role (hr_*_app) ke saath sirf session mode (5432) chalega.
    /// </summary>
    public static void EnsureSessionPooling(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        if (csb.Port == 6543 && csb.Username?.StartsWith("hr_", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException(
                "Tenant RLS needs a session-mode connection. Use the Supabase pooler on port 5432, not 6543 (transaction mode).");
    }
}

/// <summary>AppDbContext ka har naya connection: SessionTenantId ko <c>app.tenant_id</c> mein likho.</summary>
internal sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    public static readonly TenantConnectionInterceptor Instance = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => TenantSession.Apply(connection, (eventData.Context as AppDbContext)?.SessionTenantId);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        => TenantSession.ApplyAsync(connection, (eventData.Context as AppDbContext)?.SessionTenantId, cancellationToken);
}
