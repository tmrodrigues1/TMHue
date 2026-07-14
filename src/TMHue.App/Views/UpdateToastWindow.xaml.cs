using System.Windows;
using TMHue.App.Infrastructure;

namespace TMHue.App.Views;

/// <summary>
/// Toast persistente (não auto-dispensa, ao contrário do CaptureToastWindow) anunciando uma
/// nova versão. O download só começa após o clique em "Atualizar"; durante o download o toast
/// mostra o progresso e, ao concluir, o Velopack aplica e reinicia o app.
/// </summary>
public partial class UpdateToastWindow : Window
{
    private readonly UpdateService _updateService;
    private bool _downloading;

    public UpdateToastWindow(UpdateService updateService, string version)
    {
        InitializeComponent();
        _updateService = updateService;
        MessageText.Text = $"TMHue {version} disponível";

        Loaded += (_, _) => PlaceAboveTaskbar();
    }

    private void PlaceAboveTaskbar()
    {
        const double gapAboveTaskbar = 4;
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;
        Top = SystemParameters.WorkArea.Bottom - ActualHeight - gapAboveTaskbar;
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_downloading) return;
        _downloading = true;

        UpdateButton.IsEnabled = false;
        DismissButton.IsEnabled = false;
        MessageText.Text = "Baixando atualização…";

        var ok = await _updateService.DownloadAndApplyAsync(percent =>
            Dispatcher.Invoke(() => MessageText.Text = $"Baixando atualização… {percent}%"));

        // Em sucesso o app reinicia e esta janela morre junto; só chegamos aqui em falha.
        if (!ok)
        {
            _downloading = false;
            MessageText.Text = "Falha ao atualizar. Tente novamente.";
            UpdateButton.IsEnabled = true;
            DismissButton.IsEnabled = true;
        }
    }

    private void OnDismissClick(object sender, RoutedEventArgs e) => Close();
}
