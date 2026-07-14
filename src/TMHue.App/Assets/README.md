# Assets pendentes

Estes binários não podem ser gerados por código e precisam ser produzidos separadamente (ex.: Figma/Illustrator + exportação):

- `tray-icon.ico` — ícone da system tray e do executável. Incluir 16×16 e 32×32 no mesmo `.ico`.
- `eyedropper.cur` — cursor personalizado em formato de conta-gotas, usado durante a captura.

Depois de adicionar `tray-icon.ico`, descomente a linha `<ApplicationIcon>` em `TMHue.App.csproj`.

Enquanto os assets não existirem:
- `TrayIconService` usa `SystemIcons.Application` como fallback.
- `CursorService` usa `Cursors.Cross` como fallback.

Nenhum dos dois quebra a build ou a execução — apenas o acabamento visual fica incompleto.
