# Divine Pride — chave API e integração

1. Acesse https://www.divine-pride.net/tools/api-doc, entre na sua conta ou crie uma conta.
2. Obtenha a chave API na área indicada pelo site e copie-a.
3. Abra **Configurações → Conexão com o Divine Pride** no aplicativo.
4. Cole no campo mascarado e clique **Proteger e salvar chave** e **Testar conexão**.
5. Mantenha região **LATAM** e idioma **pt**, ou altere conforme necessário.

A chave é protegida por DPAPI CurrentUser em `divine-pride-key.dpapi`. Não compartilhe nem inclua em commits. A credencial anterior não é reutilizada; favoritos e dados antigos são preservados.

**Pesquisar → Favoritar → Registrar morte.** A pesquisa por nome/ID é local. A seleção consulta detalhes pelo ID e reutiliza cache em disco de sete dias, limitado a 200 resultados. Não existe busca geral por nome documentada. Falhas da API mantêm os dados locais.

Contrato: `GET https://www.divine-pride.net/api/database/Monster/{id}?apiKey=CHAVE`, headers `Accept-Language: pt` e `x-server: LATAM`. Erros não exibem chave, URL autenticada ou corpo recebido. Não há extração em massa.

A documentação mostra `spawns`, sem definir campos/unidades. Esses valores não são convertidos em intervalos por suposição: o aplicativo utiliza fontes verificadas Farmland, referências salvas e o catálogo Renewal. Sem dados, salva a morte sem previsão; correções opcionais ficam nas configurações avançadas.

Os testes HTTP são simulados. A validação autenticada real requer a nova chave do usuário; a build não consulta sua conta.

<a id="respawns-automaticos"></a>
## Respawns automáticos — catálogo 4.7.0

O registro normal pede somente a hora da morte. O catálogo integrado `src/RagnarokCompanion/Data/mvp-respawns.json` cobre **53 IDs MVP e 65 definições de spawn**, offline, extraídos do rAthena **Renewal** no commit `d4b8e7b8f16061cc2496d377ac8f777ce72a4f39`. Não há registros Pré-Renovação nem valores fictícios.

Fontes examinadas:

- Wiki oficial [MVPs do Farmland](https://farmland.net.br/wiki/index.php/MVPs): não foi encontrada uma tabela pública confirmando intervalos personalizados. Nenhum tempo foi marcado como oficial do Farmland por suposição.
- [Spawns Renewal rAthena](https://github.com/rathena/rathena/tree/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/npc/re/mobs), limitados aos arquivos habilitados em `npc/re/scripts_monsters.conf` e `npc/scripts_monsters.conf`. Somente definições permanentes, um monstro por definição, delay positivo, sem callback especial ou mapa de instância.
- [Cálculo do servidor](https://github.com/rathena/rathena/blob/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/src/map/mob.cpp#L1071) e [configuração global](https://github.com/rathena/rathena/blob/d4b8e7b8f16061cc2496d377ac8f777ce72a4f39/conf/battle/monster.conf): `delay1 + rnd()%delay2`, em milissegundos; multiplicadores padrão 100%, variância habilitada para `boss_monster` e desabilitada para `monster`. O gerador verifica esses padrões e interrompe se mudarem. A fronteira superior aleatória é exclusiva em ms; a interface apresenta um limite conservador em segundos/minutos. Configurações globais próprias do Farmland continuam desconhecidas.

Ifrit, ID **1832**, em **thor_v03**, usa no catálogo Renewal base **39.600 segundos** e variação adicional de **600 segundos**: **660–670 minutos**. Esses dados vêm da linha de spawn, não do banco de atributos. Não representam confirmação do Farmland.

A resolução preserva ajustes explícitos do usuário. Sem ajuste personalizado, prefere registro confirmado do Farmland, depois referências anteriormente salvas e então o catálogo Renewal. A chave inclui ID, mapa e identificador do spawn; pontos com intervalos diferentes não são reunidos em uma janela inventada. Referências de gateways diferentes não são copiadas entre si.

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
