using Microsoft.Data.SqlClient;
using ReportPoc.Models;

namespace ReportPoc.Services;

public interface IDatabaseMetadataService
{
    Task<List<SchemaTableInfo>> GetTablesAndColumnsAsync(
        string connectionString,
        string? schema,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseMetadataService : IDatabaseMetadataService
{
    public async Task<List<SchemaTableInfo>> GetTablesAndColumnsAsync(
        string connectionString,
        string? schema,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_SCHEMA = @Schema
  AND c.TABLE_NAME IS NOT NULL
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Schema", (schema ?? "dbo"));

        var tableMap = new Dictionary<string, SchemaTableInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            if (!tableMap.TryGetValue(tableName, out var table))
            {
                table = new SchemaTableInfo { TableName = tableName };
                tableMap[tableName] = table;
            }

            table.Columns.Add(new SchemaColumnInfo
            {
                ColumnName = reader.GetString(1),
                DataType = reader.GetString(2),
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                IsNullable = reader.GetString(4).Equals("YES", StringComparison.OrdinalIgnoreCase)
            });
        }

        return tableMap.Values.OrderBy(x => x.TableName).ToList();
    }
}
