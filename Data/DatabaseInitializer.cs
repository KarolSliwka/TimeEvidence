using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TimeEvidence.Data
{
    public class DatabaseInitializer
    {
        private readonly TimeEvidenceDbContext _db;

        public DatabaseInitializer(TimeEvidenceDbContext db)
        {
            _db = db;
        }

        public async Task EnsureSchemaUpdatedAsync()
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                // Check Supervisor table columns and add missing ones
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('Supervisors')";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var colName = reader.GetString(1);
                        existingColumns.Add(colName);
                    }
                }

                var commands = new List<string>();
                if (!existingColumns.Contains("PhoneNumber"))
                {
                    commands.Add("ALTER TABLE Supervisors ADD COLUMN PhoneNumber TEXT NULL");
                }
                if (!existingColumns.Contains("NotificationPreference"))
                {
                    commands.Add("ALTER TABLE Supervisors ADD COLUMN NotificationPreference INTEGER NOT NULL DEFAULT 0");
                }

                foreach (var sql in commands)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = sql;
                    await alter.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}
