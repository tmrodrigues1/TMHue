# Precisão de captura — fases 5 a 8

Implementação em 11/10/2026. O padrão continua sendo o RGB capturado pelo GDI,
sem compensação dos canais. Pixel exato e preferências regionais existentes foram preservados.
Mira, tela congelada e conversão sRGB são opções em **Configurações → Opções avançadas de captura**.

## Atualização e navegação

- Prévia e lupa consomem a mesma `ScreenSample`, incluindo pixels inválidos, modo e origem.
- Captura ao vivo: até 30 atualizações/s em movimento; após 200 ms parado, até 10/s.
  O conteúdo sob o cursor parado continua sendo atualizado.
- Não há leituras simultâneas. Eventos de movimento guardam a posição mais recente;
  o timer lê no dispatcher. Teclado e leitura inicial atualizam imediatamente.
- A lupa só escreve o bitmap quando os pixels mudam. Formato, origem e posição são cacheados.
- Confirmação continua relendo a posição da mensagem nativa e encerrando a sessão
  antes de clipboard/histórico. Timer e callbacks são abortados em todas as saídas.
- Setas simples avançam um pixel físico. Shift+setas procura até 80 pixels,
  comparando cada pixel com o ponto inicial: diferença máxima por canal maior que
  a tolerância configurada. Padrão 2, intervalo 0–32; zero detecta qualquer diferença.
  Sem transição, avança até o limite pesquisado. Espaços entre monitores interrompem o avanço.
- A busca ao vivo usa uma única captura de faixa de até 81 pixels, evitando até
  81 chamadas síncronas a `GetPixel`. A tolerância usa os canais capturados antes da conversão ICC.

## Monitores, mira e tela congelada

Há uma sobreposição por monitor, com tamanho físico reconciliado com o DPI do HWND
e a área de hit-test do WPF. Cada janela valida a exclusão da captura. A lupa consulta
o monitor do pixel selecionado, atualiza seu tamanho ao trocar de DPI e fica nos limites
desse monitor. Pixels nos espaços entre telas continuam inválidos.

O pincel existente foi preservado: o cursor incorporado de 32×32 usa hotspot (1,31).
Uma mira opcional de centro vazado usa hotspot explícito (16,16); a célula selecionada
também está marcada na lupa. O teste nativo verifica a posição da mensagem de clique,
inclusive no monitor à esquerda com coordenadas negativas.

O modo congelado captura cada monitor antes de mostrar as sobreposições e apresenta
essa imagem durante a seleção. Mouse, teclado, lupa e confirmação consultam os mesmos
pixels do snapshot. Não acompanha conteúdo animado e não precisa de timer periódico.

Imagens ficam somente na memória. Há orçamento de 256 MiB, calculado antes da alocação
considerando bitmap GDI, buffer e bitmap WPF. Cancelamento da preparação é propagado
ao trabalho em segundo plano; gerações antigas não podem abrir ou fechar a seleção nova.
Ao sair, buffers gerenciados são limpos e referências/recursos nativos liberados.
Uma mudança da topologia de monitores cancela a seleção para evitar coordenadas antigas.

## Saída e gerenciamento de cores

**Valores capturados**, padrão: retorna os canais RGB de 8 bits lidos pelo GDI.
Não aplica offsets, correção automática, nem tentativa de recuperar a cor original do arquivo.
O histórico registra também uma descrição da origem; históricos antigos continuam carregando.

**Converter SDR para sRGB**, opcional: consulta o perfil ICC atual de cada monitor,
prepara a transformação Windows para o perfil sRGB padrão e usa intenção colorimétrica
relativa. A lupa e a seleção recebem a mesma amostra convertida. Perfis iguais usam
identidade; regiões entre monitores são convertidas por monitor antes da seleção regional.
Perfil ausente, transformação falha, HDR/WCG ativo ou modo desconhecido cancelam a
captura sRGB e descartam a amostra. Nunca se rotulam canais sem conversão como sRGB.

O estado de cor avançada é consultado por `QueryDisplayConfig` e
`DisplayConfigGetDeviceInfo`, com fallback para a estrutura anterior. Perfil e modo
são consultados ao abrir cada sessão; o app não altera HDR, calibração ou perfis do sistema.
O rótulo da lupa identifica valores capturados, conversão SDR ou limite HDR/WCG de 8 bits.

A cor do arquivo passa por decodificação, perfil da imagem, processamento do aplicativo,
composição e gerenciamento da tela. O valor renderizado não permite inverter genericamente
essas operações. A opção ICC SDR pressupõe que os canais capturados representam RGB do
dispositivo; não desfaz tone mapping nem processamento que já ocorreu no aplicativo.

### Avaliação do mecanismo HDR

A captura GDI atual entrega RGB8 e não preserva os dados necessários para HDR completo.
Converter esses bytes por ICC não recupera luminância, gamut ou precisão perdidos.
A Microsoft orienta usar um mecanismo adequado a cor avançada e tratamento explícito
ao converter para SDR: [HDR e captura](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range).

Para uma implementação HDR, a alternativa preferida é **Windows Graphics Capture com
frames FP16**, preservando scRGB linear até a conversão de saída. **DXGI Output5 / Desktop
Duplication** também permite formatos de maior precisão, com tratamento próprio de acesso
perdido, mudanças de monitor, rotação e sincronização de frames:
[DuplicateOutput1](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_5/nf-dxgi1_5-idxgioutput5-duplicateoutput1).

