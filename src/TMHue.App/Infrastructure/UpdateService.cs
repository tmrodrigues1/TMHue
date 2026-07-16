using System.IO;
using System.Windows.Threading;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using Velopack;
using Velopack.Sources;

namespace TMHue.App.Infrastructure;

/// <summary>Resultado de uma verificação de atualização disparada manualmente.</summary>
public enum UpdateCheckResult
{
    UpToDate,
    UpdateAvailable,
    NotInstalled,
    Failed,

    /// <summary>Verificação manual recusada: ainda não passaram 2 horas desde a última.</summary>
    Throttled
}

/// <summary>
/// Verifica e aplica atualizações via Velopack usando as GitHub Releases públicas do
/// repositório (sem token no cliente; apenas releases estáveis, nunca pré-releases).
/// Todas as falhas são registradas e silenciosas: o app nunca fecha nem degrada a captura
/// de cores por causa de rede, checksum ou concorrência — a versão atual permanece ativa
/// e uma nova tentativa acontece na próxima checagem.
/// </summary>
public sealed class UpdateService
{
    private const string RepoUrl = "https://github.com/tmrodrigues1/TMHue";
    private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan ManualCheckInterval = TimeSpan.FromHours(2);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

    private readonly AppSettings _settings;
    private readonly ISettingsRepository _settingsRepository;
    private readonly Action<Exception> _logError;
    private readonly UpdateManager _manager;

    private UpdateInfo? _pendingUpdate;
    private bool _busy;

    public UpdateService(AppSettings settings, ISettingsRepository settingsRepository, Action<Exception> logError)
    {
        _settings = settings;
        _settingsRepository = settingsRepository;
        _logError = logError;
        _manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    }

    /// <summary>Nova versão encontrada; o argumento é a versão alvo (ex.: "1.2.0").
    /// Disparado no dispatcher da UI.</summary>
    public event EventHandler<string>? UpdateAvailable;

    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>Versão da atualização pendente encontrada pela última checagem (ex.: "1.2.0"),
    /// ou null se não há atualização aguardando consentimento do usuário.</summary>
    public string? PendingVersion => _pendingUpdate?.TargetFullRelease.Version.ToString();

    public string CurrentVersionDisplay =>
        _manager.IsInstalled && _manager.CurrentVersion is not null
            ? _manager.CurrentVersion.ToString()
            : (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");

    /// <summary>Checagem automática em segundo plano: respeita o intervalo de 24 h, tem
    /// timeout curto e nunca lança. Chamar depois que a UI já estiver disponível.</summary>
    public void CheckInBackground()
    {
        if (!_manager.IsInstalled)
            return;

        var last = _settings.LastUpdateCheckUtc;
        if (last is not null && DateTime.UtcNow - last < AutoCheckInterval)
            return;

        _ = CheckAsync(manual: false);
    }

    /// <summary>Tempo restante até a próxima verificação manual permitida, ou null quando o
    /// botão já pode ser usado. O carimbo é gravado apenas em verificações concluídas com
    /// sucesso: uma falha de rede não pune o usuário com 2 horas de espera.</summary>
    public TimeSpan? ManualCheckCooldownRemaining
    {
        get
        {
            var last = _settings.LastManualUpdateCheckUtc;
            if (last is null) return null;
            var remaining = ManualCheckInterval - (DateTime.UtcNow - last.Value);
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    /// <summary>Verificação manual (Configurações). Ignora o intervalo de 24 h da checagem
    /// automática, mas respeita o intervalo mínimo de 2 h entre verificações manuais.</summary>
    public async Task<UpdateCheckResult> CheckManuallyAsync()
    {
        if (!_manager.IsInstalled)
            return UpdateCheckResult.NotInstalled;

        if (ManualCheckCooldownRemaining is not null)
            return _pendingUpdate is null ? UpdateCheckResult.Throttled : UpdateCheckResult.UpdateAvailable;

        var result = await CheckAsync(manual: true);

        if (result is UpdateCheckResult.UpToDate or UpdateCheckResult.UpdateAvailable)
        {
            _settings.LastManualUpdateCheckUtc = DateTime.UtcNow;
            TryPersistSettings();
        }

        return result;
    }

    private async Task<UpdateCheckResult> CheckAsync(bool manual)
    {
        if (_busy)
            return _pendingUpdate is null ? UpdateCheckResult.Failed : UpdateCheckResult.UpdateAvailable;

        _busy = true;
        try
        {
            // CheckForUpdatesAsync não aceita CancellationToken; WaitAsync impõe o timeout
            // curto para a checagem nunca segurar nada por muito tempo.
            var info = await _manager.CheckForUpdatesAsync().WaitAsync(CheckTimeout).ConfigureAwait(false);

            _settings.LastUpdateCheckUtc = DateTime.UtcNow;
            TryPersistSettings();

            if (info is null)
                return UpdateCheckResult.UpToDate;

            _pendingUpdate = info;
            var version = info.TargetFullRelease.Version.ToString();
            await RunOnUiThreadAsync(() => UpdateAvailable?.Invoke(this, version)).ConfigureAwait(false);
            return UpdateCheckResult.UpdateAvailable;
        }
        catch (Exception ex)
        {
            _logError(new IOException($"Falha ao verificar atualizações ({(manual ? "manual" : "automática")}).", ex));
            return UpdateCheckResult.Failed;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Baixa a atualização pendente (validação de integridade fica a cargo do
    /// Velopack) e, só após concluir, aplica e reinicia o app. Retorna false em falha —
    /// a versão atual continua rodando e uma nova tentativa fica disponível.</summary>
    public async Task<bool> DownloadAndApplyAsync(Action<int>? progress = null)
    {
        var update = _pendingUpdate;
        if (update is null)
            return false;

        try
        {
            await _manager.DownloadUpdatesAsync(update, progress).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => _manager.ApplyUpdatesAndRestart(update)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logError(new IOException("Falha ao baixar/aplicar atualização; a versão atual foi mantida.", ex));
            // Permite tentar de novo do zero na próxima checagem.
            _pendingUpdate = update;
            return false;
        }
    }

    private void TryPersistSettings()
    {
        try
        {
            _settingsRepository.Save(_settings);
        }
        catch (Exception ex)
        {
            _logError(ex);
        }
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }
}
