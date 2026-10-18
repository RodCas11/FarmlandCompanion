# SpotLight — Farmland Companion

Fonte: https://farmland.net.br/wiki/index.php/SpotLight (revisão 5819, consultada em 15/10/2026).

## Dados e regras

A ferramenta publica seus dados no JavaScript inline da própria página: `monsters`, sete arrays de mapas e `lists`. Não há endpoint separado usado pelo seletor. A inspeção com navegador confirmou a troca local dos grupos, sem requisições adicionais. Os recursos externos observados são os módulos gerais do MediaWiki (`load.php`, estilos, startup, jquery e skins).

| Grupo | Mapas |
|---|---:|
| Solar | 39 |
| Lunar | 37 |
| Estelar | 39 |
| Radiante | 39 |
| Celestial | 38 |
| Infernal | 38 |
| Astral | 38 |

Total: 268 mapas. Os limites mínimo e máximo são os publicados, sem recalcular a partir dos monstros. O recomendado é lido diretamente de `nivel_recomendado` no JSON, inclusive quando diferente de uma regra derivada. O filtro por nível usa o recomendado, como na wiki. O nível opcional do personagem destaca a faixa mínimo–máximo. Ordenação pelos cabeçalhos, pesquisa imediata por código/nome, monstros no tooltip e links de mapa para Divine Pride.

## Cache e permissões

O arquivo `spotlights_farmland_completo.json`, fornecido na raiz do projeto e extraído do DOM da wiki, é a fonte principal incorporada ao executável. Grupos e mapas são lidos dinamicamente, sem uma lista de nomes fixa no aplicativo. Todos os níveis do JSON são preservados. Campos opcionais ausentes aparecem como “—”, sem cálculos substitutos.

O JSON fornecido tem links e tooltips nulos nos 268 mapas. O catálogo pessoal verificado em `%APPDATA%\RagnarokCompanion\spotlight-catalog.json` complementa somente esses campos ausentes, por código do mapa. A migração preserva as URLs individuais Divine Pride que o catálogo anterior já usava, conforme a rota de mapas publicada na ferramenta da wiki. Sem uma URL disponível no catálogo, **Ver mapa ↗** fica desabilitado. Não há monstros inventados. **Atualizar detalhes da wiki** consulta explicitamente uma página, no máximo uma vez por minuto, sem executar JavaScript remoto e sem usar a chave RagnaPlace. Falhas preservam o catálogo anterior.

## Seleção e rotação

O grupo ativo é informado pelo jogador. Nenhum endpoint público de grupo ativo foi identificado. O último grupo consultado (`ViewedGroupId`) e o grupo confirmado (`GroupId`, `SelectedUtc`) ficam separados em `spotlight-selection.json`, junto com o nível opcional e a preferência de exibição. Consultar outro grupo não altera nem renova a confirmação. Preferências antigas são preservadas. A sequência oficial informada é Solar → Lunar → Estelar → Radiante → Celestial → Infernal → Astral, retornando ao Solar. Ela é configurada explicitamente e persistida em `RotationOrder`; não é derivada da ordem alfabética ou do catálogo. O grupo e a data/hora confirmados formam a referência: o cálculo conta as viradas às 20h de São Paulo, inclusive após suspensão ou dias fechado. O botão **Corrigir SpotLight atual** redefine o grupo e a referência, preservando a sequência. A interface diferencia confirmação manual e grupo calculado. Sequências incompletas ou inválidas não são usadas para prever grupos. O contador calcula a próxima rotação nesse fuso, independentemente do fuso do Windows. Quests compara os mapas dos materiais com o grupo atual, destacando alternativas sem presumir bônus de drop. O compacto usa a mesma MainWindow e oferece a aba Quests, sem aba SpotLight.

## Verificação

`FarmlandCompanion.exe --check-spotlight --check-output="C:\caminho\relatorio.txt"` importa a página oficial para o cache pessoal e valida contagens, seleção Lunar, pesquisa, filtros, ordenação, persistência e expiração em uma pasta de teste separada. Não modifica o grupo pessoal selecionado. Os testes automatizados validam o JSON real e usam dados sintéticos identificados nos testes de ausência/complemento de campos. O smoke WPF não acessa a rede: usa os sete grupos do JSON, mostra Lunar com 37 mapas e valida popup real de tooltip, seletor aberto, foco/seleção e o layout mínimo em 100%, 125% e 150% de DPI. Os PNGs ficam em `artifacts/latest-smoke` após a build.

O quadrado claro vinha do tooltip nativo do WPF: a linha fornecia apenas texto e não havia estilo de ToolTip no tema. Foram definidos template escuro com borda arredondada e contexto ligado à linha atual, além de templates explícitos de células/cabeçalhos, itens do seletor e menus de contexto. O smoke gera uma comparação com o tooltip nativo sem estilo, abre e fecha o popup novo e verifica a superfície escura. O smoke também tenta movimentar o cursor real e registra `spotlight-pointer.txt`: se o desktop não entregar entrada de mouse à janela, informa a limitação e mantém as verificações reais do popup aberto/fechado, foco e seleção. A renderização em DPI não substitui mover fisicamente a janela entre monitores.

A distribuição segue `build-latest.ps1`, com manifesto novo e reconhecimento do pacote anterior, smoke antes de substituição e bloqueio se arquivos estiverem em uso. Pastas de dados e credenciais anteriores são mantidas.
