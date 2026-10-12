using System.Globalization;

namespace TMHue.Core.Services;

/// <summary>Uma sessão anterior que não chegou a encerrar de forma limpa.</summary>
public sealed record UncleanSession(string Version, DateTimeOffset StartedAt);

/// <summary>
/// Arquivo que existe apenas enquanto o app está em execução. Se ele ainda estiver lá no início
/// seguinte, a sessão anterior terminou sem passar pelo encerramento normal — travamento nativo,
/// processo finalizado ou queda de energia —, ou seja, justamente os casos em que o próprio app
/// não teve chance de registrar nada no log.
/// </summary>
public sealed class SessionMarker
{
    private readonly string _filePath;

    public SessionMarker(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Marca o início desta sessão e devolve a anterior, caso ela não tenha encerrado
    /// de forma limpa.</summary>
    public UncleanSession? Begin(string version, DateTimeOffset startedAt)
    {
        var previous = ReadPrevious();

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, $"{startedAt.ToString("O", CultureInfo.InvariantCulture)}|{version}");

        return previous;
    }

    /// <summary>Encerramento limpo: a próxima sessão não terá nada a relatar.</summary>
    public void End() => File.Delete(_filePath);

    private UncleanSession? ReadPrevious()
    {
        if (!File.Exists(_filePath)) return null;

        var parts = File.ReadAllText(_filePath).Split('|', 2);
        return parts.Length == 2
            && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startedAt)
            ? new UncleanSession(parts[1].Trim(), startedAt)
            // Marcador ilegível ainda prova que houve uma sessão interrompida.
            : new UncleanSession("?", File.GetLastWriteTime(_filePath));
    }
}
