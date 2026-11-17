# Catálogo customizado do Farmland

`farmland.lua`, fornecido pelo ADM, tem prioridade para nomes, descrições, quantidade de slots e posições/classes explicitamente informadas. O importador lê os dados sem executar Lua e preserva o catálogo complementar quando o arquivo não define um metadado.

Para atualizar a base antes de gerar a build:

```powershell
./build/Import-FarmlandBuildItems.ps1 -Lua 'E:\Codigos\APP MV E MINA\farmland.lua'
./build-latest.ps1
```

São atualizados o SQLite derivado e `Data/build-farmland-items.json`. Ovos identificados como mascotes entram no slot opcional de ovo. Descrições oficiais substituem as genéricas; itens ausentes do arquivo conservam o catálogo existente. O Lua original permanece ignorado no Git e não é distribuído.

Random Options das builds guardam somente o nome da opção. Valores de bônus antigos são removidos na leitura do modelo, sem editar publicações remotas automaticamente.
