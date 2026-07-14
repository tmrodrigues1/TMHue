# Changelog

Todas as mudanças relevantes do TMHue serão registradas neste arquivo.

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
