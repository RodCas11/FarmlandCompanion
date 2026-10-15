# Contrato e decisões da integração RagnaPlace

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

O contrato admite `page` de 1 a 20 e `limit` de 5, 10, 15 ou 20. O app usa 10, com navegação explícita, sem percorrer páginas automaticamente. O filtro de nome é `q`; não foi inventado parâmetro de filtro MVP ou mapa. O filtro por mapa atua nos detalhes já disponíveis localmente. A pesquisa pode retornar monstros comuns. A confirmação de MVP usa `exp.mvp > 0` nos detalhes; a classe Boss sozinha não basta. Casos sem essa informação usam o catálogo local ou cadastro personalizado.

Gateways mostrados: membros de `renewal`, excluindo `dead`. Os gateways oficiais não representam o Farmland, que é Renewal sem terceiras classes. Nenhuma mecânica de classes é inferida a partir do gateway.

## Respawn e procedência

`spawns[].respawn` é um único inteiro em **segundos**; o app converte para minutos. O contrato não fornece respawn mínimo/máximo variável, nem dados específicos do Farmland. Quando disponível, o valor único é uma referência do gateway, apresentada como tal. Não é uma confirmação dos tempos de um servidor privado.

O usuário pode aceitar explicitamente a referência do gateway ou salvar um intervalo próprio. Para janela variável, os dois limites vêm da configuração local, sem inferir um máximo inexistente. Configurações confirmadas pelo usuário no Farmland têm prioridade quando o mesmo monstro/mapa chega da API. A identificação “Farmland — confirmado pelo usuário” exige a marcação explícita do usuário; nenhum tempo Farmland é fornecido de fábrica.

Cada mapa retornado gera uma entrada distinta. Não se combina o menor respawn de um mapa com o maior de outro. O catálogo inicial fornece somente 16 identidades de MVP verificadas no rAthena Renewal, sem supor mapas ou tempos Farmland. Dados desse pequeno catálogo estão em `src/RagnarokCompanion/Data/mvp-seed.json`, com licença e origem em `licenses/`.

Imagens são documentadas pelo contrato em `image.static` e `image.animated`. Os DTOs reconhecem a imagem estática, mas esta versão não baixa nem armazena sprites; a interface usa cartões de texto e atribuição com link. Isso evita distribuição de imagens do jogo sem licença específica.

## Cache, limites e segredos

O serviço de cache mantém no máximo 100 respostas na memória por até dois minutos. Não persiste respostas brutas, sprites ou um espelho do catálogo da API: não foi encontrada autorização explícita de redistribuição/cache persistente do banco. Bookmarks escolhidos pelo usuário, configurações locais e snapshots de timers adotados são persistidos para uso pessoal offline. Favoritos previamente usados podem reutilizar a configuração do timer sem nova consulta.

Consultas são assíncronas via HttpClient, serializadas e espaçadas por um segundo. Consultas iguais simultâneas compartilham o resultado em cache; a UI impede operações online simultâneas. Cada operação tem prazo total de 15 segundos. Requisições GET recebem no máximo uma repetição, com espera de 1,5 segundo, somente em 502/503/504 e se não houver pausa solicitada pelo servidor. Não há retry automático em 401, 403 ou 429. `Retry-After` e `X-RateLimit-Remaining`/`X-RateLimit-Reset` são observados; o contrato define Reset em segundos até liberação, não timestamp Unix. Durante a pausa, dados em cache continuam utilizáveis.

O HttpClient de produção não segue redirecionamentos, para evitar encaminhar a chave a outro host. Corpos de respostas e exceções HTTP não são mostrados nem registrados. A leitura de respostas é limitada a 2 MB. Trocar/apagar a chave invalida o cache da sessão. Não existe campo de chave em configurações/importações/exportações JSON. O PasswordBox permanece mascarado e é limpo depois de salvar. O arquivo `ragnaplace-key.dpapi` contém apenas bytes protegidos com DPAPI CurrentUser; não é portável entre usuários ou computadores.

## Validação

Os testes automatizados HTTP são simulados e o smoke test WPF não faz consultas reais. Em 15/10/2026, a refatoração UX também passou por diagnóstico real, com a chave DPAPI que o proprietário já havia configurado no Windows: gateway Renewal, autenticação, pesquisa e detalhes de um MVP confirmados, em três consultas limitadas. A chave não foi exibida, copiada para o repositório ou alterada. O diagnóstico não altera timers nem preferências. Resultado em `artifacts/ragnaplace-check.txt`.

Para verificar novamente, use Configurações → Conexão com o RagnaPlace → Testar conexão autenticada, ou execute o aplicativo com `--check-ragnaplace`. O diagnóstico lê somente a credencial já protegida e o gateway selecionado, faz no máximo três chamadas e grava um relatório sem segredos. Não há sincronização em massa. Uma autenticação válida não confirma os tempos específicos do Farmland; eles continuam sujeitos à confirmação/configuração do usuário.
