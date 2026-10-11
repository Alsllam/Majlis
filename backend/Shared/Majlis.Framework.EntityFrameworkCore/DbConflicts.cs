using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Framework.EntityFrameworkCore;

/// <summary>Recognizes write conflicts caused by concurrent requests, which the API reports as 409.</summary>
public static class DbConflicts
{
    /// <summary>
    /// True for a row-version conflict, or a unique-index violation (SQL Server 2601/2627) — e.g. two commands on the
    /// same session trying to append the same <c>seq</c>. Neither write happened; the client re-reads and retries.
    /// </summary>
    public static bool IsConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => true,
        _ => false,
    };
}
