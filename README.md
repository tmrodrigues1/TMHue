# TMHue

<p>
  <img src="assets/logo_tmrbrush.png" width="138" height="138" alt="Logo do TMHue">
</p>

TMHue é um conta-gotas de cores para Windows. Capture, copie e guarde cores sem interromper o seu trabalho.

> [!WARNING]
> As releases ainda não possuem assinatura Authenticode. O Windows pode exibir um aviso do SmartScreen ao abrir o instalador. Baixe sempre pela página oficial de releases.

[Baixar o TMHue para Windows](https://github.com/tmrodrigues1/TMHue/releases/latest)

## Instalação

Baixe o `TMHUE_v130.exe` da [release v1.3.0](https://github.com/tmrodrigues1/TMHue/releases/tag/v1.3.0). A instalação é feita por usuário, sem UAC, e já inclui o runtime .NET necessário.

Quem já usa o aplicativo pode atualizar pelas Configurações. Configurações e histórico são preservados nas atualizações. A checagem automática em segundo plano pode ser desativada; a verificação manual continua disponível.

Ao desinstalar, o TMHue remove seus arquivos, configurações, histórico, logs e registros de inicialização e da bandeja.

## Funcionalidades

- **Capture uma cor sem trocar de janela:** use um atalho para ativar o conta-gotas e clique no ponto desejado da tela.
- **Veja melhor antes de escolher:** a lupa mostra a área próxima ao cursor para facilitar capturas precisas.
- **Ajuste a captura:** escolha o pixel exato ou uma amostra de 5×5/11×11, ative o cursor de precisão e congele a tela para selecionar cores em conteúdo animado.
- **Converta para sRGB quando necessário:** use o perfil de cor do monitor para converter capturas SDR. Requer um perfil válido; a captura em HDR tem limitações de 8 bits por canal.
- **Cole a cor onde precisar:** o código da cor é copiado automaticamente, pronto para usar em um editor, documento ou projeto.
- **Não perca suas últimas escolhas:** o histórico guarda as cores mais recentes e permite fixar suas favoritas.
- **Deixe do seu jeito:** escolha tema claro ou escuro, personalize atalhos e defina se o aplicativo inicia com o Windows.
- **Funciona no seu espaço de trabalho:** suporta mais de um monitor e diferentes escalas de tela.
- **Confira acessibilidade:** compare duas cores com a validação de contraste WCAG.
- **Explore combinações:** gere harmonias a partir de uma cor e extraia paletas de imagens ou de uma região da tela.
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

Instale a CLI na mesma versão do pacote Velopack usado pelo aplicativo (atualmente `1.2.161`) e gere o instalador local:

```powershell
dotnet tool install -g vpk --version 1.2.161
.\build-installer.ps1 -Version 1.3.0
```

Os artefatos ficam em `artifacts\velopack\1.3.0`; o instalador local se chama `com.thiagorodrigues.TMHue-win-Setup.exe`. Nas releases oficiais, ele recebe o nome `TMHUE_vXYZ.exe` (por exemplo, `TMHUE_v130.exe`).

Para publicar uma release oficial, registre a versão no `CHANGELOG.md` e envie uma tag `vX.Y.Z`; o GitHub Actions testa, gera e publica os pacotes e o arquivo `SHA256SUMS.txt`. Veja o [processo de release](docs/release-process.md).

## Stack utilizada

- **C# e .NET 10** para a base do aplicativo.
- **WPF** para a interface do Windows.
- **MVVM** para manter a interface organizada e fácil de evoluir.
- **Velopack** para instalação e atualizações automáticas.
- **xUnit** para os testes automatizados.
- **GitHub Actions** para o pipeline de release.

## Desempenho e uso

O TMHue foi pensado para ficar disponível enquanto você trabalha, com redução do trabalho repetido durante a captura e liberação de memória após o uso das ferramentas. O consumo varia conforme as ferramentas abertas, as imagens e a resolução dos monitores; congelar a tela mantém uma cópia dos monitores em memória durante a seleção.

As capturas e imagens usadas no extrator de paletas são processadas localmente, sem gravar os pixels em disco nem enviá-los pela rede.

Falhas são registradas em `%LocalAppData%\TMHue\Logs\errors.log`, com rotação do arquivo para limitar seu tamanho. Veja os detalhes em [segurança e privacidade](docs/security.md).

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
