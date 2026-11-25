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

## Revisão de manutenção — 1.0.0
- Reparo/reinstalação no próprio cluster, sem mundo temporário.
- Editor literal Master/Caves com revisão e confirmação; detecção de alterações concorrentes.
- Consulta local de nomes em caches V1 e V2; logs do atualizador gravados em fluxo.
- Proteção do setup global contra registros externos ao plano.

## Revisão visual — 1.0.0
- Catálogo de mods por shard, filtro por nome/ID e prévia original Steam Workshop.
- Inclusão por ID/link e ativação/desativação sem editar Lua; preserva opções existentes.
- Primeiro mod em cluster sem modoverrides.lua, detecção adicional de caminhos e seleção direta de cluster.

## Revisão de diagnóstico — 1.0.0
- Painel somente leitura com gravidade, localização, problema e recomendação.
- Inspeção de configuração, caches, setup, processos, disco e sinais em logs; relatório TXT sem linhas brutas.
- Correção da detecção convencional C:\steamcmd e regressões de diagnóstico.

## Revisão de operações assistidas — 1.0.0
- Filtros de gravidade e busca no diagnóstico; atalhos para análise de reinstalação e catálogo visual.
- Bloqueios de segurança e seleção por caminho exato; sem reparo automático de portas, tokens ou saves.
- Comparação inconclusiva quando Lua não pode ser lido, estimativa de espaço para backup e exportação sem sobrescrita.

## Revisão de recuperação — 1.0.0
- Central de backups e recuperação verificada das configurações de mods sem reverter saves.
- Manifesto SHA-256 com vínculo ao cluster e à instalação; proteção contra backup divergente.
- Desativação integral explícita, confirmação reforçada e dispensa de downloads quando não há mods ativos.

## Revisão de atualização do dedicado — 1.0.0
- Atualizador SteamCMD integrado para instalações independentes, validação opcional, confirmação dupla, backup e proteção das configurações de mods.

## Revisão de interrupções — 1.0.0
- Manifesto persistente anterior às alterações; bloqueio de nova manutenção enquanto houver operação pendente.
- Recuperação assistida de configurações e caches afetados; rejeita divergências e preserva saves.
