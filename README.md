# Gerenciador de Dedicado · 1.0.0

Aplicativo independente para Windows x64, x86 e ARM64 e servidores dedicados de Don't Starve Together. Projeto comunitário, não afiliado à Klei ou à Valve.

**Estado:** candidato a validação. Teste em um cluster descartável; não use em produção sem backup externo e teste real. A versão pública permanece 1.0.0; a revisão é identificada pelo commit e pelo número de build.

## Modos de manutenção
- **Reinstalar mods deste dedicado:** usa os próprios arquivos Master/Caves como referência; preserva as opções e não exige outro mundo.
- **Copiar configuração:** transfere as configurações de um mundo de referência para o dedicado.
- **Editar configurações e reinstalar:** editor Lua literal por shard, com validação sem executar Lua. Adicione entradas Workshop, altere opções ou desative mods. A análise e duas confirmações precedem qualquer escrita.
- A desativação integral exige edição explícita e confirmação reforçada. O programa bloqueia coleções Workshop, configurações globais com mods não relacionados e instalações compartilhadas. Logs do atualizador ficam em arquivos separados de saída e erro no backup.
- A conexão Steam/Workshop continua necessária para baixar mods; testar a entrada no mundo após manutenção continua indispensável.

## Uso
1. Escolha o próprio dedicado para gerenciar ou reparar mods. Um mundo temporário é opcional e só é necessário quando você deseja copiar uma configuração de referência. Desligue o jogo, os shards e qualquer reinício automático do servidor.
2. Abra o aplicativo. Confira as pastas de origem, destino e instalação do dedicado (subpastas numéricas de contas são pesquisadas); selecione os clusters de origem e destino (números inteiros existentes).
3. Analise o plano, consulte nomes/IDs e confira Master/Caves. Confirme a origem e, depois, digite RESET Cluster_N para o destino.
4. Somente em instalação exclusiva, sem UGC personalizado, autorize a manutenção. O programa copia o cluster inteiro para backup local, preserva configurações e coloca caches antigos dos IDs afetados e UGC do destino em quarentena. A presença de caches UGC de outros clusters bloqueia a operação. Atualiza os shards sequencialmente sem iniciar o mundo; verifique logs e teste o servidor antes de liberar jogadores.

O programa não encerra servidores em execução, não edita saves, não apaga o cache global do cliente Steam, não instala mods privados/localmente desenvolvidos, não atualiza o binário do jogo e não reinicia o mundo. Remover mods que criam entidades pode afetar o mundo na próxima inicialização. Se o download falhar, a aplicação tenta restaurar configurações e caches; o backup fica disponível. Não compartilhe backups (podem conter token).

## Validação
Os testes de integração usam diretórios temporários e simulam um download incompleto, restauração e sucesso, sem iniciar DST. Um servidor real com Steam Workshop continua necessário para homologação.

## Compatibilidade
.NET 10 autocontido: Windows 11 e edições do Windows 10 ainda suportadas pela Microsoft; o aplicativo não requer instalação separada do runtime. Windows 7/8 e Linux/macOS não são suportados. x64 e x86 são verificados em runner Windows x64; ARM64 é compilado, mas exige teste em hardware ARM. A disponibilidade do executável DST depende da plataforma; não prometemos servidor funcional em 32 bits/ARM. SteamCMD e caminhos personalizados de instalação podem ser selecionados manualmente; UGC personalizado continua bloqueado.

## Compilar
Requer SDK .NET 10. No Windows: `dotnet run --project src/App`. Testes: `dotnet run --project tests/CoreTests`. Publicação: `dotnet publish src/App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`.

## Distribuição
[Ações e builds](https://github.com/efac000/software-dedicado-/actions) · [Releases](https://github.com/efac000/software-dedicado-/releases) · [Site (após habilitar Pages)](https://efac000.github.io/software-dedicado-/).

O workflow CI executa testes e compila; a publicação de uma revisão candidata ocorre em alterações do aplicativo na branch principal, após repetir testes e compilação; também pode ser acionada manualmente no workflow Release. O site consulta releases via API e só habilita o download quando encontra um executável publicado. Não há atualização silenciosa. O executável não possui assinatura Authenticode nesta etapa; confirme o SHA-256 junto ao release.

## Referências
- [Klei: opções de linha de comando](https://support.klei.com/hc/en-us/articles/360029556192-Dedicated-Server-Command-Line-Options-Guide)
- [DST Wiki: dedicated servers / V1 e V2](https://dontstarve.wiki.gg/wiki/Guides/Don%E2%80%99t_Starve_Together_Dedicated_Servers)
- [Steam Workshop API](https://partner.steamgames.com/doc/webapi/ISteamRemoteStorage)
- [Microsoft: single-file](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)

### Gerenciador visual
Escolha o modo **Gerenciar mods visualmente**, selecione o dedicado e analise. O catálogo permite incluir mods por ID ou URL HTTPS oficial da Workshop, ativar/desativar por shard preservando as opções, filtrar pelo nome/ID e consultar imagens originais da Workshop quando disponíveis. O editor Lua avançado continua opcional. Um cluster existente sem modoverrides.lua pode receber os primeiros mods diretamente; não é necessário gerar um mundo de referência. Não substitui a criação inicial do próprio servidor. A manutenção exige revisão e duas confirmações. Imagens são consultadas de domínios Steam permitidos, com limites de tamanho e tempo. A detecção verifica Documentos/OneDrive e instalações Steam/SteamCMD convencionais; caminhos personalizados podem ser selecionados manualmente.

## Diagnóstico integrado
O botão Diagnóstico do dedicado inspeciona o cluster selecionado (ou uma pasta escolhida diretamente) em modo somente leitura, mesmo se houver problemas estruturais. Verifica instalação, INI, existência de token, portas declaradas, papéis de shards, Lua, registros globais e caches padrão, processos e espaço livre. Analisa até 256 KiB finais de server_log.txt por shard e classifica padrões de falha; não confirma causa, atualidade, validade do token, dependências, rede externa ou funcionamento real. O relatório exportável não contém tokens nem linhas brutas de logs. Nenhuma correção é automática; use o modo de manutenção após revisar as recomendações.

### Operações assistidas
No diagnóstico, filtre por gravidade, nome, ID ou descrição. Selecione um aviso de cache/setup/download para preparar a reinstalação ou um indício de Lua para abrir o catálogo visual. Erros de inspeção, instalação compartilhada, setup extra, processos ativos, espaço baixo e clusters não suportados bloqueiam os atalhos. O botão nunca executa a manutenção; uma nova análise, instalação exclusiva, duas confirmações e backup continuam obrigatórios. Antes de copiar o backup, calcula-se o tamanho do cluster e verifica-se a unidade de destino com reserva mínima de 64 MiB ou 5%. Downloads podem exigir espaço adicional. Relatórios são criados sem sobrescrever arquivos existentes.

## Central de backups
Novos backups incluem um manifesto local das configurações de mods, com caminho exato do cluster e instalação, shards e hashes dos arquivos. A central prepara a recuperação por meio do motor de manutenção existente, com nova análise e duas confirmações. Não reverte saves, tokens, server.ini ou o binário do dedicado; os mods da configuração antiga são baixados novamente quando disponíveis. Um override originalmente ausente é representado como tabela vazia. Backups antigos sem manifesto continuam acessíveis pela pasta, sem restauração automática. A desativação de todos os mods exige confirmação DESATIVAR TODOS Cluster_N; nenhum download é iniciado nesse caso. A remoção pode afetar entidades ao iniciar o mundo.
