# Histórico

## 1.0.0 — revisão de compatibilidade

- Seleção de origem e destino em pastas diferentes; detecção de subpastas numéricas de contas.
- Pacotes autocontidos x64, x86 e ARM64; smoke tests x64/x86 na CI.
- Bloqueio de caminhos de instalação sobrepostos aos mundos e de mods sem identificação.
- Downloads por arquitetura e SHA-256 no site.

## 1.0.0 — revisão de segurança

- Verificação de modinfo.lua no layout V2 documentado; a pasta vazia não conta como download.
- Quarentena V1 limitada aos IDs afetados; bloqueio de outros caches UGC.
- Restauração de caches parciais e conferência do plano após backup.
- Testes de integração em clusters fictícios, incluindo falha simulada e save preservado.
- Link permanente da página de releases e melhorias no site.

## 1.0.0 — revisão candidata inicial

- Interface Windows, seleção de clusters e comparação por shard.
- Parser restrito de tabelas Lua, consulta opcional de metadados Workshop e dupla confirmação.
- Backup do cluster, quarentena, atualizador sequencial e tentativa de restauração.
- CI Windows, publicação candidata por GitHub Releases e site de download integrado.

Validação de uma instalação DST real ainda necessária.
