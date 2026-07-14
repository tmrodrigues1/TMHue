# Arquitetura — TMHue

## Camadas

- **TMHue.Core** — modelos, interfaces e regras de negócio puras (sem dependência de Windows ou WPF). `ColorHistoryService`, `RgbColor`, `AppSettings`.
- **TMHue.Windows** — toda a integração nativa: P/Invoke (`Native/NativeMethods.cs`), leitura de pixel via GDI, clipboard, hotkey global, tray icon (NotifyIcon), startup (registro `Run`), persistência em JSON.
- **TMHue.App** — WPF/MVVM. Views, ViewModels, o bootstrapper (`App.xaml.cs`) que faz a composição via `Microsoft.Extensions.DependencyInjection`, e o `ColorPickerCoordinator`, que implementa a máquina de estados descrita no plano (Idle → Preparing → Picking → Captured/Cancelled/Failed → Idle).

## Fluxo de captura

1. `ColorPickerCoordinator.BeginCapture()` cria `PickerOverlayWindow` (transparente, topmost, cobre a tela virtual inteira em pixels físicos via `SetWindowPos`) e `PickerPreviewWindow` (lupa + hex/rgb, excluída de capturas via `WDA_EXCLUDEFROMCAPTURE`).
2. Movimento do mouse → `ScreenColorSampler.TryReadPixel`/`ReadRegion` (GDI `GetPixel`) → atualiza a preview.
3. Clique esquerdo → congela a cor, copia para o clipboard (`ClipboardService`, com retries), salva no histórico (`ColorHistoryService`, dedupe do último item, limite de 5), fecha overlay/preview, restaura estado `Idle`.
4. Esc / clique direito / perda de foco → cancela sem tocar em clipboard/histórico.

Todas as saídas passam por `Cleanup()` em `ColorPickerCoordinator`, garantindo que overlay, preview e assinatura de eventos sejam sempre liberados.

## Multi-monitor e DPI

- `app.manifest` declara `PerMonitorV2`.
- `VirtualScreenBounds.GetCurrent()` usa `SM_XVIRTUALSCREEN`/`SM_YVIRTUALSCREEN`/`SM_CXVIRTUALSCREEN`/`SM_CYVIRTUALSCREEN`, suportando coordenadas negativas.
- Overlay e preview são posicionados em pixels físicos via `SetWindowPos`, contornando o scaling por DIP do WPF em setups com DPI misto.

## Persistência

`%LocalAppData%\TMHue\settings.json` e `history.json`. Falha de parsing faz backup do arquivo corrompido (`.bak`) e recria o padrão — nunca bloqueia a inicialização.
