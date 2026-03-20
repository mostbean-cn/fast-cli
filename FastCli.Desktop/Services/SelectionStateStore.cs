using System.Text.Json;
using System.IO;

namespace FastCli.Desktop.Services;

public sealed class SelectionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SelectionStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<SelectionStateSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SelectionStateSnapshot();
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new SelectionStateSnapshot();
            }

            await using var stream = File.OpenRead(_filePath);
            var snapshot = await JsonSerializer.DeserializeAsync<SelectionStateSnapshot>(stream, JsonOptions, cancellationToken);
            return snapshot ?? new SelectionStateSnapshot();
        }
        catch
        {
            return new SelectionStateSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(SelectionStateSnapshot snapshot, CancellationToken cancellationToken = default)
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
