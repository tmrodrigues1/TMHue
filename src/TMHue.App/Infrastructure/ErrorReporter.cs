using System.IO;
using System.Runtime.InteropServices;
using TMHue.Core.Services;
using TMHue.Windows.Persistence;
using TMHue.Windows.Reliability;

namespace TMHue.App.Infrastructure;

/// <summary>
/// Ponto único de registro de falhas em <c>%LocalAppData%\TMHue\Logs\errors.log</c>. Cada entrada
/// diz de onde a falha veio e se o app sobreviveu a ela, seguida da exceção completa (tipo,
/// mensagem, exceções internas e pilha com arquivo e linha). Cobre também o que o app não
/// consegue registrar na hora — travamento nativo, processo finalizado —, relatando-o no início
/// seguinte com o que o próprio Windows gravou sobre o encerramento.
/// Nenhum método aqui lança: registrar uma falha nunca pode causar outra.
/// </summary>
internal static class ErrorReporter
{
    public static string LogPath { get; } = Path.Combine(AppPaths.LogsFolder, "errors.log");

    private static readonly ErrorLog Log = new(LogPath);
    private static readonly SessionMarker Session = new(Path.Combine(AppPaths.LogsFolder, "session.marker"));
    private static readonly string Version =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    private static int _sessionHeaderWritten;
    private static int _sessionOpen;
    private static volatile bool _disabled;

    /// <summary>Nada mais é gravado a partir daqui. Usado pela desinstalação, que apaga a pasta
    /// de logs e não pode recriá-la.</summary>
    public static void Disable() => _disabled = true;

    /// <summary>Handlers do processo inteiro. Chamar como primeira instrução do <c>Main</c>, para
    /// cobrir também falhas anteriores à criação da aplicação WPF.</summary>
    public static void RegisterProcessHandlers()
    {
        // Exceção não tratada fora da thread da UI (pool, timers, threads próprias) ou antes de o
        // dispatcher existir: o runtime encerra o processo logo depois deste handler.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception
                ?? new InvalidOperationException($"Objeto lançado que não é uma exceção: {args.ExceptionObject}");
            Report("FATAL — exceção não tratada; o app foi encerrado", exception);
            Flush();
            // A causa já está no log; a próxima sessão não precisa relatar um encerramento inesperado.
            EndSession();
        };

        // Tarefa que falhou sem que ninguém observasse o resultado. Não derruba o app, mas indica
        // um erro que passaria despercebido.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Report("Tarefa em segundo plano (exceção não observada); o app continuou em execução", args.Exception);
            args.SetObserved();
        };

        // Só roda em encerramento normal (inclusive reinício por atualização); nunca em travamento.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Flush();
            EndSession();
        };
    }

    /// <summary>Registra uma falha. <paramref name="origin"/> descreve onde ela foi capturada e o
    /// que aconteceu com o app, por exemplo "Interface (thread da UI); o app continuou em execução".</summary>
    public static void Report(string origin, Exception exception) => Write($"[{origin}] {exception}");

    /// <summary>Registra um fato relevante para o diagnóstico que não é uma exceção.</summary>
    public static void Note(string origin, string message) => Write($"[{origin}] {message}");

    /// <summary>Abre a sessão desta instância e, se a anterior não encerrou de forma limpa,
    /// registra isso. Chamar somente na instância que obteve a posse da instância única.</summary>
    public static void BeginSession()
    {
        try
        {
            Interlocked.Exchange(ref _sessionOpen, 1);
            if (Session.Begin(Version, DateTimeOffset.Now) is not { } previous) return;

            Note("Sessão anterior", $"A sessão iniciada em {previous.StartedAt:yyyy-MM-dd HH:mm:ss} (versão {previous.Version}) "
                + "terminou sem encerramento limpo e sem registrar a causa: travamento nativo, processo finalizado "
                + "pelo Gerenciador de Tarefas ou desligamento forçado. O que o Windows registrou sobre o encerramento "
                + "vem logo abaixo, quando existir.");

            // A consulta ao log de eventos pode levar centenas de milissegundos: fora do startup.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var report in WindowsCrashReports.ReadSince(previous.StartedAt, "TMHue.exe"))
                    Note("Sessão anterior · Windows Event Log", report);
            });
        }
        catch
        {
            // diagnóstico nunca pode impedir o app de iniciar
        }
    }

    /// <summary>Grava contagens de repetição pendentes.</summary>
    public static void Flush()
    {
        if (_disabled) return;

        try
        {
            Log.Flush();
        }
        catch
        {
            // logging must never crash the caller
        }
    }

    private static void EndSession()
    {
        if (Interlocked.Exchange(ref _sessionOpen, 0) == 0) return;

        try
        {
            Session.End();
        }
        catch
        {
            // sem o marcador removido, o pior caso é um aviso a mais no próximo início
        }
    }

    private static void Write(string entry)
    {
        if (_disabled) return;

        try
        {
            // Uma vez por execução, antes da primeira entrada: o contexto necessário para
            // reproduzir o problema em outra máquina.
            if (Interlocked.Exchange(ref _sessionHeaderWritten, 1) == 0)
            {
                Log.Append($"[Ambiente] TMHue {Version} · {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}) · "
                    + $"{RuntimeInformation.FrameworkDescription} · idioma {LocalizationService.CurrentLanguage} · PID {Environment.ProcessId}",
                    DateTimeOffset.Now);
            }

            Log.Append(entry, DateTimeOffset.Now);
        }
        catch
        {
            // logging must never crash the caller
        }
    }
}
