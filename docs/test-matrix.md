# Matriz de testes — TMHue

## Automatizados

- **Unit** (`tests/TMHue.UnitTests`): formatação hex, parsing de hex, dedupe/limite do histórico, `HotkeyDefinition.ToString`.
- **Integration** (`tests/TMHue.IntegrationTests`): clipboard real (STA), `VirtualScreenBounds`, descarte de buffers inválidos, recorte, exclusão nativa e persistência dos modos.
- **Seleção** (`tests/TMHue.UnitTests`): releitura no clique, clique imediato, conteúdo alterado, falhas, bloqueio após confirmação, pixel exato e cor regional presente na amostra.
- **Geometria e navegação**: limites da lupa em cinco escalas, coordenadas negativas, monitores adjacentes, espaços vazios e tolerância por canal.
- **Cor e snapshot**: identidade ICC, estruturas nativas, RGB8 preservado, rejeição de HDR/metadados/perfil ausentes em sRGB, limite de memória e cancelamento antes da alocação.
- **Comparação na tela** (`tools/SamplingDiagnostics`): [fases 1–4](sampling-phases-1-4.md) e [fases 5–8](sampling-phases-5-8.md); 45 leituras, conteúdo alterado sob cursor parado, snapshot, clique nativo, cleanup e timings.

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
| Cor | Perfil sRGB e outros ICC; valores capturados e conversão SDR |
| Seleção | Mouse rápido, clique imediato, setas, Shift+setas, mira e tela congelada |
| Sessão | Local e acesso remoto |
| Clipboard | Histórico do Windows (Win+V) ligado e desligado |
| Inicialização | Manual e automática (com o Windows) |

## Critérios de aceite da v1.0

Ver seção 26 do plano de desenvolvimento original (`Plano de Desenvolvimento - TMHue.pdf`).
