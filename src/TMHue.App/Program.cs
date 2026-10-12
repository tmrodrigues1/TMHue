using TMHue.App.Infrastructure;
using Velopack;

namespace TMHue.App;

/// <summary>
/// Entry point manual (em vez do Main gerado pelo App.xaml) para que os hooks do Velopack
/// rodem antes de qualquer código WPF. Em instalação/atualização/desinstalação o Run()
/// executa o hook apropriado e encerra o processo; no uso normal, retorna imediatamente.
/// </summary>
public static class Program
{
    // O mesmo --packId usado em packaging/velopack/build-velopack.ps1 e no workflow de release.
    private const string PackageId = "com.thiagorodrigues.TMHue";

    [STAThread]
    public static void Main(string[] args)
    {
        // Antes de qualquer outra coisa: uma falha nos hooks do Velopack ou na criação da
        // aplicação WPF também precisa chegar ao log.
        ErrorReporter.RegisterProcessHandlers();

        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => CleanUpUserData())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    /// <summary>O desinstalador do Velopack remove o que ele mesmo criou: a pasta de instalação
    /// (%LocalAppData%\com.thiagorodrigues.TMHue), os atalhos, a pasta temporária e a entrada em
    /// "Aplicativos instalados". Todo o resto que o app deixa na máquina — settings.json,
    /// history.json e logs em %LocalAppData%\TMHue, o valor "TMHue" em HKCU\...\Run, a aprovação
    /// de inicialização, o registro do ícone da bandeja e os logs que o próprio Velopack grava
    /// fora da pasta de instalação — sai por aqui, antes de os arquivos
    /// serem apagados. O Velopack encerra o app em execução antes de chamar este hook.</summary>
    private static void CleanUpUserData()
    {
        // Este processo acabou de apagar a pasta de logs; registrar algo agora a recriaria.
        ErrorReporter.Disable();

        // Em ...\com.thiagorodrigues.TMHue\current\TMHue.exe, a raiz da instalação é o pai.
        var installRoot = System.IO.Path.GetDirectoryName(
            AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        TMHue.Windows.Persistence.UninstallCleanup.Run(installRoot, PackageId);
    }
}
