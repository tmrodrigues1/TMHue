# Matriz de testes — TMHue

## Automatizados

- **Unit** (`tests/TMHue.UnitTests`): formatação hex, parsing de hex, dedupe/limite do histórico, `HotkeyDefinition.ToString`.
- **Integration** (`tests/TMHue.IntegrationTests`): clipboard real (STA), `VirtualScreenBounds` em ambiente Windows real.

## Manual — variações obrigatórias antes do release

| Cenário | Variações |
|---|---|
| Windows | Windows 11 atualizado |
| Monitores | Um, dois e três monitores |
| Posicionamento | Esquerda, direita, acima, abaixo |
| DPI | 100%, 125%, 150%, 175%, 200% |
| Resolução | Full HD, QHD, 4K |
| Tema | Claro, escuro, alto contraste |
| Aplicações-alvo | Navegador, Power BI, imagens, vídeos, Office, desktop |
| Exibição | HDR ligado e desligado |
| Sessão | Local e acesso remoto |
| Clipboard | Histórico do Windows (Win+V) ligado e desligado |
| Inicialização | Manual e automática (com o Windows) |

## Critérios de aceite da v1.0

Ver seção 26 do plano de desenvolvimento original (`Plano de Desenvolvimento - TMHue.pdf`).
