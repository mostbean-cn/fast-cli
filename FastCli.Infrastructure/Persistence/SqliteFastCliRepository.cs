using System.Text.Json;
using FastCli.Application.Abstractions;
using FastCli.Domain.Enums;
using FastCli.Domain.Models;
using Microsoft.Data.Sqlite;

namespace FastCli.Infrastructure.Persistence;

public sealed class SqliteFastCliRepository : IFastCliRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly SqliteDatabaseInitializer _databaseInitializer;

    public SqliteFastCliRepository(SqliteDatabaseInitializer databaseInitializer)
    {
        _databaseInitializer = databaseInitializer;
    }

    public async Task<IReadOnlyList<CommandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, sort_order, created_at, updated_at
            FROM command_groups
            ORDER BY sort_order, created_at;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<CommandGroup>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CommandGroup
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(4))
            });
        }

        return results;
    }

    public async Task<CommandGroup?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, sort_order, created_at, updated_at
            FROM command_groups
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", groupId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CommandGroup
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            SortOrder = reader.GetInt32(2),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(4))
        };
    }

    public async Task SaveGroupAsync(CommandGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO command_groups (id, name, sort_order, created_at, updated_at)
            VALUES ($id, $name, $sortOrder, $createdAt, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                sort_order = excluded.sort_order,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", group.Id.ToString());
        command.Parameters.AddWithValue("$name", group.Name);
        command.Parameters.AddWithValue("$sortOrder", group.SortOrder);
        command.Parameters.AddWithValue("$createdAt", group.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", group.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM command_groups WHERE id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", groupId.ToString());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await NormalizeGroupSortOrdersAsync(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReorderGroupsAsync(IReadOnlyList<Guid> orderedGroupIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var allIds = await GetOrderedGroupIdsAsync(connection, transaction, cancellationToken);
        var finalOrder = BuildOrderedIds(allIds, orderedGroupIds);

        for (var index = 0; index < finalOrder.Count; index++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE command_groups SET sort_order = $sortOrder WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", index);
            command.Parameters.AddWithValue("$id", finalOrder[index].ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandProfile>> GetCommandsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, group_id, name, description, working_directory, shell_type, run_mode,
                   command_text, arguments_json, environment_variables_json, run_as_administrator,
                   sort_order, created_at, updated_at
            FROM command_profiles
            WHERE group_id = $groupId
            ORDER BY sort_order, created_at;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<CommandProfile>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapCommandProfile(reader));
        }

        return results;
    }

    public async Task<CommandProfile?> GetCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, group_id, name, description, working_directory, shell_type, run_mode,
                   command_text, arguments_json, environment_variables_json, run_as_administrator,
                   sort_order, created_at, updated_at
            FROM command_profiles
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", commandId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapCommandProfile(reader);
    }

    public async Task SaveCommandAsync(CommandProfile commandProfile, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO command_profiles (
                id, group_id, name, description, working_directory, shell_type, run_mode,
                command_text, arguments_json, environment_variables_json, run_as_administrator,
                sort_order, created_at, updated_at)
            VALUES (
                $id, $groupId, $name, $description, $workingDirectory, $shellType, $runMode,
                $commandText, $argumentsJson, $environmentVariablesJson, $runAsAdministrator,
                $sortOrder, $createdAt, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                group_id = excluded.group_id,
                name = excluded.name,
                description = excluded.description,
                working_directory = excluded.working_directory,
                shell_type = excluded.shell_type,
                run_mode = excluded.run_mode,
                command_text = excluded.command_text,
                arguments_json = excluded.arguments_json,
                environment_variables_json = excluded.environment_variables_json,
                run_as_administrator = excluded.run_as_administrator,
                sort_order = excluded.sort_order,
                updated_at = excluded.updated_at;
            """;

        command.Parameters.AddWithValue("$id", commandProfile.Id.ToString());
        command.Parameters.AddWithValue("$groupId", commandProfile.GroupId.ToString());
        command.Parameters.AddWithValue("$name", commandProfile.Name);
        command.Parameters.AddWithValue("$description", commandProfile.Description);
        command.Parameters.AddWithValue("$workingDirectory", (object?)commandProfile.WorkingDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$shellType", commandProfile.ShellType.ToString());
        command.Parameters.AddWithValue("$runMode", commandProfile.RunMode.ToString());
        command.Parameters.AddWithValue("$commandText", commandProfile.CommandText);
        command.Parameters.AddWithValue("$argumentsJson", JsonSerializer.Serialize(commandProfile.Arguments, JsonOptions));
        command.Parameters.AddWithValue("$environmentVariablesJson", JsonSerializer.Serialize(commandProfile.EnvironmentVariables, JsonOptions));
        command.Parameters.AddWithValue("$runAsAdministrator", commandProfile.RunAsAdministrator ? 1 : 0);
        command.Parameters.AddWithValue("$sortOrder", commandProfile.SortOrder);
        command.Parameters.AddWithValue("$createdAt", commandProfile.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", commandProfile.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        Guid? groupId = null;

        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = "SELECT group_id FROM command_profiles WHERE id = $id LIMIT 1;";
            selectCommand.Parameters.AddWithValue("$id", commandId.ToString());
            var result = await selectCommand.ExecuteScalarAsync(cancellationToken);

            if (result is string groupIdValue)
            {
                groupId = Guid.Parse(groupIdValue);
            }
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM command_profiles WHERE id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", commandId.ToString());
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (groupId.HasValue)
        {
            await NormalizeCommandSortOrdersAsync(connection, transaction, groupId.Value, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReorderCommandsAsync(Guid groupId, IReadOnlyList<Guid> orderedCommandIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var allIds = await GetOrderedCommandIdsAsync(connection, transaction, groupId, cancellationToken);
        var finalOrder = BuildOrderedIds(allIds, orderedCommandIds);

        for (var index = 0; index < finalOrder.Count; index++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE command_profiles SET sort_order = $sortOrder WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", index);
            command.Parameters.AddWithValue("$id", finalOrder[index].ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MoveCommandAsync(Guid commandId, Guid targetGroupId, int targetIndex, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        Guid? sourceGroupId = null;

        await using (var getGroupCommand = connection.CreateCommand())
        {
            getGroupCommand.Transaction = transaction;
            getGroupCommand.CommandText = "SELECT group_id FROM command_profiles WHERE id = $id LIMIT 1;";
            getGroupCommand.Parameters.AddWithValue("$id", commandId.ToString());
            var result = await getGroupCommand.ExecuteScalarAsync(cancellationToken);

            if (result is string groupIdText)
            {
                sourceGroupId = Guid.Parse(groupIdText);
            }
        }

        if (!sourceGroupId.HasValue)
        {
            throw new InvalidOperationException("未找到要移动的命令。");
        }

        var sourceIds = await GetOrderedCommandIdsAsync(connection, transaction, sourceGroupId.Value, cancellationToken);
        sourceIds.Remove(commandId);

        var targetIds = sourceGroupId == targetGroupId
            ? sourceIds
            : await GetOrderedCommandIdsAsync(connection, transaction, targetGroupId, cancellationToken);

        if (sourceGroupId != targetGroupId)
        {
            targetIds.Remove(commandId);
        }

        var insertIndex = targetIndex < 0
            ? 0
            : targetIndex > targetIds.Count
                ? targetIds.Count
                : targetIndex;
        targetIds.Insert(insertIndex, commandId);

        await using (var updateGroupCommand = connection.CreateCommand())
        {
            updateGroupCommand.Transaction = transaction;
            updateGroupCommand.CommandText = "UPDATE command_profiles SET group_id = $groupId WHERE id = $id;";
            updateGroupCommand.Parameters.AddWithValue("$groupId", targetGroupId.ToString());
            updateGroupCommand.Parameters.AddWithValue("$id", commandId.ToString());
            await updateGroupCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await ApplyCommandSortOrderAsync(connection, transaction, targetGroupId, targetIds, cancellationToken);

        if (sourceGroupId != targetGroupId)
        {
            await ApplyCommandSortOrderAsync(connection, transaction, sourceGroupId.Value, sourceIds, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionRecord>> GetExecutionRecordsAsync(Guid? commandId, int take, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = commandId.HasValue
            ? """
                SELECT id, command_profile_id, trigger_source, run_mode, status, started_at, ended_at, exit_code, summary, output_text
                FROM execution_records
                WHERE command_profile_id = $commandId
                ORDER BY started_at DESC
                LIMIT $take;
                """
            : """
                SELECT id, command_profile_id, trigger_source, run_mode, status, started_at, ended_at, exit_code, summary, output_text
                FROM execution_records
                ORDER BY started_at DESC
                LIMIT $take;
                """;

        if (commandId.HasValue)
        {
            command.Parameters.AddWithValue("$commandId", commandId.Value.ToString());
        }

        command.Parameters.AddWithValue("$take", take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ExecutionRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ExecutionRecord
            {
                Id = Guid.Parse(reader.GetString(0)),
                CommandProfileId = Guid.Parse(reader.GetString(1)),
                TriggerSource = reader.GetString(2),
                RunMode = Enum.Parse<CommandRunMode>(reader.GetString(3), ignoreCase: true),
                Status = Enum.Parse<ExecutionStatus>(reader.GetString(4), ignoreCase: true),
                StartedAt = DateTimeOffset.Parse(reader.GetString(5)),
                EndedAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
                ExitCode = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Summary = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                OutputText = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
            });
        }

        return results;
    }

    public async Task SaveExecutionRecordAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO execution_records (
                id, command_profile_id, trigger_source, run_mode, status, started_at, ended_at,
                exit_code, summary, output_text)
            VALUES (
                $id, $commandProfileId, $triggerSource, $runMode, $status, $startedAt, $endedAt,
                $exitCode, $summary, $outputText)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                ended_at = excluded.ended_at,
                exit_code = excluded.exit_code,
                summary = excluded.summary,
                output_text = excluded.output_text;
            """;
        command.Parameters.AddWithValue("$id", record.Id.ToString());
        command.Parameters.AddWithValue("$commandProfileId", record.CommandProfileId.ToString());
        command.Parameters.AddWithValue("$triggerSource", record.TriggerSource);
        command.Parameters.AddWithValue("$runMode", record.RunMode.ToString());
        command.Parameters.AddWithValue("$status", record.Status.ToString());
        command.Parameters.AddWithValue("$startedAt", record.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$endedAt", (object?)record.EndedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$exitCode", (object?)record.ExitCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$summary", record.Summary);
        command.Parameters.AddWithValue("$outputText", record.OutputText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateExecutionRecordOutputAsync(Guid executionRecordId, string outputText, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE execution_records
            SET output_text = $outputText
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", executionRecordId.ToString());
        command.Parameters.AddWithValue("$outputText", outputText);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        await _databaseInitializer.EnsureInitializedAsync(cancellationToken);
        var connection = new SqliteConnection($"Data Source={_databaseInitializer.DatabasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static CommandProfile MapCommandProfile(SqliteDataReader reader)
    {
        return new CommandProfile
        {
            Id = Guid.Parse(reader.GetString(0)),
            GroupId = Guid.Parse(reader.GetString(1)),
            Name = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            WorkingDirectory = reader.IsDBNull(4) ? null : reader.GetString(4),
            ShellType = Enum.Parse<ShellType>(reader.GetString(5), ignoreCase: true),
            RunMode = Enum.Parse<CommandRunMode>(reader.GetString(6), ignoreCase: true),
            CommandText = reader.GetString(7),
            Arguments = Deserialize<List<string>>(reader.GetString(8)) ?? new List<string>(),
            EnvironmentVariables = Deserialize<List<EnvironmentVariableEntry>>(reader.GetString(9)) ?? new List<EnvironmentVariableEntry>(),
            RunAsAdministrator = reader.GetInt32(10) == 1,
            SortOrder = reader.GetInt32(11),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(12)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(13))
        };
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static async Task<List<Guid>> GetOrderedGroupIdsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM command_groups ORDER BY sort_order, created_at;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<Guid>();

        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }

        return ids;
    }

    private static async Task<List<Guid>> GetOrderedCommandIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id
            FROM command_profiles
            WHERE group_id = $groupId
            ORDER BY sort_order, created_at;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<Guid>();

        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }

        return ids;
    }

    private static List<Guid> BuildOrderedIds(IReadOnlyList<Guid> currentIds, IReadOnlyList<Guid> requestedIds)
    {
        var results = new List<Guid>(currentIds.Count);

        foreach (var requestedId in requestedIds)
        {
            if (currentIds.Contains(requestedId) && !results.Contains(requestedId))
            {
                results.Add(requestedId);
            }
        }

        foreach (var currentId in currentIds)
        {
            if (!results.Contains(currentId))
            {
                results.Add(currentId);
            }
        }

        return results;
    }

    private static async Task NormalizeGroupSortOrdersAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var ids = await GetOrderedGroupIdsAsync(connection, transaction, cancellationToken);

        for (var index = 0; index < ids.Count; index++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE command_groups SET sort_order = $sortOrder WHERE id = $id;";
            command.Parameters.AddWithValue("$sortOrder", index);
            command.Parameters.AddWithValue("$id", ids[index].ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task NormalizeCommandSortOrdersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var ids = await GetOrderedCommandIdsAsync(connection, transaction, groupId, cancellationToken);
        await ApplyCommandSortOrderAsync(connection, transaction, groupId, ids, cancellationToken);
    }

    private static async Task ApplyCommandSortOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid groupId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < orderedIds.Count; index++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE command_profiles
                SET group_id = $groupId, sort_order = $sortOrder
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$groupId", groupId.ToString());
            command.Parameters.AddWithValue("$sortOrder", index);
            command.Parameters.AddWithValue("$id", orderedIds[index].ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
