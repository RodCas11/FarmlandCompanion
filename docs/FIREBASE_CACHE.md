# Cache e sincronização Firebase · 4.26

O plano Spark foi mantido. A implementação usa SQLite, REST e regras do Firestore; não adiciona Functions, serviços de pesquisa pagos, listeners ou contadores de visualizações.

| Dados | Fonte inicial | Consulta ao Firebase |
|---|---|---|
| Catálogo/resumos, autores e estrelas | SQLite | Até 20 resumos por página, quando vencidos após 12h ou por ação manual |
| Equipamentos, cartas, refinos, enchants, RO, Shadows, visuais, atributos, ovo e notas | Conteúdo SQLite por ID/versão | Primeiro acesso ao conteúdo ou versão nova descoberta em sincronização |
| Progresso | Último resumo no SQLite, identificado como cache | Um documento `communityProgress/global`, após 6h ou manualmente |
| Estrelas do usuário | SQLite separado por UID | Consulta agrupada na sincronização do catálogo; leitura/commit ao votar |
| Rascunhos | SQLite existente | Só ao publicar/atualizar ou consultar publicações próprias |
| Autorização ADM | Verificação autenticada no Firebase | Login/restauração; regras verificam cada operação protegida |

`BuildCommunitySyncService` centraliza exclusão de requisições simultâneas, TTL, offline e backoff persistido. A inicialização tem atraso aleatório curto; o agendamento local tem intervalo de 6h com jitter e só consulta o catálogo quando seu TTL de 12h vence. Trocar abas e digitar na pesquisa não inicia consultas remotas. Buscar/sincronizar e Carregar mais são ações explícitas; o intervalo mínimo manual é de cinco minutos por consulta. Em falhas, o cache permanece disponível e as tentativas respeitam backoff exponencial com jitter. Nenhum timer fica consultando a nuvem continuamente.

A listagem usa projeção do Firestore: só metadados (`buildId`, título, autor/UID, classe, estágio, finalidade, estrelas, `version`, `updatedAt`). O payload completo permanece uma única vez no documento publicado e não é transferido na listagem. Assim, resumos e conteúdo são separados na consulta e no SQLite sem duplicar equipamentos em coleções remotas. A projeção reduz tráfego; o custo de leitura de um documento continua sujeito às regras de cobrança/cotas do Firebase.

Paginação usa cursores reais de documento; em Mais estreladas inclui também a contagem no cursor. Não usa offset. A pesquisa percorre os resumos já salvos localmente e limita os resultados a 20; consultas remotas usam os filtros selecionados e as páginas solicitadas, sem baixar a comunidade inteira.

## Persistência

- `community_summaries`: metadados locais.
- `community_contents`: payload por ID e versão; a versão nova substitui somente o conteúdo correspondente quando for baixado.
- `build_sync_state`: horário, página/cursor, último progresso e backoff.
- `build_sync_history`: histórico local limitado às últimas 500 operações.
- `build_star_cache`: voto por UID/build, reconciliado após resposta do servidor.
- Tabelas de rascunhos, publicações próprias e perfis anteriores permanecem preservadas; o cache legado continua legível.

O cache do progresso não concede permissões nem libera códigos de recompensa. A barra offline mostra o último valor conhecido, marcado como cache. Permissões administrativas não vêm do SQLite. Tokens continuam protegidos com DPAPI.

## Estrutura remota e migração

Publicações usam `version` (inteiro) e `updatedAt` (data UTC). A primeira aprovação inicia versão 1; atualizações de conteúdo incrementam a versão. Estrelas não incrementam a versão do conteúdo. As regras do autor exigem incremento para impedir alteração silenciosa de payload e manter o cache coerente; clientes anteriores precisam atualizar para continuar publicando alterações.

`communityProgress/global` contém `approvedKeys`, `count` e `rewardUnlocked`. A aprovação ADM atualiza a combinação e o resumo no mesmo commit, com precondição de versão, sem contar duplicatas. Usuários comuns não podem escrever o agregado. Revogações ou exclusões administrativas devem ajustar também as chaves do resumo; somente o ADM pode fazê-lo. Nenhum código de resgate foi introduzido.

A migração inicializa metadados faltantes e o resumo a partir das combinações oficiais existentes, sem apagar builds ou rascunhos. Nesta entrega, migração e `firestore.rules` foram publicadas. Não exige serviço pago, mudança de plano, novas credenciais ou configuração adicional pelo jogador. Os índices existentes permanecem válidos.

Para validar localmente: testes `BuildSyncTests` cobrem projeção/página de 20, exclusão de consultas simultâneas, TTL, reabertura por versão, invalidação, progresso offline e backoff de HTTP 429. Os testes do emulador validam permissões e o novo agregado.
As listagens de publica��es pr�prias e solicita��es administrativas tamb�m usam proje��o de metadados e p�ginas de 20. Abrir uma publica��o pr�pria ou revis�o busca o corpo uma vez e guarda seu conte�do por UID/identidade e vers�o do documento. As leituras repetidas de autoriza��o antes de carregar/aprovar foram removidas: a verifica��o de entrada permanece, e as regras autorizam cada consulta/commit no backend. As a��es administrativas continuam exigindo conex�o.
