using System.Text.Json.Serialization;

namespace DBSyncApp.Models
{
    public enum DbType { SqlServer, PostgreSQL }

    public class ConnectionProfile
    {
        public string Name { get; set; } = "";
        public DbType DbType { get; set; }
        public string Server { get; set; } = "";
        public int Port { get; set; }
        public string Database { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool UseWindowsAuth { get; set; }

        public string BuildConnectionString()
        {
            if (DbType == DbType.SqlServer)
            {
                if (UseWindowsAuth)
                    return $"Server={Server},{Port};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;";
                return $"Server={Server},{Port};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=True;";
            }
            else
            {
                return $"Host={Server};Port={Port};Database={Database};Username={Username};Password={Password};";
            }
        }

        public string DisplayName => string.IsNullOrEmpty(Name)
            ? $"{Server}/{Database}"
            : Name;

        public int DefaultPort => DbType == DbType.SqlServer ? 1433 : 5432;
    }

    public class TableDiff
    {
        public string TableName { get; set; } = "";
        public List<RowDiff> Rows { get; set; } = new();
        public int InsertCount => Rows.Count(r => r.DiffType == DiffType.Insert);
        public int UpdateCount => Rows.Count(r => r.DiffType == DiffType.Update);
        public int DeleteCount => Rows.Count(r => r.DiffType == DiffType.Delete);
        public bool HasChanges => Rows.Any();
    }

    public enum DiffType { Insert, Update, Delete }

    public class RowDiff
    {
        public DiffType DiffType { get; set; }
        public string PrimaryKey { get; set; } = "";
        public Dictionary<string, object?> SourceValues { get; set; } = new();
        public Dictionary<string, object?> TargetValues { get; set; } = new();
        public List<string> ChangedColumns { get; set; } = new();
        public string GeneratedSql { get; set; } = "";
        public string EditedSql { get; set; } = "";
        public bool IsSelected { get; set; } = true;

        public string FinalSql => string.IsNullOrWhiteSpace(EditedSql) ? GeneratedSql : EditedSql;
        public bool IsModified => !string.IsNullOrWhiteSpace(EditedSql) && EditedSql != GeneratedSql;
    }
}
