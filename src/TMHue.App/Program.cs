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
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
