using Velopack;

namespace TMHue.App;

/// <summary>
/// Entry point manual (em vez do Main gerado pelo App.xaml) para que os hooks do Velopack
/// rodem antes de qualquer código WPF. Em instalação/atualização/desinstalação o Run()
/// executa o hook apropriado e encerra o processo; no uso normal, retorna imediatamente.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => CleanUpUserData())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>O desinstalador do Velopack remove apenas a pasta de instalação
    /// (%LocalAppData%\com.thiagorodrigues.TMHue) e os atalhos. Os dados criados pelo app —
    /// settings.json, history.json e logs em %LocalAppData%\TMHue, além do valor "TMHue" em
    /// HKCU\...\Run quando "iniciar com o Windows" está ativo — sobreviveriam à desinstalação
    /// sem esta limpeza explícita. Best-effort: nunca pode falhar a desinstalação.</summary>
    private static void CleanUpUserData()
    {
        try
        {
            var dataFolder = TMHue.Windows.Persistence.AppPaths.RootFolder;
            if (System.IO.Directory.Exists(dataFolder))
                System.IO.Directory.Delete(dataFolder, recursive: true);
        }
        catch
        {
            // best-effort
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue("TMHue", throwOnMissingValue: false);
        }
        catch
        {
            // best-effort
        }
    }
}
