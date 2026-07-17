# Changelog

Todas as mudanças relevantes do TMHue serão registradas neste arquivo.

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
