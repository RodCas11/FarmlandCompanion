# RagnaPlace — chave API e integração

## Como obter e configurar sua chave API

A chave permite que o Farmland Companion consulte o catálogo online do RagnaPlace. Cada usuário deve solicitar sua própria chave; ela não vem incluída no aplicativo.

### 1. Criar uma conta

1. Abra a [página oficial da API RagnaPlace](https://ragnaplace.com/en/api).
2. Se ainda não tem conta, clique em **Sign up** e conclua o cadastro seguindo as instruções do site.
3. Entre na conta pelo botão **Login**. Se aparecer **Log in to request an API key**, faça o login para continuar.

### 2. Solicitar a chave

1. Na mesma página, encontre a seção **Your API keys**.
2. Clique em **Request a key**, conforme exibido na página quando você está conectado.
3. Preencha as informações solicitadas pelo RagnaPlace. Caso seja solicitada uma descrição, explique que pretende usar a chave no Farmland Companion para consultas pessoais ao catálogo de monstros.
4. Envie a solicitação e aguarde a aprovação. A [página oficial](https://ragnaplace.com/en/api) informa que as solicitações passam por aprovação; solicitar uma chave não garante liberação imediata.
5. Quando a chave estiver disponível na sua conta, copie o valor da **API key**. Os campos e as instruções da solicitação podem mudar; siga o que o site apresentar.

### 3. Colocar a chave no Farmland Companion

1. Abra o aplicativo e entre em **Configurações**.
2. Localize **Integração RagnaPlace**.
3. Cole a chave no campo **API Key**.
4. Clique em **Proteger e salvar chave**. O campo será limpo após salvar; isso é esperado e não significa que a chave foi apagada.
5. Clique em **Atualizar gateways** e selecione um gateway Renewal disponível.
6. Clique em **Testar conexão autenticada** e confira a mensagem de resultado.

O gateway identifica a base consultada no RagnaPlace. Ele não confirma os mapas ou os tempos de respawn do Farmland; esses tempos continuam sujeitos à configuração e confirmação no aplicativo.

### 4. Usar o catálogo

Abra **MVPs** e use **Buscar MVP...** por nome, nome parcial ou ID. A busca local é imediata; a online usa a chave e o gateway configurados, após 400 ms sem digitação. **Buscar no RagnaPlace** repete explicitamente a consulta. Resultados externos exibem ID e gateway; ao selecionar ou favoritar, os detalhes confirmam `exp.mvp > 0`. Após a confirmação, a seleção essencial fica disponível offline. Informe a morte em **HH:mm** e o respawn será associado automaticamente por ID e mapa. Quando não houver fonte, a morte fica salva sem previsão; a edição avançada continua opcional.

### Se a conexão não funcionar

- **Chave ainda pendente:** confira o andamento da solicitação na sua conta RagnaPlace.
- **Chave inválida ou revogada:** confira o status no site e copie novamente a chave disponível; salve-a outra vez no aplicativo.
- **Nenhum gateway selecionado:** use **Atualizar gateways**, escolha um e repita o teste autenticado. Carregar a lista de gateways, por si só, não valida a chave.
- **Limite de consultas ou indisponibilidade:** aguarde e tente novamente mais tarde, evitando cliques repetidos.

### Cuidados com a chave

Trate a chave como uma senha: não publique em código, GitHub, mensagens ou capturas de tela. No aplicativo ela é armazenada localmente, criptografada pelo Windows para o seu usuário, e não precisa ser colocada em nenhum arquivo do projeto. Para removê-la, clique em **Apagar chave**.

O RagnaPlace permite uso gratuito e não comercial, exige atribuição e proíbe contornar limites ou extrair toda a base. Consulte as [regras oficiais da API](https://ragnaplace.com/en/api) antes de solicitar sua chave.

## Contrato e decisões técnicas

Inspeção em 15/10/2026. Fontes oficiais:

- Referência: https://ragnaplace.com/pt/api/reference
- OpenAPI público verificado por GET, HTTP 200, versão `1.0.0`: https://api.ragnaplace.com/v1/openapi.json
- Regras da API: https://ragnaplace.com/pt/api
- Gateways públicos verificados por GET, HTTP 200: https://api.ragnaplace.com/v1/gateways
- Esclarecimento do mantenedor sobre base e autenticação: https://ragnaplace.com/en/d/documentacao-da-apiapi-documentation-Y5HHOuM

A leitura inicial da página de referência não expôs o contrato, e requisições diretas ao HTML receberam 403. O contrato JSON oficial foi localizado e lido antes de implementar rotas autenticadas. Não foi utilizada extração do banco RagnaPlace nem tentativa de contornar limites.

## Rotas utilizadas

| Operação | Rota | Dados usados |
|---|---|---|
| Gateways | `GET /v1/gateways` | `gateways[].slug`, `gateways[].group` |
| Busca autenticada | `GET /v1/{gateway}/search?type=mob&q=...&page=...&limit=10` | `type`, `gateway`, `page`, `pages`, `items[].id`, `items[].name` |
| Detalhes autenticados | `GET /v1/{gateway}/mob/{id}` | `id`, `gatewayId`, `name`, `exp.mvp`, `spawns[].map.id`, `spawns[].respawn`, `url` |

Autenticação exclusivamente no header `x-api-key`, nunca na URL. O teste de autenticação usa uma busca pequena por Poring; o endpoint público de gateways não prova autenticação. Não há chamada autenticada automática ao iniciar o app.

O contrato admite `page` de 1 a 20 e `limit` de 5, 10, 15 ou 20. A busca principal traz até dez resultados e pede para refinar o termo; a consulta avançada mantém navegação explícita. Nenhuma delas percorre páginas automaticamente. O filtro de nome é `q`; não foi inventado parâmetro de filtro MVP ou mapa. O filtro por mapa atua nos detalhes já disponíveis localmente. A pesquisa pode retornar monstros comuns. A confirmação de MVP usa `exp.mvp > 0` nos detalhes; a classe Boss sozinha não basta. Casos sem essa informação usam o catálogo local ou cadastro personalizado.

Gateways mostrados: membros de `renewal`, excluindo `dead`. Os gateways oficiais não representam o Farmland, que é Renewal sem terceiras classes. Nenhuma mecânica de classes é inferida a partir do gateway.

## Respawn e procedência

`spawns[].respawn` é um único inteiro em **segundos**; o app converte para minutos. O contrato não fornece respawn mínimo/máximo variável, nem dados específicos do Farmland. Quando disponível, o valor único é uma referência do gateway, apresentada como tal. Não é uma confirmação dos tempos de um servidor privado.

O usuário pode aceitar explicitamente a referência do gateway ou salvar um intervalo próprio. Para janela variável, os dois limites vêm da configuração local, sem inferir um máximo inexistente. Configurações confirmadas pelo usuário no Farmland têm prioridade quando o mesmo monstro/mapa chega da API. A identificação “Farmland — confirmado pelo usuário” exige a marcação explícita do usuário; nenhum tempo Farmland é fornecido de fábrica.

Cada mapa retornado gera uma entrada distinta. Não se combina o menor respawn de um mapa com o maior de outro. O catálogo inicial fornece somente 16 identidades de MVP verificadas no rAthena Renewal, sem supor mapas ou tempos Farmland. Dados desse pequeno catálogo estão em `src/RagnarokCompanion/Data/mvp-seed.json`, com licença e origem em `licenses/`.

Imagens são documentadas pelo contrato em `image.static` e `image.animated`. Os DTOs reconhecem a imagem estática, mas esta versão não baixa nem armazena sprites; a interface usa cartões de texto e atribuição com link. Isso evita distribuição de imagens do jogo sem licença específica.

## Cache, limites e segredos

O serviço de cache mantém no máximo 100 respostas na memória por até dois minutos. Não persiste respostas brutas, sprites ou um espelho do catálogo da API: não foi encontrada autorização explícita de redistribuição/cache persistente do banco. Bookmarks escolhidos pelo usuário, configurações locais e snapshots de timers adotados são persistidos para uso pessoal offline. Favoritos previamente usados podem reutilizar a configuração do timer sem nova consulta. O intervalo operacional selecionado do gateway também é salvo com origem, sem persistir o corpo da resposta.

Consultas são assíncronas via HttpClient, serializadas e espaçadas por um segundo. Consultas iguais simultâneas compartilham o resultado em cache; o cliente serializa as chamadas e cancela buscas substituídas. A busca integrada usa debounce de 400 ms e descarta respostas antigas, inclusive após mudança de gateway. Cada operação tem prazo total de 15 segundos. Requisições GET recebem no máximo uma repetição, com espera de 1,5 segundo, somente em 502/503/504 e se não houver pausa solicitada pelo servidor. Não há retry automático em 401, 403 ou 429. `Retry-After` e `X-RateLimit-Remaining`/`X-RateLimit-Reset` são observados; o contrato define Reset em segundos até liberação, não timestamp Unix. Durante a pausa, dados em cache continuam utilizáveis.

O HttpClient de produção não segue redirecionamentos, para evitar encaminhar a chave a outro host. Corpos de respostas e exceções HTTP não são mostrados nem registrados. A leitura de respostas é limitada a 2 MB. Trocar/apagar a chave invalida o cache da sessão. Não existe campo de chave em configurações/importações/exportações JSON. O PasswordBox permanece mascarado e é limpo depois de salvar. O arquivo `ragnaplace-key.dpapi` contém apenas bytes protegidos com DPAPI CurrentUser; não é portável entre usuários ou computadores.

## Validação

Os testes automatizados HTTP são simulados e o smoke test WPF não faz consultas reais. Em 15/10/2026, a refatoração UX também passou por diagnóstico real, com a chave DPAPI que o proprietário já havia configurado no Windows: gateway Renewal, autenticação, pesquisa e detalhes de um MVP confirmados, com consultas limitadas, incluindo busca por Freeoni no gateway `laro-pt` e busca parcial por Detardeurus, ausente do catálogo inicial, com verificação `exp.mvp` nos detalhes. A chave não foi exibida, copiada para o repositório ou alterada. O diagnóstico não altera timers nem preferências. Resultado em `artifacts/ragnaplace-check.txt`.

Para verificar novamente, use Configurações → Conexão com o RagnaPlace → Testar conexão autenticada, ou execute o aplicativo com `--check-ragnaplace`. O diagnóstico lê somente a credencial já protegida e o gateway selecionado, faz no máximo quatro chamadas (gateways, busca parcial, detalhes e verificação de Ifrit) e grava um relatório sem segredos. Não há sincronização em massa. Uma autenticação válida não confirma os tempos específicos do Farmland; eles continuam sujeitos à confirmação/configuração do usuário.

<a id="respawns-automaticos"></a>
## Respawns automáticos — catálogo 4.7.0

O registro normal pede somente a hora da morte. O catálogo integrado `src/RagnarokCompanion/Data/mvp-respawns.json` cobre **53 IDs MVP e 65 definições de spawn**, offline, extraídos do rAthena **Renewal** no commit `d4b8e7b8f16061cc2496d377ac8f777ce72a4f39`. Não há registros Pré-Renovação nem valores fictícios.

Fontes examinadas:

- Wiki oficial [MVPs do Farmland](https://farmland.net.br/wiki/index.php/MVPs): não foi encontrada uma tabela pública confirmando intervalos personalizados. Nenhum tempo foi marcado como oficial do Farmland por suposição.
- [OpenAPI RagnaPlace](https://api.ragnaplace.com/v1/openapi.json): `spawns[].respawn` é segundos, sem um campo documentado de variação. Um valor presente pode ser adotado automaticamente como referência fixa do gateway, sem inventar a variação.
- [Spawns Renewal rAthena](https://github.com/rathena/rathena/tree/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/npc/re/mobs), limitados aos arquivos habilitados em `npc/re/scripts_monsters.conf` e `npc/scripts_monsters.conf`. Somente definições permanentes, um monstro por definição, delay positivo, sem callback especial ou mapa de instância.
- [Cálculo do servidor](https://github.com/rathena/rathena/blob/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/src/map/mob.cpp#L1071) e [configuração global](https://github.com/rathena/rathena/blob/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/conf/battle/monster.conf): `delay1 + rnd()%delay2`, em milissegundos; multiplicadores padrão 100%, variância habilitada para `boss_monster` e desabilitada para `monster`. O gerador verifica esses padrões e interrompe se mudarem. A fronteira superior aleatória é exclusiva em ms; a interface apresenta um limite conservador em segundos/minutos. Configurações globais próprias do Farmland continuam desconhecidas.

Ifrit, ID **1832**, em **thor_v03**, usa no catálogo Renewal base **39.600 segundos** e variação adicional de **600 segundos**: **660–670 minutos**. Esses dados vêm da linha de spawn, não do banco de atributos. Não representam confirmação do Farmland.

A resolução preserva ajustes explícitos do usuário. Sem ajuste personalizado, prefere registro confirmado do Farmland, depois intervalo presente no RagnaPlace e então o catálogo Renewal. A chave inclui ID, mapa e identificador do spawn; pontos com intervalos diferentes não são reunidos em uma janela inventada. Referências de gateways diferentes não são copiadas entre si.

Para uma morte em UTC `D`, início = `D + mínimo`, fim = `D + máximo`. Em respawn fixo, início e fim são iguais. A apresentação usa a hora local com data, e o motor conserva UTC: não reinicia na troca de aba, reinício ou suspensão. Antes do mínimo aparece a contagem; na janela aparece **Pode nascer**; depois, **Janela prevista encerrada**, sem afirmar que o MVP está vivo. No compacto, **Mostrar horário previsto** alterna contador e horário; o tooltip inclui a data completa.

Quando não há dados, a morte é gravada com `HasRespawn=false`, sem previsões ou alertas fictícios. Favoritos e histórico permanecem disponíveis. Dados legados sem esse campo mantêm seus tempos antigos (`HasRespawn=true`). Limpar MVPs remove as mortes/timers ativos, sem apagar configurações, favoritos ou histórico.

### Atualização autorizada e correções

Em Configurações → MVPs, **Atualizar catálogo de respawns (JSON)** importa um documento com `Version: 1` e `Records`, seguindo os campos do JSON integrado: ID, nome, mapa, identificador de spawn opcional, base/variação/mínimo/máximo em segundos, fonte, versão, data de verificação e confirmação do Farmland. A atualização persiste em `mvp-respawns-custom.json` e recupera previsões de mortes antes desconhecidas. Não sobrescreve correções personalizadas ou timers já configurados. O arquivo deve conter somente dados de uma fonte que o jogador verificou; `IsFarmlandConfirmed` é uma declaração do catálogo importado, não uma validação automática da administração.

O modelo atual importa apenas `ScheduleType: Interval` (padrão quando ausente). Instâncias, eventos e mapas `@` não recebem automaticamente um intervalo convencional. Não foi identificado um horário fixo oficial de MVP no Farmland; um catálogo marcado como relógio fixo é rejeitado, em vez de aplicar a fórmula de morte indevidamente. Correções opcionais permanecem na área avançada.

Para regenerar a fonte integrada, use `build/Update-RespawnCatalog.ps1 -RathenaRoot CAMINHO` com um checkout rAthena verificado e todos os arquivos ativos disponíveis. O script não lê credenciais nem atualiza a distribuição. Cada registro conserva a linha de spawn e um link fixado no commit de origem.

### IDs sem spawn convencional no recorte examinado

Estes **68 IDs** têm experiência MVP no banco Renewal, mas não passaram pelos critérios de spawn permanente habilitado. Podem ser variantes, invocações, instâncias ou eventos; não lhes foi atribuído um tempo genérico. Uma fonte adicional pode fornecer dados apropriados posteriormente.

| ID | Nome no rAthena |
|---:|---|
| 1373 | Lord of the Dead |
| 1399 | Baphomet |
| 1502 | Bring it on! |
| 1646 | Lord Knight Seyren |
| 1647 | Assassin Cross Eremes |
| 1648 | Whitesmith Howard |
| 1649 | High Priest Margaretha |
| 1650 | Sniper Cecil |
| 1651 | High Wizard Kathryne |
| 1708 | Thanatos Phantom |
| 1779 | Ktullanux |
| 1874 | Beelzebub |
| 1876 | Lord of the Dead |
| 1980 | Kublin |
| 2022 | Nidhoggur's Shadow |
| 2052 | Dark Lord |
| 2094 | Orc Hero |
| 2095 | Eddga |
| 2096 | Osiris |
| 2097 | Dracula |
| 2098 | Doppelganger |
| 2099 | Mistress |
| 2100 | Baphomet |
| 2101 | Lord of the Dead |
| 2103 | Ktullanux |
| 2104 | Evil Snake Lord |
| 2105 | Turtle General |
| 2106 | Vesper |
| 2107 | Fallen Bishop Hibram |
| 2108 | Gloom Under Night |
| 2109 | Valkyrie Randgris |
| 2110 | Ifrit |
| 2111 | Whitesmith Howard |
| 2112 | Lord Knight Seyren |
| 2113 | Assassin Cross Eremes |
| 2131 | Lost Dragon |
| 2235 | Paladin Randel |
| 2236 | Creator Flamel |
| 2237 | Professor Celia |
| 2238 | Champion Chen |
| 2239 | Stalker Gertie |
| 2240 | Clown Alphoccio |
| 2241 | Gypsy Trentini |
| 2319 | Buwaya |
| 2341 | 2011 RWC Boss |
| 2529 | Faceworm Queen |
| 2532 | Red Faceworm Queen |
| 2533 | Green Faceworm Queen |
| 2534 | Blue Faceworm Queen |
| 2535 | Yellow Faceworm Queen |
| 2996 | Celine Kimi |
| 3426 | Infinite Eddga |
| 3427 | Infinite Osiris |
| 3428 | Infinite Phreeoni |
| 3429 | Infinite Orc Hero |
| 3430 | Infinite Tao Gunka |
| 3450 | Bijou |
| 3473 | Stefan.J.E.Wolf |
| 3621 | Pet Child |
| 3628 | Heart Hunter Ebel |
| 3658 | Lich Lord |
| 3659 | Lich Lord |
| 3757 | Dracula of Rage |
| 3758 | Angry Moonlight Flower |
| 20620 | Red Pepper |
| 20621 | Senior Red Pepper |
| 20642 | Sweety |
| 20659 | Pitaya Boss |
