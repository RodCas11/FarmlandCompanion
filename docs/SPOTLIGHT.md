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

Total: 268 mapas. Os limites mínimo e máximo são os publicados, sem recalcular a partir dos monstros. O recomendado segue `Math.max(1,max-15)`. O filtro por nível usa o recomendado, como na wiki. O nível opcional do personagem destaca a faixa mínimo–máximo. Ordenação pelos cabeçalhos, pesquisa imediata por código/nome, monstros no tooltip e links de mapa para Divine Pride.

## Cache e permissões

A wiki não declara licença de redistribuição no rodapé nem em `rightsinfo` da API MediaWiki. Portanto o catálogo completo não é distribuído com o executável. O aplicativo lê uma única página pública para uso pessoal quando não existe cache e grava `spotlight-catalog.json` na pasta de dados existente `%APPDATA%\RagnarokCompanion`. Depois funciona offline; atualizar é explícito, limitado a uma tentativa por minuto. Não executa JavaScript remoto, não percorre a base de mapas nem usa a chave RagnaPlace. Mudanças de estrutura ou dados inválidos impedem a substituição do cache anterior. Sem cache, é necessária uma primeira consulta online. A atribuição e o link para a wiki permanecem visíveis.

## Seleção e rotação

O grupo ativo é informado pelo jogador. Nenhum endpoint público de grupo ativo foi identificado. Seleção, horário UTC, nível opcional e preferência de exibição ficam em `spotlight-selection.json`. Às 20h no fuso São Paulo, inclusive após suspensão ou reinício, o grupo anterior permanece selecionado e fica pendente de confirmação. O botão Confirmar permite reconfirmar o mesmo grupo. O contador calcula a próxima rotação nesse fuso, independentemente do fuso do Windows. O compacto usa a mesma MainWindow.

## Verificação

`FarmlandCompanion.exe --check-spotlight --check-output="C:\caminho\relatorio.txt"` importa a página oficial para o cache pessoal e valida contagens, seleção Lunar, pesquisa, filtros, ordenação, persistência e expiração em uma pasta de teste separada. Não modifica o grupo pessoal selecionado. Os testes automatizados usam dados sintéticos identificados como teste; o smoke WPF não acessa a rede.

A distribuição segue `build-latest.ps1`, com manifesto novo e reconhecimento do pacote anterior, smoke antes de substituição e bloqueio se arquivos estiverem em uso. Pastas de dados e credenciais anteriores são mantidas.
