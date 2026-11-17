# Catálogo customizado do Farmland

`farmland.lua`, fornecido pelo ADM, tem prioridade para nomes, descrições, quantidade de slots e posições/classes explicitamente informadas. O importador lê os dados sem executar Lua e preserva o catálogo complementar quando o arquivo não define um metadado.

Para atualizar a base antes de gerar a build:

```powershell
./build/Import-FarmlandBuildItems.ps1 -Lua 'E:\Codigos\APP MV E MINA\farmland.lua'
./build-latest.ps1
```

São atualizados o SQLite derivado e `Data/build-farmland-items.json`. Ovos identificados como mascotes entram no slot opcional de ovo. Descrições oficiais substituem as genéricas; itens ausentes do arquivo conservam o catálogo existente. O Lua original permanece ignorado no Git e não é distribuído.

Random Options das builds guardam somente o nome da opção. Valores de bônus antigos são removidos na leitura do modelo, sem editar publicações remotas automaticamente.

## Conferência completa de IDs

`build/Import-CompleteItemCatalog.ps1 -SourceDirectory <pasta-autorizada>` mescla todos os IDs de item_db_equip.yml, item_db_etc.yml e item_db_usable.yml, importa todos os nomes/descrições do bRO.lua e aplica o farmland.lua por último. Não reconstrói drops, mapas ou quests e não altera os rascunhos dos jogadores. O relatório confere a presença de cada ID antes de substituir o banco. Somente o SQLite e os metadados derivados entram na distribuição; os arquivos originais permanecem privados.

No editor, o filtro de compatibilidade é aplicado antes do limite de 20 sugestões. Como o Farmland usa transclasses sem terceiras classes, referências Renewal All_Third/Third são interpretadas no estágio transclasse, mantendo as restrições de profissão, gênero e posição. Informações específicas do farmland.lua continuam prioritárias.
