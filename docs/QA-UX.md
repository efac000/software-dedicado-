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

PR #29 integrado; testes locais de navegação em Diagnóstico e Backups nos dois temas aprovados. O catálogo permanece bloqueado durante edição. CI e release [45704640329](https://github.com/efac000/software-dedicado-/actions/runs/45704640329) aprovados, incluindo conferência de hashes antes da publicação e execução ARM64 nativa. O site recomenda a build 1.0.0-build-45704640329-1; ela continua marcada como pré-release no GitHub por não haver homologação em outras escalas físicas de DPI. A versão estável anterior permanece acessível.
