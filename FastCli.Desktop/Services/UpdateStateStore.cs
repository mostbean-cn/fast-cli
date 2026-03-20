using System.IO;
using System.Text.Json;

namespace FastCli.Desktop.Services;

public sealed class UpdateStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UpdateStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<UpdateStateSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new UpdateStateSnapshot();
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new UpdateStateSnapshot();
            }

            await using var stream = File.OpenRead(_filePath);
            var snapshot = await JsonSerializer.DeserializeAsync<UpdateStateSnapshot>(stream, JsonOptions, cancellationToken);
            return snapshot ?? new UpdateStateSnapshot();
        }
        catch
        {
            return new UpdateStateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(UpdateStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
