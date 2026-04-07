using SharedKernel.Data;

namespace TradingSupervisorService.Migrations;

/// <summary>
/// Registry of all migrations for supervisor.db.
/// Add new migrations to the returned list in version order.
/// MigrationRunner will apply them transactionally and track completion.
/// </summary>
public static class SupervisorMigrations
{
    /// <summary>
    /// Returns the complete ordered list of migrations for supervisor.db.
    /// </summary>
    public static IReadOnlyList<IMigration> All => new IMigration[]
    {
        new SupervisorInitial001(),
        new Migration002_IvtsMonitoring(),
        // Future migrations will be added here:
        // new AddPositionIndexes003(),
        // new AddAlertSeverityColumn004(),
    };
}
