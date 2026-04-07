using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Registry of all migrations for options.db.
/// Add new migrations to the returned list in version order.
/// MigrationRunner will apply them transactionally and track completion.
/// </summary>
public static class OptionsMigrations
{
    /// <summary>
    /// Returns the complete ordered list of migrations for options.db.
    /// </summary>
    public static IReadOnlyList<IMigration> All => new IMigration[]
    {
        new OptionsInitial001(),
        new AddGreeksColumns002(),
        new AddOrderTracking003(),
        // Future migrations will be added here:
        // new AddRollHistoryTable004(),
    };
}
