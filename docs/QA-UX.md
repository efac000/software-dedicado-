# Homologação da revisão de UX 1.0.0

A release estável e o download oficial não devem ser substituídos antes dos gates abaixo.

| Gate | Critério |
|---|---|
| CI | Núcleo, acessibilidade, páginas internas, cancelamento e binários x64/x86/ARM64 |
| Segurança | Edição isolada por shard, diagnóstico somente leitura, backup e duas confirmações preservados |
| Visual | Revisar artefato UI-review-fixture, temas claro/escuro e estado vazio |
| DPI | Geometria simulada 96/120/144/168/192; validar também DPI real no Windows |
| Tecnologia assistiva | Narrador/NVDA: navegação, nomes, leitura dos estados e retorno de foco |
| Online | Servidor descartável exclusivo e desligado durante manutenção, chave Steam opcional e token Klei fornecidos localmente, sem registro dos segredos |
| Distribuição | SHA-256 dos três executáveis, publicação como candidata, revisão de downloads, só então promover a estável |

A moldura DWM mantém funções nativas e a camada visual permanece WinForms. Uma migração integral para WPF deve ser avaliada em uma revisão própria, com paridade de comportamento e testes completos. Não gerar certificados fictícios nem afirmar assinatura Authenticode quando ela não existe.
