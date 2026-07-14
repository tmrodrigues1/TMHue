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

O workflow então:

1. Publica o app (`dotnet publish`, x64, self-contained, multi-arquivo — exigência do Velopack).
2. Baixa a release anterior (`vpk download github`) para gerar o **pacote delta**.
3. Empacota com `vpk pack` usando o `packId` exclusivo `com.thiagorodrigues.TMHue`
   (não conflita com `%LocalAppData%\TMHue`, onde ficam configurações e histórico).
4. Cria a GitHub Release (nunca pré-release) anexando `Setup.exe`, pacote completo
   `.nupkg`, delta quando disponível e `releases.win.json` — o feed que o app consulta.

### Assinatura Authenticode

Opcional: se os segredos `CODESIGN_PFX_BASE64` e `CODESIGN_PFX_PASSWORD` estiverem
configurados, o workflow assina os artefatos. Sem eles, a release é publicada sem assinatura
(o Windows pode exibir o aviso do SmartScreen). O token do GitHub existe apenas no Actions;
o app cliente usa `GithubSource` sem credencial.

## Atualizações no app

- Checagem automática em segundo plano no máximo 1× a cada 24 h, com timeout curto, só
  depois da UI pronta; nunca bloqueia o uso nem degrada a captura.
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

Pré-requisito: `dotnet tool install -g vpk`. Artefatos em `artifacts\velopack\<versão>`.

A splash (`packaging/velopack/splash.gif`, ~316 KB, 10 FPS, loop) é gerada por
`dotnet run --project tools/SplashGenerator -c Release packaging/velopack/splash.gif`.

## Migração a partir do instalador antigo (Inno Setup)

A primeira migração é uma reinstalação manual documentada: baixar o novo `Setup.exe`,
instalar (o `packId` novo instala em `%LocalAppData%\com.thiagorodrigues.TMHue`) e
desinstalar a versão Inno antiga em **Configurações > Aplicativos**. Os dados do usuário
em `%LocalAppData%\TMHue` são preservados — nenhum instalador toca nessa pasta.
Os arquivos do Inno Setup permanecem em `packaging/inno` apenas como referência histórica
(`packaging/build-installer.ps1` era o script antigo).

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
