namespace SharedKernel.Data;

/// <summary>
/// Represents a database schema migration.
/// Each migration has a unique version number and SQL to execute.
/// Migrations are applied in version order and tracked in schema_migrations table.
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Unique version number for this migration.
    /// Convention: 001, 002, 003, etc.
    /// MUST be unique within a migration set.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Human-readable name for this migration.
    /// Example: "SupervisorInitial" or "AddPositionIndexes"
    /// </summary>
    string Name { get; }

    /// <summary>
    /// SQL statements to execute when applying this migration.
    /// Can contain multiple statements separated by semicolons.
    /// Executed within a transaction by MigrationRunner.
    /// </summary>
    string UpSql { get; }
}