Esse backend exige recursos GPU, metadados de luminância e uma política verificável de
tone mapping para sRGB/HEX. Ele não foi ativado nesta implementação: não há validação
HDR neste ambiente. O caminho RGB8 aprovado foi mantido e sua limitação ficou explícita.
A transformação SDR usa a API documentada de
[perfis ICC](https://learn.microsoft.com/en-us/windows/win32/api/icm/nf-icm-createmultiprofiletransform).

## Evidências e desempenho

Registro: [sampling-phases-5-8-results.json](sampling-phases-5-8-results.json).
Ambiente: Windows 10.0.26300.0, dois monitores 1920×1080 a 100%, um deles à esquerda
(-1920,0), ambos SDR/8 bits e perfil sRGB. Esses dados foram consultados no sistema.

- As 45 comparações de sólido, borda, linha e gradiente em 1×1/5×5/11×11 passaram.
  **#3A10E0 permaneceu #3A10E0** antes, durante a sobreposição e sob a lupa.
- Exclusão, limites físicos, hit-test, posição do clique e limites da lupa passaram nos dois monitores.
- Alteração de azul para verde sob cursor parado atualizou a lupa e o valor juntos;
  a tela congelada continuou azul e confirmou o pixel azul.
- Bitmap estável não recebeu escritas repetidas. O timer parou após descarte/confirmação.
- Coordenador: cancelamento durante preparação, nova sessão após cancelamento,
  clique nativo com releitura, leitura inicial falha sem salvar/copiar e Esc sem janelas órfãs passaram.
  O teste usa somente suas próprias janelas e serviços simulados de clipboard/histórico/hotkeys.
- sRGB → sRGB preservou o azul. Fixture Adobe RGB → sRGB produziu #4107E5;
  `ColorConvertedBitmap` do WPF produziu #4106E5, diferença de 1 LSB.
  Os perfis reais dos monitores não foram alterados para esse teste.
- Geometria em 100%, 125%, 150%, 175% e 200% está coberta por testes puros.
  Isso não substitui a validação nativa de DPI misto.

O JSON registra 100 leituras por tamanho, mediana, P95 e máximo. A mediana observada
foi aproximadamente 7 ms e o P95 ficou entre 7,05 e 7,09 ms. A primeira atualização
completa levou 39,1 ms; as nove atualizações seguintes tiveram mediana de 6,2 ms.
Esses tempos incluem captura, seleção e escrita WPF; não medem apresentação final pelo compositor.
Os limites de 30/10 Hz reduzem carga e a confirmação permanece síncrona e independente do timer.

**61 testes unitários e 32 de integração passaram**, além do diagnóstico nativo.
Build sem erros; NU1900 indica que a consulta NuGet de vulnerabilidades não conseguiu
acessar api.nuget.org. Nenhum pacote foi acrescentado nem a consulta desativada.
Arquivos criados ou modificados nesta implementação têm menos de 400 linhas.

## Reproduzir

Na raiz do projeto:

```powershell
dotnet build .\src\TMHue.App\TMHue.App.csproj -c Debug -m:1 --no-restore
dotnet build TMHue.sln -m:1 --no-restore
dotnet test tests/TMHue.UnitTests/TMHue.UnitTests.csproj -p:Platform=x64 --no-build --no-restore -m:1
dotnet test tests/TMHue.IntegrationTests/TMHue.IntegrationTests.csproj -p:Platform=x64 --no-build --no-restore -m:1
dotnet build tools/SamplingDiagnostics/SamplingDiagnostics.csproj -m:1 --no-restore
dotnet tools/SamplingDiagnostics/bin/Debug/net10.0-windows/win-x64/SamplingDiagnostics.dll docs/sampling-phases-5-8-results.json
```

O executável rápido fica em `src/TMHue.App/bin/Debug/net10.0-windows/win-x64/TMHue.exe`.
O diagnóstico abre e fecha suas próprias janelas; não modifica o mouse do sistema,
perfis, configurações do usuário, clipboard ou histórico reais. Os testes de integração
de clipboard já existentes usam o clipboard real, conforme sua implementação anterior.

## Validação manual pendente

Antes de certificar as variações de hardware:

1. Comparar sólido #3A10E0, bordas e linhas finas em modo exato/regional,
   lupa ligada/desligada, movimentos rápidos, clique imediato e Esc/clique direito.
2. Conferir ponta do pincel e mira com escalas 100–200%, DPI misto e mudança entre
   monitores; testar posições negativas, monitores acima/abaixo e espaços vazios.
3. Abrir tela congelada em conteúdo animado e conferir lupa, navegação e confirmação;
   cancelar durante preparação e confirmar que nenhum conteúdo é persistido.
4. Em hardware HDR, repetir HDR ligado/desligado e perfis diferentes. Registrar
   perfil, modo, aplicativo e valor renderizado. Conferir aviso RGB8 e rejeição de sRGB em HDR.
5. Testar acesso remoto, alteração de topologia e falhas de captura sem reaproveitar amostra.

As fases 5/6/8 estão implementadas e verificadas nas condições disponíveis. A fase 7
tem conversão SDR explícita, identificação de limites e avaliação do backend HDR;
comparações HDR reais e preservação HDR em FP16 permanecem pendentes.
