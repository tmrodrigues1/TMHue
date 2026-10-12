# Processo de release — TMHue

O TMHue é distribuído com **Velopack**: `Setup.exe` one-click por usuário (sem UAC), com
splash animada "Color Scan" e abertura automática ao concluir. As atualizações são
descobertas pelo próprio app via GitHub Releases do repositório
[`tmrodrigues1/TMHue`](https://github.com/tmrodrigues1/TMHue/releases).

## Release oficial (GitHub Actions)

O workflow `.github/workflows/release.yml` dispara ao enviar uma tag `vX.Y.Z`:

```powershell
git tag -a v1.0.0 -m "TMHue 1.0.0"
git push origin v1.0.0
```

Antes de criar a tag, o `CHANGELOG.md` precisa ter a seção `## [X.Y.Z]` da versão: as notas
da release são extraídas dela, e o workflow falha logo no início se ela não existir.

O workflow então:

1. Valida a tag (`vX.Y.Z`) e extrai as notas do `CHANGELOG.md` — antes de compilar.
2. Restaura os pacotes em modo travado (`dotnet restore --locked-mode`) e roda os testes
   (`dotnet test TMHue.sln -c Release`). Qualquer falha cancela a release.
3. Publica o app (`dotnet publish`, x64, self-contained, multi-arquivo — exigência do Velopack).
4. Instala a CLI `vpk` na **mesma versão** do pacote `Velopack` do `TMHue.App.csproj` e baixa
   a release anterior (`vpk download github`) para gerar o **pacote delta**.
5. Empacota com `vpk pack` usando o `packId` exclusivo `com.thiagorodrigues.TMHue`
   (não conflita com `%LocalAppData%\TMHue`, onde ficam configurações e histórico).
6. Prepara a GitHub Release em rascunho (nunca pré-release) anexando o instalador
   `TMHUE_vXYZ.exe` (por exemplo, `TMHUE_v130.exe`), pacote completo
   `.nupkg`, delta quando disponível e `releases.win.json` — o feed que o app consulta.
7. Anexa `SHA256SUMS.txt` (SHA-256 de cada arquivo da release), grava as notas e publica.

As actions de terceiros são fixadas pelo SHA do commit, com a versão em comentário; ao
atualizá-las, troque os dois.

### Assinatura Authenticode

Opcional: se os segredos `CODESIGN_PFX_BASE64` e `CODESIGN_PFX_PASSWORD` estiverem
configurados, o workflow assina os artefatos. Sem eles, a release é publicada sem assinatura
(o Windows pode exibir o aviso do SmartScreen). **O TMHue ainda não tem certificado: as
releases saem sem assinatura.**

Vale saber o que isso significa para a atualização automática: o feed do Velopack não é
assinado pelo autor (o checksum vem da mesma release que o pacote). Sem Authenticode, a única
garantia de autoria é o controle da conta e do repositório no GitHub — por isso as proteções
abaixo importam mais enquanto não houver certificado.

O token do GitHub existe apenas no Actions; o app cliente usa `GithubSource` sem credencial.

### Proteções a configurar no GitHub (manuais, uma única vez)

Estas configurações ficam fora do repositório e não podem ser aplicadas por commit:

1. **Environment `release`** (*Settings → Environments*): o job já roda nele. Adicione
   *Required reviewers*, para que nenhuma tag publique sem aprovação. Quando houver
   certificado, guarde `CODESIGN_PFX_BASE64` / `CODESIGN_PFX_PASSWORD` como segredos desse
   environment, não do repositório.
2. **Proteção de tags** (*Settings → Rules → Rulesets*): restrinja a criação de tags `v*` a
   quem pode lançar versões.
3. **Proteção da branch `main`**: exigir pull request e o check `ci` aprovado.
4. Quando for adotar assinatura, avaliar **Azure Trusted Signing** (ou certificado OV/EV em
   HSM) no lugar de um PFX exportável guardado como segredo.

### Pacotes NuGet e builds reproduzíveis

Cada projeto versiona um `packages.lock.json` (ativado em `Directory.Build.props`), e o CI
restaura em modo travado. Ao adicionar ou atualizar um pacote, rode `dotnet restore` e faça
commit dos locks alterados junto com o `.csproj`; sem isso o CI e a release falham.

O workflow `ci` (push e PR para a `main`) compila, testa e roda
`dotnet list package --vulnerable --include-transitive`.

Ao atualizar o pacote `Velopack`, rode antes o teste de instalação e atualização
(`packaging/velopack/test-installer.ps1` e a cadeia `v1 → v2` do checklist abaixo): é a
biblioteca que aplica as atualizações nas máquinas dos usuários, e a CLI do workflow acompanha
a versão dela automaticamente.

### Runtime .NET embutido

O publish é self-contained: o runtime vai dentro do pacote e as correções de segurança dele só
chegam aos usuários em uma nova release do TMHue. O workflow agendado `runtime-refresh` abre
uma issue no dia 15 de cada mês (depois do Patch Tuesday) lembrando de recompilar e publicar.

Alternativa não adotada: publicar framework-dependent e empacotar com
`vpk pack --framework net10.0-x64-desktop`. O runtime compartilhado passaria a ser atualizado
pelo Microsoft Update e a instalação cairia para poucos MB; em troca, o primeiro install baixa
o runtime quando a máquina não o tem.

## Atualizações no app

- Checagem automática em segundo plano no máximo 1× a cada 24 h, com timeout curto, só
  depois da UI pronta; nunca bloqueia o uso nem degrada a captura. Pode ser desligada em
  **Configurações → "Verificar atualizações automaticamente"**.
- Nova versão → toast com a versão e botão **Atualizar**; nada é baixado sem clique.
- Ao clicar: download em segundo plano ("Baixando atualização…"), validação de integridade
  pelo Velopack e reinício somente após o download terminar.
- Falhas (rede/checksum/concorrência) mantêm a versão atual, vão para
  `%LocalAppData%\TMHue\Logs\errors.log` e permitem nova tentativa.
- Em **Configurações → Atualizações** há o disparo manual "Verificar atualizações".
- O app consulta apenas releases publicadas, nunca pré-releases.

## Build local (testes)

```powershell
# Instalador local sem assinatura
.\build-installer.ps1 -Version 1.0.0

# Teste visual da splash: payload inflado para o Setup.exe ficar visível por mais tempo.
# NUNCA distribuir esse build.
.\build-installer.ps1 -Version 1.0.0 -SplashTestPadMB 300
```

Pré-requisito: `dotnet tool install -g vpk --version <versão do pacote Velopack no .csproj>`.
Artefatos em `artifacts\velopack\<versão>`.

A splash (`packaging/velopack/splash.gif`, ~316 KB, 10 FPS, loop) é gerada por
`dotnet run --project tools/SplashGenerator -c Release packaging/velopack/splash.gif`.

## Migração a partir do instalador antigo (Inno Setup)

A primeira migração é uma reinstalação manual documentada: baixar o novo `Setup.exe`,
instalar (o `packId` novo instala em `%LocalAppData%\com.thiagorodrigues.TMHue`) e
desinstalar a versão Inno antiga em **Configurações > Aplicativos**. Os dados do usuário
em `%LocalAppData%\TMHue` são preservados durante a instalação e as atualizações;
a desinstalação remove os dados do aplicativo.
Os arquivos do Inno Setup e o rascunho de MSIX permanecem em `packaging/legacy` apenas como
referência histórica; não são mantidos nem devem ser usados para distribuição (ver
`packaging/legacy/README.md`).

## Checklist de aceite

Ver critérios completos em `docs/test-matrix.md`. Pontos críticos:

- Instala sem elevação (sem UAC), com splash e barra de progresso real do Velopack.
- Abre o TMHue automaticamente ao final; atalhos criados; desinstalação limpa.
- Cadeia completa `v1 → GitHub Release v2 → notificação → Atualizar → reinício`,
  incluindo delta e fallback para o pacote completo.
- Inicializa sem elevar privilégios; nenhuma checagem de update bloqueia o startup.
- Ícone de tray criado corretamente; atalho global funciona sem a janela aberta.
- Cursor restaurado em todo caminho de saída (Captured/Cancelled/Failed).
- Histórico e configurações persistem entre execuções e entre atualizações.
