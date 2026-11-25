# Gerenciador de Dedicado · 1.0.0

Aplicativo independente para Windows x64, x86 e ARM64 e servidores dedicados de Don't Starve Together. Projeto comunitário, não afiliado à Klei ou à Valve.

**Estado:** candidato a validação. Teste em um cluster descartável; não use em produção sem backup externo e teste real. A versão pública permanece 1.0.0; a revisão é identificada pelo commit e pelo número de build.

## Modos de manutenção
- **Reinstalar mods deste dedicado:** usa os próprios arquivos Master/Caves como referência; preserva as opções e não exige outro mundo.
- **Copiar configuração:** transfere as configurações de um mundo de referência para o dedicado.
- **Editar configurações e reinstalar:** editor Lua literal por shard, com validação sem executar Lua. Adicione entradas Workshop, altere opções ou desative mods. A análise e duas confirmações precedem qualquer escrita.
- O programa bloqueia listas totalmente vazias, coleções Workshop, configurações globais com mods não relacionados e instalações compartilhadas. Logs do atualizador ficam em arquivos separados de saída e erro no backup.
- A conexão Steam/Workshop continua necessária para baixar mods; testar a entrada no mundo após manutenção continua indispensável.

## Uso
1. No DST, crie e gere um mundo temporário com os mods de servidor desejados. Salve e feche o jogo. Desligue os shards e qualquer reinício automático do servidor.
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
