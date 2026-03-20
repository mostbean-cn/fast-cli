using Microsoft.Data.Sqlite;

namespace FastCli.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _databasePath;
    private readonly string _schemaSql;
    private bool _initialized;

    public SqliteDatabaseInitializer(string databasePath, string schemaSql)
    {
        _databasePath = databasePath;
        _schemaSql = schemaSql;
    }

    public string DatabasePath => _databasePath;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = _schemaSql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
