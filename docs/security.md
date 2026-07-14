# Segurança e privacidade — TMHue

- Não requer privilégios administrativos (`app.manifest`: `requestedExecutionLevel level="asInvoker"`).
- Não instala serviços, não injeta código em outros processos, não usa driver de kernel.
- Não mantém hook global de teclado ou mouse: o overlay próprio recebe eventos apenas enquanto a captura está ativa.
- Não envia nenhum dado para a rede. Nenhuma dependência de rede está presente no projeto.
- Não realiza screenshots completos nem armazena imagens — apenas lê pixels pontuais via GDI (`GetPixel`) sob demanda.
- Logs (`%LocalAppData%\TMHue\Logs`) devem conter só informações técnicas — nunca pixels, capturas de tela ou conteúdo da tela.
- Conteúdo protegido por DRM ou superfícies especiais pode retornar preto/valor incorreto; isso é tratado como limitação do sistema, sem tentativa de contorno.

## Assinatura de código

- Teste local: certificado autoassinado somente em VM/perfil de teste, nunca distribuído.
- A release `v1.0.0` não possui assinatura Authenticode. O Windows pode emitir alertas ao abrir
  o instalador; baixe apenas da página oficial de Releases e confira o SHA-256 publicado.
- Releases futuras devem usar certificado de assinatura de código de produção, guardado fora do
  repositório e aplicado com timestamp ao executável, instalador e desinstalador.
