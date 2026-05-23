using System.Data;
using System.Data.Common;
using System.Text;
using DBSyncApp.Models;
using Microsoft.Data.SqlClient;
using Npgsql;
using DbType = DBSyncApp.Models.DbType;

namespace DBSyncApp.Services
{
    public class DatabaseService
    {
        private readonly ConnectionProfile _profile;

        public DatabaseService(ConnectionProfile profile)
        {
            _profile = profile;
        }

        private DbConnection CreateConnection()
        {
            if (_profile.DbType == DbType.SqlServer)
                return new SqlConnection(_profile.BuildConnectionString());
            else
                return new NpgsqlConnection(_profile.BuildConnectionString());
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = _profile.DbType == DbType.SqlServer
                ? "SELECT name FROM sys.databases WHERE name NOT IN ('master','tempdb','model','msdb') ORDER BY name"
                : "SELECT datname FROM pg_database WHERE datistemplate = false AND datname NOT IN ('postgres') ORDER BY datname";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                databases.Add(reader.GetString(0));

            return databases;
        }

        public async Task<List<string>> GetTablesAsync()
        {
            var tables = new List<string>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = _profile.DbType == DbType.SqlServer
                ? "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME"
                : "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type='BASE TABLE' ORDER BY table_schema, table_name";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));

