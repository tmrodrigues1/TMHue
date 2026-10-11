# Precisão de captura — fases 1 a 4

Validação em 11/10/2026, Windows 10.0.26300.0. Referência: azul `#3A10E0`
presente nos preenchimentos de `_about/exemplo.svg`.

## Comparação controlada

`tools/SamplingDiagnostics` exibe uma imagem RGB de 8 bits com superfície sólida,
borda azul/branca, linha azul de um pixel e gradiente. Cada ponto é lido em 1×1,
5×5 e 11×11: antes da sobreposição, com sobreposição e com sobreposição+lupa.
A lupa cobre deliberadamente parte do alvo para detectar sua própria imagem na leitura.

Registros: [antes](sampling-before.json), [exclusão isolada](sampling-exclusion.json)
e [depois](sampling-after.json). São 45 leituras por execução.

| Amostra | Antes de abrir a seleção | Seleção antiga | Seleção corrigida |
|---|---|---|---|
| Azul sólido, três tamanhos | #3A10E0 | #3A10DF | #3A10E0 |
| Gradiente, ponto RGB(100,50,205) | #6432CD | #6432CC | #6432CD |
| Ponto azul coberto pela lupa | #3A10E0 | #3A3A44 | #3A10E0 |
| Linha azul, pixel exato | #3A10E0 | #3A10DF | #3A10E0 |
| Linha azul, região 5×5/11×11 | #FFFFFF | #FEFEFE | #FFFFFF |

O escurecimento aparece no pixel central e na região igualmente: sua causa é a
composição do fundo preto com alpha 1 da sobreposição. A lupa sem exclusão aparece
na captura. Já o branco na linha fina regional resulta da interpretação da área:
o fundo ocupa a maior parte da amostra. Nenhuma correção aritmética dos canais é usada.

## Exclusão e compatibilidade

As duas janelas aplicam `WDA_EXCLUDEFROMCAPTURE` na criação do HWND. O retorno de
`SetWindowDisplayAffinity`, seu erro nativo e a confirmação via `GetWindowDisplayAffinity`
são verificados. Os dois retornos foram bem-sucedidos e a comparação confirmou a
exclusão na captura GDI deste ambiente. O fundo alpha 1 permanece para receber cliques;
Esc, clique direito e perda de foco continuam cancelando.

Em Windows anterior ao build 19041 ou se a API falhar, a captura é cancelada e uma
mensagem explica o problema. Não se usa a compatibilidade antiga que gera um retângulo
preto. A [documentação da Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)
descreve esses limites; um retorno bem-sucedido deve ser acompanhado da comparação
visual ao validar outros drivers/sessões.

Foi considerada a captura anterior à sobreposição: ela congelaria o conteúdo e
deixaria de representar alterações durante a seleção. Como a exclusão atual funcionou,
essa alternativa não foi ativada. O modo congelado continua reservado à fase 6.

## Confirmação e falhas

- Há uma leitura inicial ao abrir o conta-gotas.
- O clique usa as coordenadas da mensagem nativa, convertidas de cliente para tela;
  não consulta uma posição do cursor que pode ter avançado após o evento.
- Uma sessão relê a área clicada e se encerra antes de acessar clipboard/histórico.
- O callback pendente é abortado no clique e em todas as saídas.
- Falhas limpam pixels e máscara de validade, descartam a amostra e não salvam/copiam cor.
- A região é recortada contra os monitores reais. Pixels fora da tela ou nos espaços
  entre monitores aparecem quadriculados na lupa e não participam do cálculo.

## Modos de amostragem

Novas configurações usam **Pixel exato — 1×1**. Valores existentes 1, 5 e 11 são
preservados; valores inválidos são normalizados para 1. Mouse e teclado passam pela
mesma sessão e usam o mesmo modo. A área considerada é delimitada na lupa.

**Cor da região — 5×5/11×11** usa a mediana dos canais apenas como referência e
escolhe o pixel real mais próximo dela por distância RGB quadrática. Empates favorecem
o pixel mais próximo do centro; persistindo o empate, vence a primeira posição na
ordem linha/coluna. O resultado pertence à área válida capturada. Isso evita a cor
fictícia que uma mediana independente por canal poderia produzir.

A captura é limitada a 11×11; buffers GDI são reutilizados e descartados pelo contêiner
de serviços. A seleção regional custa O(n log n + n), com n ≤ 121. Nenhuma captura de
tela inteira ou imagem do desktop é persistida pelo app.

## Reproduzir

Na raiz do projeto:

```powershell
dotnet build tools/SamplingDiagnostics/SamplingDiagnostics.csproj -m:1
dotnet tools/SamplingDiagnostics/bin/Debug/net10.0-windows/win-x64/SamplingDiagnostics.dll docs/sampling-after.json
dotnet test tests/TMHue.UnitTests/TMHue.UnitTests.csproj -m:1
dotnet test tests/TMHue.IntegrationTests/TMHue.IntegrationTests.csproj -m:1
```

O diagnóstico abre e fecha suas próprias janelas de teste, sem controlar outros
aplicativos e sem alterar configurações, histórico ou clipboard. Os registros antigos
foram obtidos antes da correção; a ferramenta atual reproduz o comportamento corrigido.

## Limites e continuidade

Os testes cobrem confirmação imediata, mudança de posição/conteúdo, rejeição de leitura
inválida, bloqueio de prévias após confirmar, recorte, cor regional real, coordenadas
assinadas e preservação das preferências: **51 unitários e 18 de integração passaram**.
A compilação passou. O NuGet emitiu NU1900:
a consulta de vulnerabilidades não conseguiu acessar api.nuget.org; não foi desativada.

Ainda falta a validação manual abrangente de cliques rápidos, escalas/monitores,
acesso remoto, HDR e perfis. A amostra controlada verifica valores RGB de 8 bits deste
ambiente; o estado HDR e os perfis do monitor não foram certificados nesta etapa.
As fases 5 a 8 podem continuar sobre esta base, incluindo essas verificações.
