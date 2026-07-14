# TMHue

<p>
  <img src="assets/logo_tmrbrush.png" width="138" height="138" alt="Logo do TMHue">
</p>

TMHue é um conta-gotas de cores para Windows. Capture, copie e guarde cores sem interromper o seu trabalho.

[Baixar o TMHue para Windows](https://github.com/tmrodrigues1/TMHue/releases/latest)

> [!WARNING]
> As releases ainda não possuem assinatura Authenticode. O Windows pode exibir um aviso do SmartScreen ao abrir o instalador. Baixe sempre pela página oficial de releases.

## Instalação

Baixe o `Setup.exe` da [release mais recente](https://github.com/tmrodrigues1/TMHue/releases/latest). A instalação é feita por usuário, sem UAC, e o aplicativo verifica atualizações em segundo plano.

## Funcionalidades

- **Capture uma cor sem trocar de janela:** use um atalho para ativar o conta-gotas e clique no ponto desejado da tela.
- **Veja melhor antes de escolher:** a lupa mostra a área próxima ao cursor para facilitar capturas precisas.
- **Cole a cor onde precisar:** o código da cor é copiado automaticamente, pronto para usar em um editor, documento ou projeto.
- **Não perca suas últimas escolhas:** o histórico guarda as cores mais recentes e permite fixar suas favoritas.
- **Deixe do seu jeito:** escolha tema claro ou escuro, personalize atalhos e defina se o aplicativo inicia com o Windows.
- **Funciona no seu espaço de trabalho:** suporta mais de um monitor e diferentes escalas de tela.
- **Confira acessibilidade:** compare duas cores com a validação de contraste WCAG.
- **Mantenha-se atualizado:** receba uma notificação quando uma nova versão estiver disponível.

## Requisitos

- Windows 10 versão 2004 (build 19041) ou posterior, 64 bits.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) para desenvolvimento.
- [Velopack CLI](https://docs.velopack.io/) para gerar o instalador local.

## Desenvolvimento

Clone o projeto e entre na pasta criada:

```powershell
git clone https://github.com/tmrodrigues1/TMHue.git
cd TMHue
```

Use o comando abaixo para baixar as dependências do projeto:

```powershell
dotnet restore
```

Use este comando para verificar se o aplicativo compila corretamente:

```powershell
dotnet build TMHue.sln -c Debug
```

Para abrir o TMHue em modo de desenvolvimento, execute:

```powershell
.\run.ps1
```

Para executar os testes e conferir as regras principais do aplicativo:

```powershell
dotnet test TMHue.sln -c Release
```

## Build e instalador Windows

Para criar o instalador local com Velopack, execute:

```powershell
.\build-installer.ps1 -Version 1.0.0
```

Os artefatos ficam em `artifacts\velopack\1.0.0`. Para publicar uma release oficial, envie uma tag `vX.Y.Z`; o GitHub Actions gera e publica os pacotes automaticamente. Veja o [processo de release](docs/release-process.md).

## Stack utilizada

- **C# e .NET 10** para a base do aplicativo.
- **WPF** para a interface do Windows.
- **MVVM** para manter a interface organizada e fácil de evoluir.
- **Velopack** para instalação e atualizações automáticas.
- **xUnit** para os testes automatizados.
- **GitHub Actions** para o pipeline de release.

## Desempenho e uso

O TMHue foi pensado para ficar disponível enquanto você trabalha, sem pesar no computador. Nos testes realizados, o uso de memória ficou abaixo de **80 MB**. Ele só lê a cor da tela enquanto a captura está ativa e não salva imagens ou capturas de tela.

## Organização do projeto

```text
src/TMHue.App/        Interface do aplicativo
src/TMHue.Core/       Regras e modelos de dados
src/TMHue.Windows/    Recursos específicos do Windows
tests/                Testes automatizados
packaging/            Arquivos do instalador
docs/                 Documentação do projeto
```

Veja também a [arquitetura](docs/architecture.md), as informações de [segurança e privacidade](docs/security.md) e o [processo de release](docs/release-process.md).

## Licença

Distribuído sob a [licença MIT](LICENSE).
