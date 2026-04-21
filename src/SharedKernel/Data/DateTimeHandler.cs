using Dapper;
using System.Data;

namespace SharedKernel.Data;

/// <summary>
/// Dapper TypeHandler for DateTime that preserves UTC kind.
/// Ensures DateTime values read from SQLite TEXT columns maintain DateTimeKind.Utc.
/// </summary>
public sealed class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        // Store as ISO 8601 string with 'Z' suffix for UTC
        parameter.Value = value.ToUniversalTime().ToString("O");
    }

    public override DateTime Parse(object value)
    {
        if (value is string str)
        {
            // Parse with RoundtripKind to preserve UTC indicator
            return DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        if (value is DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTime");
    }
}

/// <summary>
/// Nullable DateTime handler.
/// </summary>
public sealed class NullableDateTimeHandler : SqlMapper.TypeHandler<DateTime?>
{
    public override void SetValue(IDbDataParameter parameter, DateTime? value)
    {
        parameter.Value = value?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value;
    }

    public override DateTime? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        if (value is DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return null;
    }
}
