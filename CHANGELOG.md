# Changelog

Todas as mudanças relevantes do TMHue serão registradas neste arquivo.

## [1.3.0] - 2026-10-11

### Novidades

- Nova opção "Verificar atualizações automaticamente" em Configurações: desligada, o TMHue não faz nenhuma conexão por conta própria.

### Melhorias

- Menor uso de memória: a captura com "Congelar a tela ao selecionar" guarda uma única cópia da tela por monitor em vez de três, e o app devolve memória ao sistema ao ir para a bandeja, ao fechar uma ferramenta e ao terminar uma captura.
- Captura mais leve: menos trabalho repetido a cada movimento do cursor, na lupa e na janela principal.
- Extrator de paletas: imagens arrastadas são carregadas sem travar a janela, com limite de 50 MB e 100 megapixels.
- Instalação menor: removidos os recursos de idiomas que o app não usa.
- Diagnóstico de falhas: todo erro vai para `%LocalAppData%\TMHue\Logs\errors.log` com a origem, a versão, o ambiente e a pilha de chamadas com arquivo e linha — inclusive exceções fora da interface, falhas na inicialização e travamentos nativos, que antes não deixavam registro.
- O log de erros agora é rotacionado em vez de apagado ao atingir o limite.

### Correções

- Corrigido um caso em que o app podia ocupar um núcleo inteiro de CPU parado na bandeja, quando outro processo ocupava o canal usado para detectar uma segunda instância.
- O app não fica mais preso na bandeja, sem atalhos, quando `settings.json` ou `history.json` estão corrompidos ou foram editados com valores inválidos.
- A janela principal não abre mais fora da tela depois que um monitor é desconectado.
- Corrigida uma condição de corrida que podia perder a gravação das configurações logo após a checagem de atualização.
- Se a preparação de uma captura travar, a tecla Esc é liberada após 5 segundos.
- Em caso de erros repetidos em sequência, o app avisa e encerra, em vez de continuar consumindo CPU e disco.
- Desinstalação mais completa: além dos dados e da inicialização com o Windows, remove a aprovação de inicialização e o registro do ícone da bandeja, e insiste quando um arquivo está temporariamente em uso.
- Uma falha durante a inicialização agora mostra um aviso com o caminho do log e encerra o app, em vez de deixá-lo em execução sem janela nem ícone na bandeja.

### Segurança

- Releases passam a publicar `SHA256SUMS.txt`; o workflow roda os testes antes de publicar e não interpola mais o nome da tag em scripts.
- Documentação de privacidade corrigida para descrever o uso real de rede (checagem de atualizações) e de captura de tela.

## [1.2.2] - 2026-07-17

### Correções

- Corrigida a abertura da janela principal em instalações do Windows sem as fontes opcionais Segoe UI Variable e Cascadia Mono.
- Quando a janela não puder ser renderizada, o app agora exibe um aviso e registra o erro, em vez de permanecer silencioso na bandeja.
- Corrigida a exibição do tamanho instalado em Configurações > Aplicativos.

## [1.2.1] - 2026-07-17

### Novidades

- Interface em dois idiomas: alterne entre Português (Brasil) e Inglês (US) diretamente nas configurações, sem reiniciar o aplicativo.
- Tema claro e escuro: escolha a aparência que melhor combina com seu ambiente e mantenha uma experiência visual mais confortável durante o uso.

### Melhorias

- Extrator de paletas mais completo: agora identifica até 15 cores dominantes, oferecendo uma leitura mais rica e representativa das imagens.
- Captura de região otimizada: grandes seleções são redimensionadas de forma inteligente durante a captura, reduzindo o consumo de memória sem comprometer a qualidade da paleta gerada.

## [1.0.0] - 2026-07-14

- Instalador migrado do Inno Setup para Velopack: one-click por usuário, sem UAC, splash
  animada "Color Scan" e abertura automática ao concluir.
- Atualizações automáticas via GitHub Releases: checagem em segundo plano (1× a cada 24 h),
  toast com botão "Atualizar", download só após clique e reinício apenas ao final.
- Nova ação "Verificar atualizações" em Configurações, com exibição da versão atual.
- Workflow de release por tag (`vX.Y.Z`) no GitHub Actions com pacote delta e assinatura
  Authenticode obrigatória por padrão.

## [1.0.0] - 2026-07-12

- Primeira release pública do TMHue para Windows.
- Conta-gotas com atalho global, lupa, cópia automática e histórico persistente.
- Instalador x64 por usuário, com atualização manual e desinstalação.
- Artefato distribuído sem assinatura Authenticode; o SHA-256 acompanha a release.
