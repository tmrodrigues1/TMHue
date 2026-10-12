namespace TMHue.Core.Services;

/// <summary>
/// Log de erros em texto com tamanho limitado. Ao atingir o teto, o arquivo atual vira
/// "&lt;nome&gt;.1&lt;ext&gt;" (um único arquivo antigo é mantido) em vez de ser apagado. Entradas
/// idênticas consecutivas viram uma linha de contagem, para que um erro em laço não encha o disco.
/// </summary>
public sealed class ErrorLog
{
    public const long MaxFileBytes = 1024 * 1024;
    private const int MaxEntryCharacters = 16 * 1024;

    private readonly string _filePath;
    private readonly string _archivePath;
    private readonly object _gate = new();
    private string? _lastEntry;
    private int _repeatCount;

    public ErrorLog(string filePath)
    {
        _filePath = filePath;
        _archivePath = Path.Combine(
            Path.GetDirectoryName(filePath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(filePath)}.1{Path.GetExtension(filePath)}");
    }

    public void Append(Exception exception) => Append(exception.ToString(), DateTimeOffset.Now);

    public void Append(string entry, DateTimeOffset timestamp)
    {
        if (entry.Length > MaxEntryCharacters)
            entry = entry[..MaxEntryCharacters];

        lock (_gate)
        {
            if (entry == _lastEntry)
            {
                _repeatCount++;
                return;
            }

            WritePendingRepeats(timestamp);
            _lastEntry = entry;
            Write($"[{timestamp:yyyy-MM-dd HH:mm:ss}] {entry}{Environment.NewLine}");
        }
    }

    /// <summary>Grava a contagem de repetições ainda pendente; chamar ao encerrar o app.</summary>
    public void Flush()
    {
        lock (_gate)
            WritePendingRepeats(DateTimeOffset.Now);
    }

    private void WritePendingRepeats(DateTimeOffset timestamp)
    {
        if (_repeatCount == 0) return;

        var repeats = _repeatCount;
        _repeatCount = 0;
        Write($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Entrada anterior repetida {repeats} vez(es).{Environment.NewLine}");
    }

    private void Write(string text)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var file = new FileInfo(_filePath);
        if (file.Exists && file.Length + System.Text.Encoding.UTF8.GetByteCount(text) > MaxFileBytes)
            File.Move(_filePath, _archivePath, overwrite: true);

        File.AppendAllText(_filePath, text);
    }
}