            return tables;
        }

        public async Task<string?> GetPrimaryKeyAsync(string fullTableName)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            var parts = fullTableName.Split('.');
            string schema = parts.Length > 1 ? parts[0] : "dbo";
            string table = parts.Length > 1 ? parts[1] : parts[0];

            string sql;
            if (_profile.DbType == DbType.SqlServer)
                sql = $@"SELECT c.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                         JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE c ON tc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                         WHERE tc.CONSTRAINT_TYPE='PRIMARY KEY' AND tc.TABLE_NAME='{table}' AND tc.TABLE_SCHEMA='{schema}'";
            else
                sql = $@"SELECT a.attname FROM pg_index i
                         JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=ANY(i.indkey)
                         JOIN pg_class c ON c.oid=i.indrelid
                         JOIN pg_namespace n ON n.oid=c.relnamespace
                         WHERE i.indisprimary AND c.relname='{table}' AND n.nspname='{schema}'";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        public async Task<DataTable> FetchTableDataAsync(string fullTableName)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string quotedTable = QuoteTable(fullTableName);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {quotedTable}";
            cmd.CommandTimeout = 120;

            using var adapter = _profile.DbType == DbType.SqlServer
                ? (DbDataAdapter)new SqlDataAdapter((SqlCommand)cmd)
                : new NpgsqlDataAdapter((NpgsqlCommand)cmd);

            var dt = new DataTable(fullTableName);
            adapter.Fill(dt);
            return dt;
        }

        public async Task<List<string>> GetColumnsAsync(string fullTableName)
        {
            var parts = fullTableName.Split('.');
            string schema = parts.Length > 1 ? parts[0] : "dbo";
            string table = parts.Length > 1 ? parts[1] : parts[0];

            using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='{schema}' AND TABLE_NAME='{table}' ORDER BY ORDINAL_POSITION";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var cols = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                cols.Add(reader.GetString(0));
            return cols;
        }

        public async Task<(int success, int failed, List<string> errors)> ExecuteSqlBatchAsync(IEnumerable<string> statements)
        {
            int success = 0, failed = 0;
            var errors = new List<string>();

            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                foreach (var sql in statements)
                {
                    if (string.IsNullOrWhiteSpace(sql)) continue;
                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        await cmd.ExecuteNonQueryAsync();
                        success++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"SQL: {sql[..Math.Min(80, sql.Length)]}...\nError: {ex.Message}");
                    }
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return (success, failed, errors);
        }

        private string QuoteTable(string fullTableName)
        {
            var parts = fullTableName.Split('.');
            if (_profile.DbType == DbType.SqlServer)
                return parts.Length > 1 ? $"[{parts[0]}].[{parts[1]}]" : $"[{parts[0]}]";
            else
                return parts.Length > 1 ? $"\"{parts[0]}\".\"{parts[1]}\"" : $"\"{parts[0]}\"";
        }
    }

    public class DiffService
    {
        public static async Task<TableDiff> ComputeDiffAsync(
            DatabaseService sourceDb,
            DatabaseService targetDb,
            string tableName,
            IProgress<string>? progress = null)
        {
            progress?.Report($"Fetching primary key for {tableName}...");
            var pk = await sourceDb.GetPrimaryKeyAsync(tableName) ?? "id";

            progress?.Report($"Loading source data for {tableName}...");
            var sourceData = await sourceDb.FetchTableDataAsync(tableName);

            progress?.Report($"Loading target data for {tableName}...");
            DataTable targetData;
            try { targetData = await targetDb.FetchTableDataAsync(tableName); }
            catch { targetData = new DataTable(); }

            progress?.Report($"Computing diff for {tableName}...");
            return ComputeDiff(tableName, pk, sourceData, targetData);
        }

        private static TableDiff ComputeDiff(string tableName, string pk, DataTable source, DataTable target)
        {
            var diff = new TableDiff { TableName = tableName };

            // Index target rows by PK
            var targetIndex = new Dictionary<string, DataRow>();
            if (target.Columns.Contains(pk))
                foreach (DataRow row in target.Rows)
                    targetIndex[row[pk]?.ToString() ?? ""] = row;

            var columns = source.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            foreach (DataRow sourceRow in source.Rows)
            {
                string pkVal = sourceRow[pk]?.ToString() ?? "";

                if (!targetIndex.TryGetValue(pkVal, out var targetRow))
                {
                    // INSERT
                    var rowDiff = new RowDiff
                    {
                        DiffType = DiffType.Insert,
                        PrimaryKey = pkVal,
                        SourceValues = columns.ToDictionary(c => c, c => (object?)sourceRow[c])
                    };
                    rowDiff.GeneratedSql = BuildInsert(tableName, rowDiff.SourceValues);
                    diff.Rows.Add(rowDiff);
                }
                else
                {
                    // Check for changes
                    var changed = columns
                        .Where(c => target.Columns.Contains(c))
                        .Where(c => !ValuesEqual(sourceRow[c], targetRow[c]))
                        .ToList();

                    if (changed.Any())
                    {
                        var rowDiff = new RowDiff
                        {
                            DiffType = DiffType.Update,
                            PrimaryKey = pkVal,
                            SourceValues = columns.ToDictionary(c => c, c => (object?)sourceRow[c]),
                            TargetValues = target.Columns.Cast<DataColumn>()
                                .Select(c => c.ColumnName)
                                .ToDictionary(c => c, c => (object?)targetRow[c]),
                            ChangedColumns = changed
                        };
                        rowDiff.GeneratedSql = BuildUpdate(tableName, pk, pkVal, changed, rowDiff.SourceValues);
                        diff.Rows.Add(rowDiff);
                    }
                }
            }

            return diff;
        }

        private static bool ValuesEqual(object? a, object? b)
        {
            if (a == DBNull.Value || a == null) return b == DBNull.Value || b == null;
            if (b == DBNull.Value || b == null) return false;
            return a.ToString() == b.ToString();
        }

        private static string Escape(object? val)
        {
            if (val == null || val == DBNull.Value) return "NULL";
            if (val is bool b) return b ? "1" : "0";
            if (val is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
            var s = val.ToString()?.Replace("'", "''") ?? "";
            return $"'{s}'";
        }

        private static string BuildInsert(string table, Dictionary<string, object?> values)
        {
            var cols = string.Join(", ", values.Keys);
            var vals = string.Join(", ", values.Values.Select(Escape));
            return $"INSERT INTO {table} ({cols}) VALUES ({vals});";
        }

        private static string BuildUpdate(string table, string pk, string pkVal,
            List<string> cols, Dictionary<string, object?> values)
        {
            var sets = string.Join(", ", cols.Select(c => $"{c} = {Escape(values[c])}"));
            return $"UPDATE {table} SET {sets} WHERE {pk} = {Escape(pkVal)};";
        }
    }
}
