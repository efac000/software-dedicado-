# Homologação da revisão de UX 1.0.0

A release estável e o download oficial não devem ser substituídos antes dos gates abaixo.

| Gate | Critério |
|---|---|
| CI | Núcleo, acessibilidade, páginas internas, cancelamento e binários x64/x86/ARM64 |
| Segurança | Edição isolada por shard, diagnóstico somente leitura, backup e duas confirmações preservados |
| Visual | Revisar artefato UI-review-fixture, temas claro/escuro e estado vazio |
| DPI | Geometria simulada 96/120/144/168/192; validar também DPI real no Windows |
| Tecnologia assistiva | Navegação, contraste e UI Automation mantidos; teste específico Narrador/NVDA dispensado pelo responsável pelo projeto |
| Online | Servidor descartável exclusivo e desligado durante manutenção, chave Steam opcional e token Klei fornecidos localmente, sem registro dos segredos |
| Distribuição | SHA-256 dos três executáveis, publicação como candidata, revisão de downloads, só então promover a estável |

A moldura DWM mantém funções nativas e a camada visual permanece WinForms. Uma migração integral para WPF deve ser avaliada em uma revisão própria, com paridade de comportamento e testes completos. Não gerar certificados fictícios nem afirmar assinatura Authenticode quando ela não existe.

## Evidências da revisão 08959ff (25/11/2026)

- [PR #28](https://github.com/efac000/software-dedicado-/pull/28) integrado: preferências com gravação atômica e mensagens de falha precisas; regressão local e CI aprovados.
- [CI e publicação da candidata](https://github.com/efac000/software-dedicado-/actions/runs/45700323953): núcleo, interface, x64, x86, ARM64 nativo e publicação aprovados.
- SteamCMD real em instalação exclusiva de QA: atualização confirmada, backup e hashes dos arquivos protegidos do mundo e configurações preservados.
- Workshop real: manutenção do mod 378160973 aprovada, com hashes dos demais arquivos do mundo preservados. A consulta autenticada de busca foi homologada anteriormente no PR #16; não se afirma nova consulta autenticada nesta rodada.
- Online: logs novos confirmam Master registrado, Caves conectado, autenticação, entrada do jogador nos dois shards e migração; nenhuma indicação de erro fatal encontrada. Jogador confirmou o teste. Os dois processos descartáveis foram encerrados. Nenhum segredo ou identificador de jogador incluído neste registro.
- [Candidata 1.0.0-build-45700323953-1](https://github.com/efac000/software-dedicado-/releases/tag/1.0.0-build-45700323953-1): os três executáveis baixados e conferidos byte a byte via SHA-256 contra o manifesto; autotestes dos executáveis públicos x64 e x86 retornaram 0. ARM64 executado nativamente no CI.
- DPI: Windows local a 96 DPI, cinco tamanhos de janela e geometria simulada 96/120/144/168/192 aprovados; outras escalas reais ainda não verificadas. Não modificar configuração de vídeo do usuário sem intervenção ou autorização específica. A candidata permanece separada da estável até completar o critério ou haver decisão explícita de dispensa.
- Narrador/NVDA dispensados. Assinatura Authenticode e migração integral WPF não são requisitos desta entrega.

O resultado comprova os cenários testados, não ausência absoluta de defeitos em todas as configurações possíveis.

## Revisão de navegação 0e72320

PR #29 integrado; testes locais de navegação em Diagnóstico e Backups nos dois temas aprovados. O catálogo permanece bloqueado durante edição. CI e release [45704640329](https://github.com/efac000/software-dedicado-/actions/runs/45704640329) aprovados, incluindo conferência de hashes antes da publicação e execução ARM64 nativa. O site passou a distribuir a build 1.0.0-build-45704640329-1, promovida a estável após os testes complementares abaixo. A versão estável anterior permanece acessível.

## Homologação final e promoção (25/11/2026)

- Código da release: `0e72320b9ca7f9d8ead5c090e482d5f6f2051b9a`. Alterações posteriores são de documentação/implantação, não alteram os executáveis.
- Windows local: escala original 100%; 125% e 150% aplicados pela interface de Configurações, confirmados por DeviceDpi 120 e 144. Dez cenários de viewport (cinco por tema) em cada escala, incluindo perímetro dos botões, teclado, navegação, preferências, cancelamento e páginas de diagnóstico e backups. Ambos retornaram ACCESSIBILITY_QA_PASS e código 0. Escala restaurada a 100%.
- Os três executáveis públicos foram baixados e comparados com SHA-256.txt e os hashes publicados no GitHub. Autotestes e testes de janela dos arquivos públicos x64 e x86 retornaram 0. ARM64 nativo aprovado no workflow de release.
- [Release estável 1.0.0-build-45704640329-1](https://github.com/efac000/software-dedicado-/releases/tag/1.0.0-build-45704640329-1) promovida sem recompilar nem substituir os binários.
- [CI do PR](https://github.com/efac000/software-dedicado-/actions/runs/45704079064), [CI da revisão](https://github.com/efac000/software-dedicado-/actions/runs/45705851101), [release](https://github.com/efac000/software-dedicado-/actions/runs/45704640329) e [implantação pública anterior](https://github.com/efac000/software-dedicado-/actions/runs/45705851112) aprovados. O CI redundante 45704640412 ficou preso e recebeu solicitação de cancelamento; a automação temporária 45704826063 expirou aguardando esse CI, substituída pela publicação direta confirmada. As falhas históricas são preservadas no histórico.
- Limites: 175% e 200% somente simulados; Narrador/NVDA não exigidos; executáveis sem Authenticode. Não se garante ausência absoluta de defeitos.

## Homologação do tema escuro fixo (25/11/2026)

- PR #30 aprovado; código de release 465092256061e3e6f36495f3cd6a0f459943e5c4 com árvore idêntica à revisão testada. Tema claro, automático e seletor removidos; preferência antiga de tema ignorada; alto contraste do Windows preservado.
- Testes locais do núcleo e da interface em 96, 120 e 144 DPI reais, cinco viewports em cada escala; DARK_ONLY_PREFERENCES_PASS, navegação, perímetro, cancelamento, diagnóstico e backups aprovados. Escala original 100% restaurada.
- CI do PR 45708805848 e release 45709177843 aprovados; autotestes de x64/x86/ARM64, ARM64 nativo e SHA-256 dos três executáveis públicos verificados.
- Build 1.0.0-build-45709177843-1 promovida sem modificar os binários. A homologação online da base permanece documentada acima; nenhuma nova alegação de teste online nesta revisão exclusivamente visual.
