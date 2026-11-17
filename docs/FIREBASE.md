# Firebase Â· Builds comunitÃ¡rias

Projeto `farmland-companion`, banco Firestore `(default)`, plano Spark. O aplicativo usa Firebase Authentication e a API REST do Firestore com **Firebase ID tokens**, sujeitos Ã s [Security Rules](https://firebase.google.com/docs/firestore/use-rest-api). NÃ£o depende de Cloud Functions, conta de serviÃ§o, listeners ou contador de visualizaÃ§Ãµes.

## Arquivos e implantaÃ§Ã£o

1. Salve a configuraÃ§Ã£o **Web pÃºblica** como JSON vÃ¡lido em `firebase-config.json` na raiz (nÃ£o `const firebaseConfig = ...`). Campos: `apiKey`, `authDomain`, `projectId`, `appId`. A configuraÃ§Ã£o fornecida em `E:\Codigos\APP MV E MINA\firebase.config.json` foi convertida na cÃ³pia de trabalho atual. NÃ£o coloque segredos OAuth, chaves privadas ou credenciais administrativas nesse arquivo. A compilaÃ§Ã£o incorpora somente a configuraÃ§Ã£o Web; a sessÃ£o pessoal usa DPAPI do Windows.
2. Copie o mesmo JSON para `backend/firebase/public/firebase-config.json`. Esses dois arquivos locais estÃ£o ignorados no Git. Para CI, disponibilize o JSON antes da compilaÃ§Ã£o. `backend/firebase/public/login.html` contÃ©m o login Google pelo navegador. O callback volta exclusivamente para `127.0.0.1`, com estado aleatÃ³rio, origem Hosting fixa e timeout de trÃªs minutos. Alguns navegadores pedem permissÃ£o de acesso Ã  rede local.
3. Firebase Console â†’ Authentication â†’ Settings â†’ Authorized domains: autorize `farmland-companion.web.app` e `farmland-companion.firebaseapp.com`. Google deve estar habilitado. O fluxo usa [Firebase Google Sign-In](https://firebase.google.com/docs/auth/web/google-signin), sem client secret no desktop.
4. Na raiz, com Firebase CLI autenticado na conta responsÃ¡vel pelo projeto, publique:

   ```powershell
   backend/firebase/node_modules/.bin/firebase.cmd deploy --project farmland-companion --only firestore:rules,firestore:indexes,hosting
   ```

   Arquivos publicados: **`firestore.rules`**, **`firestore.indexes.json`** e **`backend/firebase/public/`**, conforme **`firebase.json`**. `backend/builds/firestore.rules` Ã© uma cÃ³pia de referÃªncia; a raiz Ã© a fonte de implantaÃ§Ã£o. Nenhuma publicaÃ§Ã£o foi executada automaticamente nesta entrega.

5. Antes de habilitar envio, rode os testes locais (Java 21+ e Node necessÃ¡rios):

   ```powershell
   cd backend/firebase
   npm ci
   npm test
   ```

   O comando usa somente o projeto emulado `demo-farmland-companion`. NÃ£o escreve no Firebase real. Confere anonimato, propriedade, alteraÃ§Ã£o de autor, autoaprovaÃ§Ã£o, escalada de administrador, progresso e avaliaÃ§Ãµes.
6. ApÃ³s implantar as regras e os Ã­ndices, conferir no Rules Playground o bloqueio de writes anÃ´nimos e de alteraÃ§Ãµes de aprovaÃ§Ã£o por usuÃ¡rio comum. Crie pelo **Console administrativo** o documento `deployment/builds` com `publishingEnabled: false`. Valide login e consultas com publicaÃ§Ã£o bloqueada. Somente depois da validaÃ§Ã£o, o responsÃ¡vel pode mudar para `true`. Documento ausente ou falso bloqueia publicaÃ§Ã£o tanto no app quanto nas regras.

## ColeÃ§Ãµes e aprovaÃ§Ã£o

- `builds/{uid}_{buildId}`: submissÃµes privadas do prÃ³prio autor, sempre `status: pending`. Revisar e reenviar atualiza a prÃ³pria submissÃ£o. O editor salva normalmente em SQLite e nÃ£o publica ao salvar ou ao fazer login.
- `approvedBuilds/{id}`: snapshots pÃºblicos revisados por ADM. Somente um usuÃ¡rio cujo UID esteja em `admins/{uid}` pode escrevÃª-los. **Crie/remova os administradores exclusivamente no Console**, nunca por aplicativo; o cliente nÃ£o lÃª nem altera essa coleÃ§Ã£o.
- `approvedCombinations/{classId}:{stage}`: um registro por classe/estÃ¡gio, apontando `approvedBuildId` para snapshot aprovado da mesma combinaÃ§Ã£o. As regras verificam a referÃªncia com `getAfter`. Finalidade nÃ£o multiplica o total (18 Ã— 3 = 54).
- `communityProgress/global`: somente ADM pode escrever `rewardUnlocked` (booleano). O app exige tambÃ©m 54 combinaÃ§Ãµes oficiais antes de mostrar o desafio concluÃ­do. Nenhum cÃ³digo de resgate Ã© armazenado/publicado pelo cliente.
- `reviews/{uid}_{approvedBuildId}`: estrutura preparada para avaliaÃ§Ãµes de 1 a 5 e comentÃ¡rio de atÃ© 1.000 caracteres; autor Ã© obrigatÃ³rio e nÃ£o pode ser falsificado. NÃ£o hÃ¡ contador de visualizaÃ§Ãµes nem UI de avaliaÃ§Ãµes nesta entrega.
- `deployment/builds`: liberaÃ§Ã£o operacional administrada pelo Console; clientes nÃ£o podem alterÃ¡-la.

**AprovaÃ§Ã£o Ã© uma decisÃ£o administrativa real.** As regras nÃ£o interpretam o JSON do equipamento nem confiam na completude calculada pelo cliente. O ADM precisa conferir classe, itens, compatibilidade e critÃ©rios oficiais (Early/Mid/End) antes de criar um snapshot aprovado. NÃ£o copie automaticamente um `status` enviado pelo cliente. Para aprovar, copie os campos revisados da submissÃ£o para `approvedBuilds`, mudando `status` para `approved`; registre a combinaÃ§Ã£o correspondente em um batch. ReediÃ§Ãµes da submissÃ£o nÃ£o alteram o snapshot aprovado. Ao revogar, remova/reaponte tambÃ©m a combinaÃ§Ã£o no mesmo batch; mantenha `rewardUnlocked` falso atÃ© validaÃ§Ã£o das 54 combinaÃ§Ãµes. CÃ³digos de recompensa continuam exclusivamente com o servidor/ADM.

## Cache e privacidade

Rascunhos, resultados pÃºblicos e lista de publicaÃ§Ãµes prÃ³prias ficam em `builds.sqlite`. Cache privado Ã© separado por UID; trocar/sair da conta limpa a lista exibida, sem excluir rascunhos. Consultas sÃ£o manuais, com intervalo mÃ­nimo de um minuto; filtros usam o cache. NÃ£o hÃ¡ polling, listeners ou leitura por hover. Tokens de sessÃ£o ficam em `firebase-session.dpapi` protegidos para o usuÃ¡rio atual do Windows; nÃ£o entram no banco, logs, Git ou executÃ¡vel. Sair da conta remove a sessÃ£o local; os rascunhos permanecem.

O primeiro login completo depende da pÃ¡gina Hosting publicada e de uma aÃ§Ã£o do usuÃ¡rio no navegador. Testes locais de regras nÃ£o substituem a conferÃªncia da implantaÃ§Ã£o real. **NÃ£o foram enviadas builds reais durante o desenvolvimento.**

### Nick do Discord
Apï¿½s o login, a aba Builds pede o nick do Discord quando ainda nï¿½o hï¿½ um salvo para o UID. O nick fica em SQLite neste computador, separado por conta; pode ser alterado pelo botï¿½o Nick do Discord. ï¿½ um nome informado pelo jogador, sem verificaï¿½ï¿½o de vï¿½nculo com o Discord. Nï¿½o altera permissï¿½es ou aprovaï¿½ï¿½es.


## Aprovaï¿½ï¿½o dentro do Companion (4.22)

Cadastre os UIDs autorizados em `admins/{uid}` exclusivamente pelo Console responsï¿½vel. Depois do login, o app verifica o prï¿½prio cadastro; sessï¿½es jï¿½ abertas podem usar **Verificar acesso ADM**. A aba **Aprovaï¿½ï¿½es** sï¿½ aparece apï¿½s confirmaï¿½ï¿½o remota. Nï¿½o existe comando no app para conceder essa permissï¿½o.

Use **Atualizar pendentes**, selecione uma submissï¿½o e confira os equipamentos, atributos, cartas, encantamentos, Random Options e notas. Para aprovar, a validaï¿½ï¿½o local precisa estar sem pendï¿½ncias e o ADM deve marcar a confirmaï¿½ï¿½o de revisï¿½o dos critï¿½rios oficiais. A validaï¿½ï¿½o local auxilia a revisï¿½o; a autorizaï¿½ï¿½o administrativa ï¿½ aplicada pelo Firestore em todas as operaï¿½ï¿½es.

**Aprovar build** cria um snapshot aprovado e atualiza a combinaï¿½ï¿½o classe/estï¿½gio junto com o status da submissï¿½o em um ï¿½nico commit. A versï¿½o da submissï¿½o ï¿½ conferida por `updateTime`: se o autor editar durante a revisï¿½o, o commit inteiro falha e o ADM precisa atualizar a fila. Repetiï¿½ï¿½es da mesma combinaï¿½ï¿½o nï¿½o aumentam o total de 54. A aï¿½ï¿½o nï¿½o altera nem libera recompensas.

**Rejeitar** exige um motivo e preserva o rascunho do jogador. Em Minhas Builds, o autor consulta o status e o motivo, pode corrigir e reenviar. O app consulta a fila manualmente (atï¿½ 30 por vez; intervalo de 30 segundos), sem listeners. A revisï¿½o de uma atualizaï¿½ï¿½o nï¿½o remove um snapshot anteriormente aprovado; revogaï¿½ï¿½o de snapshots permanece uma operaï¿½ï¿½o administrativa separada no Console.

As regras atualizadas permitem ao ADM alterar somente status e motivo na submissï¿½o; nï¿½o permitem reescrever os equipamentos do autor. A leitura de `admins` ï¿½ restrita ao prï¿½prio documento e nenhuma conta pode criar sua prï¿½pria permissï¿½o. Nenhuma build real foi aprovada ou rejeitada durante a implementaï¿½ï¿½o.

## Perfil público e estrelas (4.23)

O cabeçalho de Builds exibe Entrar com Google somente quando desconectado; após login, mostra ícone, nome público e saída discreta. Não há botão de verificação ADM. A autorização é verificada no login e ao restaurar a sessão, e continua sendo aplicada pelo backend em todas as aprovações. Perfil da Comunidade fica em Configurações, preservando o nick SQLite por conta. Nick vazio usa o nome público Google.

As publicações passam a armazenar `authorName` separadamente de `ownerUid`. Aprovações preservam ambos no snapshot. A interface nunca mostra e-mail ou UID; builds antigas sem nome exibem Autor não informado. Alterar o nick afeta as próximas publicações, preservando a autoria registrada nas versões antigas.

O sistema ativo usa **uma estrela**, sem notas de 1 a 5. `buildStars/{uid}_{approvedBuildId}` registra um voto por usuário/build, enquanto `approvedBuilds/{id}.starCount` guarda a contagem. Adicionar/remover exige um commit atômico com voto e contador, regras `getAfter` e versão `updateTime` do snapshot. As regras impedem duplicação, voto próprio e alteração avulsa da contagem, inclusive por administradores. A UI destaca sua estrela em dourado. O cache de votos SQLite é separado por UID; não há listeners. As consultas obtêm somente os votos do usuário nas até 30 builds da página. O aplicativo repete a leitura em conflito concorrente antes de tentar novamente.

A ordenação Mais estreladas usa o índice composto de `firestore.indexes.json` e a contagem real no servidor; use Buscar/sincronizar para atualizar a consulta. `reviews` permanece preservada para histórico, com criação/alteração de notas desativadas. A conferência desta migração encontrou zero avaliações e zero votos antigos: inicializou-se somente o contador ausente, sem criar estrelas fictícias ou remover dados. Para bases diferentes, não execute uma limpeza de reviews: converta pares únicos UID/build, descarte voto próprio e atualize contagem/voto juntos com acesso administrativo, preservando os originais.

Aprovações usa uma única rolagem por painel, lista virtualizada e botão Carregar mais pedidos para páginas adicionais de 30, sem limitar a revisão à primeira página.

## Atualizações do criador e autoria (4.25)

A primeira publicação continua exigindo aprovação administrativa. Depois disso, o criador pode abrir Editar minha build e usar Publicar / atualizar: o aplicativo atualiza os snapshots existentes da mesma identidade ownerUid/buildId e mesma classe/estágio, sem criar uma nova publicação ou reenviar para a fila. Estrelas, autoria UID, status e combinações aprovadas não são alterados. Mudar classe/estágio exige uma nova build. Duplicar/copiar gera outra identidade e exige aprovação inicial. Revogar/remover o snapshot no backend retira a possibilidade de atualização direta.

As regras permitem ao próprio autor alterar somente payload, título, finalidade e nome público. Elas continuam proibindo criar aprovação inicial, transferir autoria, trocar identidade/classe/estágio, manipular estrelas ou progresso. O app valida a completude antes de publicar; as regras controlam autorização e campos estruturados, sem interpretar o JSON dos equipamentos. A atualização de snapshot e submissão privada usa um commit atômico com precondições de versão.

communityProfiles/{uid} guarda apenas displayName e discordNickname públicos. Cada usuário pode editar somente o próprio documento. O login/restauração sincroniza o perfil e a comunidade resolve a autoria pelo UID, com consulta agrupada e cache de um minuto. A conferência desta atualização recuperou o nome de duas publicações antigas a partir do nick já associado ao UID ou do nome público Google, sem usar e-mail nem inventar autoria.

Aprovações mostra uma lista de pedidos. Revisar build abre um modal amplo, com uma rolagem para o painel e Detalhes e validação, ações fixas no rodapé e fechamento por botão/Esc. Não cria outra MainWindow.

## Otimização 4.26
A política atual de cache, intervalos e estrutura de progresso está documentada em [FIREBASE_CACHE.md](FIREBASE_CACHE.md); ela substitui os intervalos curtos descritos nas entregas anteriores.


## GitHub Actions e configuraÃ§Ã£o da release

Em Settings â†’ Secrets and variables â†’ Actions, cadastre `FIREBASE_WEB_CONFIG` como JSON puro da configuraÃ§Ã£o Web. O workflow valida o projeto, inclui somente apiKey/authDomain/projectId/appId na compilaÃ§Ã£o e remove o arquivo temporÃ¡rio. Sem a secret vÃ¡lida, a release falha em vez de distribuir o login desconfigurado.

A configuraÃ§Ã£o Web Ã© pÃºblica por natureza e pode ser extraÃ­da do executÃ¡vel. A autorizaÃ§Ã£o continua nas regras do Firestore e no Firebase Authentication. Nunca use conta de serviÃ§o, chave privada, refresh token ou credencial administrativa nessa secret. Chaves Divine Pride sÃ£o individuais e permanecem protegidas localmente pelo Windows.
