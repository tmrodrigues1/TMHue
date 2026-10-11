# Arquitetura — TMHue

## Camadas

- **TMHue.Core** — modelos, interfaces e regras de negócio puras (sem dependência de Windows ou WPF). `ColorHistoryService`, `RgbColor`, `AppSettings`.
- **TMHue.Windows** — toda a integração nativa: P/Invoke (`Native/NativeMethods.cs`), leitura de pixel via GDI, clipboard, hotkey global, tray icon (NotifyIcon), startup (registro `Run`), persistência em JSON.
- **TMHue.App** — WPF/MVVM. Views, ViewModels, o bootstrapper (`App.xaml.cs`) que faz a composição via `Microsoft.Extensions.DependencyInjection`, e o `ColorPickerCoordinator`, que implementa a máquina de estados descrita no plano (Idle → Preparing → Picking → Captured/Cancelled/Failed → Idle).

## Fluxo de captura

1. `ColorPickerCoordinator.BeginCapture()` prepara metadados de cor, conversão SDR opcional e snapshot opcional em worker cancelável. Depois cria uma `PickerOverlayWindow` por monitor e a `MagnifierWindow`. Ambas verificam a exclusão via `WDA_EXCLUDEFROMCAPTURE`; falhas cancelam a seleção. Há uma leitura inicial. Gerações antigas são descartadas.
2. `PickerRefreshLoop` agrupa movimentos e limita a captura ao vivo a 30 Hz em movimento/10 Hz parado. `ColorSelectionSession` → `ColorAwareSampler` → GDI ou snapshot (até 11×11, com máscara de validade) → uma amostra para prévia e lupa. Pixel exato usa o centro; região usa um pixel real próximo da mediana dos canais. Mouse e teclado usam o mesmo modo; bitmap estável não é reescrito.
3. Clique esquerdo → coordenadas da mensagem nativa, releitura da área e encerramento da sessão → aborta atualização pendente, fecha janelas, copia para o clipboard (`ClipboardService`, com retries), salva no histórico (`ColorHistoryService`), restaura `Idle`. Falhas descartam a amostra e são sinalizadas.
4. Esc / clique direito / perda de foco → cancela sem tocar em clipboard/histórico.

Todas as saídas passam por `Cleanup()` em `ColorPickerCoordinator`, garantindo que overlay, preview e assinatura de eventos sejam sempre liberados.

## Multi-monitor e DPI

- `app.manifest` declara `PerMonitorV2`.
- `VirtualScreenBounds.GetCurrent()` usa `SM_XVIRTUALSCREEN`/`SM_YVIRTUALSCREEN`/`SM_CXVIRTUALSCREEN`/`SM_CYVIRTUALSCREEN`, suportando coordenadas negativas.
- `MonitorLayout` usa os monitores reais e interrompe navegação nos espaços vazios.
- Uma sobreposição por monitor reconcilia posição física e tamanho DIP do HWND. A lupa atualiza o tamanho ao mudar de DPI e fica dentro do monitor atual.

## Cor e snapshot

O padrão preserva RGB8 capturado. `ColorAwareSampler` acrescenta metadados e, por opção,
converte SDR via ICC para sRGB. HDR/WCG ou perfil desconhecido impedem essa conversão.
`FrozenScreenSampler` captura antes das sobreposições, limita memória a 256 MiB e descarta
imagens ao sair. Não grava imagens no disco. Limites HDR e evidências estão em
[fases 5–8](sampling-phases-5-8.md).

## Persistência

`%LocalAppData%\TMHue\settings.json` e `history.json`. Falha de parsing faz backup do arquivo corrompido (`.bak`) e recria o padrão — nunca bloqueia a inicialização.
