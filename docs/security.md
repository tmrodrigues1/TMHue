# Segurança e privacidade — TMHue

## Privilégios e superfície no sistema

- Não requer privilégios administrativos (`app.manifest`: `requestedExecutionLevel level="asInvoker"`).
- Não instala serviços, não injeta código em outros processos, não usa driver de kernel.
- Não mantém hook global de teclado ou mouse: o overlay próprio recebe eventos apenas enquanto a captura está ativa. Durante a captura, `Esc` é registrado como atalho global para cancelar e é liberado em toda saída (inclusive se a preparação da captura travar: há um limite de 5 s).
- Registro: escreve somente em `HKCU` — o valor `TMHue` em `...\CurrentVersion\Run` quando "Iniciar com o Windows" está ativo, e `EstimatedSize` na entrada de desinstalação do próprio app.
- Instância única: um Mutex por sessão e um named pipe `TMHue.IPC.<SID>.<sessão>`, restrito ao usuário atual nas duas pontas. O pipe aceita apenas três comandos fixos (abrir a janela, trazer para frente, iniciar captura), com mensagem limitada a 64 bytes e 2 s de espera.

## Rede

O TMHue usa a rede para **uma única finalidade: atualizações**, sempre contra as GitHub Releases públicas do repositório `tmrodrigues1/TMHue`, por HTTPS.

| Quando | O que acontece |
|---|---|
| Checagem automática | No máximo uma vez a cada 24 h, depois que a interface já abriu. Pode ser desligada em **Configurações → "Verificar atualizações automaticamente"**. |
| Botão "Verificar" em Configurações | Consulta sob demanda, no máximo uma vez a cada 2 h. |
| Botão "Atualizar" | Só então o pacote da nova versão é baixado. Nada é baixado sem esse clique. |

- O que o GitHub recebe: o endereço IP da máquina e o user-agent do cliente HTTP, como em qualquer acesso a uma página do GitHub. Nenhum identificador, telemetria, cor capturada ou conteúdo de tela é enviado.
- O cliente não carrega token de acesso; pré-releases nunca são consideradas.
- Com a checagem automática desligada e sem usar o botão "Verificar", o app não faz nenhuma conexão.

## Conteúdo da tela

- Captura normal: lê via GDI apenas um quadrado de até 11×11 pixels ao redor do cursor, sob demanda.
- **"Congelar a tela ao selecionar"** (opção avançada, desligada por padrão): copia a imagem de **todos os monitores** para a memória no início da captura. A cópia fica em um único buffer por monitor (4 bytes por pixel, com teto total de 256 MB), nunca vai para o disco, e é **zerada e liberada** em toda saída da captura — confirmada, cancelada ou com falha.
- **Extrair paleta**: captura a região da tela que o usuário seleciona (reduzida a no máximo 2048 px no maior lado) ou decodifica a imagem arrastada para a janela (limites: 50 MB e 100 megapixels). Imagem e paleta existem só em memória e são descartadas ao limpar ou fechar a janela.
- Nenhuma imagem ou pixel é gravado em disco nem enviado para fora da máquina.
- Conteúdo protegido por DRM ou superfícies especiais pode retornar preto/valor incorreto; isso é tratado como limitação do sistema, sem tentativa de contorno.

## Dados locais

Tudo fica em `%LocalAppData%\TMHue`:

- `settings.json` — preferências, atalhos e posição da janela.
- `history.json` — as 10 últimas cores capturadas (valor, horário e a descrição técnica da origem, ex.: "GDI · RGB 8-bit · SDR").
- `Logs\errors.log` e `Logs\errors.1.log` — erros inesperados, limitados a 1 MB cada. Contêm só informações técnicas (origem da falha, versão do app e do Windows, tipo da exceção, pilha de chamadas e, após um travamento, o registro que o próprio Windows fez dele no log de eventos) — nunca pixels, capturas de tela ou conteúdo da tela.
- `Logs\session.marker` — existe só enquanto o app roda; serve para detectar, no início seguinte, que a sessão anterior terminou de forma inesperada.

### Desinstalação

Desinstalar remove tudo o que o TMHue colocou na máquina:

- a pasta de instalação (`%LocalAppData%\com.thiagorodrigues.TMHue`), os atalhos da área de trabalho e do Menu Iniciar, a pasta temporária do instalador e a entrada em "Aplicativos instalados" — pelo desinstalador do Velopack;
- a pasta `%LocalAppData%\TMHue` inteira (configurações, histórico, logs, backups);
- em `HKCU`: o valor `TMHue` de `...\CurrentVersion\Run`, o de `...\Explorer\StartupApproved\Run` e o registro do ícone da bandeja em `Control Panel\NotifyIconSettings`;
- o log que o Velopack grava fora da pasta de instalação (`%LocalAppData%\velopack\velopack_com.thiagorodrigues.TMHue.log`). Como o próprio desinstalador escreve nele até o fim, a remoção acontece alguns segundos depois de ele fechar. O `velopack.log` e a pasta `velopack` também saem, a menos que outro aplicativo instalado com Velopack os use;
- despejos de memória `TMHue.exe.*.dmp` em `%LocalAppData%\CrashDumps`, que só existem em máquinas com despejo local de falhas habilitado.

O app em execução é encerrado antes da limpeza. Não são tocados os registros de uso que o próprio Windows mantém sobre qualquer programa executado (log de eventos, Prefetch, histórico de execução), que não pertencem ao app.

Os arquivos são lidos com limite de tamanho e validados após a leitura: valores inválidos voltam ao padrão e entradas corrompidas do histórico são descartadas.

## Assinatura de código e integridade das atualizações

- **As releases do TMHue não possuem assinatura Authenticode** — o projeto ainda não tem certificado de assinatura de código. O Windows pode emitir alertas ao abrir o instalador. O workflow de release assina automaticamente quando um certificado for configurado (ver `docs/release-process.md`).
- Baixe apenas da [página oficial de Releases](https://github.com/tmrodrigues1/TMHue/releases). A partir da próxima release, cada uma traz `SHA256SUMS.txt`; confira com:

  ```powershell
  (Get-FileHash .\com.thiagorodrigues.TMHue-win-Setup.exe -Algorithm SHA256).Hash
  ```

- Atualização automática: o Velopack valida o checksum de cada pacote contra o feed `releases.win.json`. Esse feed **não é assinado pelo autor** — vem da mesma GitHub Release que o pacote. O checksum garante integridade no transporte; sem assinatura Authenticode, a autenticidade depende só do controle da conta e do repositório no GitHub.
- Teste local: certificado autoassinado somente em VM/perfil de teste, nunca distribuído (`packaging/signing/README.md`).

## Runtime embutido

O app é publicado self-contained: o runtime .NET vai dentro do pacote e só é atualizado por uma nova release do TMHue. Um workflow agendado abre uma issue mensal lembrando de recompilar após as correções de segurança da Microsoft (`.github/workflows/runtime-refresh.yml`).
