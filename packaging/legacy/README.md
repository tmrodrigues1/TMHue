# Empacotamentos legados — não usar para distribuição

O TMHue é distribuído **somente** com Velopack (`packaging/velopack/` e
`.github/workflows/release.yml`). Os arquivos desta pasta ficaram apenas como referência
histórica e **não são mantidos, testados nem assinados**:

| Arquivo | O que era |
|---|---|
| `inno/TMHue.iss` | Instalador Inno Setup anterior à v1.0.0 |
| `build-installer.ps1` | Script que compilava o instalador Inno Setup |
| `msix/Package.appxmanifest` | Rascunho de pacote MSIX, nunca publicado |

Avisos:

- `inno/TMHue.iss` declara `PrivilegesRequiredOverridesAllowed=dialog`, que permite ao usuário
  escolher instalação **elevada** (para todos os usuários). O instalador oficial é por usuário e
  nunca pede elevação; um instalador gerado daqui foge desse modelo.
- Os caminhos relativos dos scripts não foram ajustados após a mudança de pasta. Quem precisar
  recompilar algo daqui deve revisar os caminhos primeiro.
- Um instalador gerado daqui instala em outro local e não recebe atualizações automáticas.
