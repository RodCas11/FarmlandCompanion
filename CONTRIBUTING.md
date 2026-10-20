# 🛠️ Contribuindo com o Farmland Companion

O [README](README.md) é o manual dos jogadores. Este documento reúne a preparação do ambiente, os comandos de desenvolvimento e as regras de publicação.

## Ambiente

- Windows 11 x64.
- .NET SDK 8 e PowerShell.
- Git; GitHub CLI (`gh`) para baixar o banco público pelo exemplo abaixo.

O projeto existente usa **C# / WPF / MVVM**. Abra `src/RagnarokCompanion/RagnarokCompanion.csproj` na sua IDE. Os scripts preferem o SDK em `.tools/dotnet/dotnet.exe`, quando presente, ou usam `dotnet` do PATH.

## Banco de itens para desenvolvimento

Você não precisa dos arquivos privados do servidor para compilar. O banco público autorizado está em uma release de dados; sua versão e SHA-256 ficam em [build/item-database.json](build/item-database.json).

Na raiz do repositório, execute no PowerShell:

```powershell
$ErrorActionPreference = 'Stop'
$manifest = Get-Content build/item-database.json -Raw | ConvertFrom-Json
$downloadDirectory = Join-Path (Get-Location) '.tools/public-data'
New-Item -ItemType Directory -Force $downloadDirectory | Out-Null

gh release download $manifest.ReleaseTag --repo RodCas11/FarmlandCompanion `
    --pattern $manifest.AssetName --dir $downloadDirectory --clobber
if ($LASTEXITCODE -ne 0) { throw 'Falha ao baixar o banco público.' }

$downloadedDatabase = Join-Path $downloadDirectory $manifest.AssetName
if ((Get-FileHash -LiteralPath $downloadedDatabase -Algorithm SHA256).Hash -ne $manifest.Sha256) {
    throw 'SHA-256 inválido. O banco existente foi preservado.'
}
Copy-Item -LiteralPath $downloadedDatabase `
    -Destination src/RagnarokCompanion/Data/farmland-items.sqlite -Force
```

Feche o aplicativo antes de substituir um banco em uso. O SQLite gerado é ignorado pelo Git; o processo de release baixa a cópia pública validada.

## Compilar e validar

```powershell
dotnet build src/RagnarokCompanion/RagnarokCompanion.csproj -c Release
dotnet test tests/RagnarokCompanion.Tests/RagnarokCompanion.Tests.csproj -c Release
```

Faça verificações proporcionais à alteração. Para publicar uma build local validada:

```powershell
powershell -ExecutionPolicy Bypass -File build-latest.ps1
```

O script compila em Release, executa os testes, publica em staging e valida a interface WPF antes de substituir **`dist/latest/FarmlandCompanion.exe`**. A última build válida é preservada em caso de falha. Se o executável estiver aberto, encerre pelo **Sair** da bandeja e tente novamente.

Para gerar também o pacote de distribuição:

```powershell
powershell -ExecutionPolicy Bypass -File build-release.ps1
```

O ZIP e seu SHA-256 ficam em `artifacts/release/`. Distribua o pacote completo produzido pelo script, incluindo dados, documentação e licenças.

## Onde alterar

| Diretório / arquivo | Responsabilidade |
|---|---|
| `src/RagnarokCompanion/Models/` | Modelos e estado persistido |
| `src/RagnarokCompanion/Services/` | Agendamento, persistência, APIs, cache e consultas |
| `src/RagnarokCompanion/ViewModels/` | Estado e comandos da interface |
| `src/RagnarokCompanion/Views/` | Interface WPF |
| `src/RagnarokCompanion/Data/` | Catálogos, regras e banco gerado |
| `src/RagnarokCompanion/Assets/` | Recursos visuais do aplicativo |
| `tests/RagnarokCompanion.Tests/` | Testes automatizados |
| `build/` | Importadores, geração de referências e publicação |
| `.github/workflows/release.yml` | Build e release no GitHub Actions |

Reutilize os serviços existentes, mantenha o modo completo e compacto na mesma MainWindow e preserve configurações, favoritos e histórico. Evite refatorar módulos que não participam da alteração.

## Dados privados e credenciais

**Não envie ao Git nem distribua os YAML originais do servidor, `bRO.lua`, chaves API ou dados pessoais do aplicativo.** O `.gitignore` cobre esses arquivos, bancos gerados e diretórios de build; confira o diff antes de cada commit e nunca force a inclusão de originais privados.

- Cada jogador informa sua própria chave Divine Pride pela interface. Ela é protegida pelo Windows com DPAPI.
- Não inclua chaves em exemplos, logs, testes, screenshots ou workflow secrets para uso compartilhado pelos jogadores.
- Não use arquivos reais de `%APPDATA%` como fixtures de testes.
- Importe somente fontes autorizadas. Publique apenas a projeção de dados permitida, acompanhada das referências e licenças aplicáveis.
- Leia Lua como **dados**, sem executar scripts. Preserve os textos originais no banco e aplique a formatação na apresentação.

## Atualizar os dados de itens

O importador existente aceita os arquivos `item_db_equip.yml`, `item_db_etc.yml`, `item_db_usable.yml` e `mob_db.yml` em uma pasta privada. Quando presente, `bRO.lua` fornece nomes e descrições, relacionados pelo ID numérico. A leitura trata UTF-8 e Windows-1252, sem executar Lua.

```powershell
# Gerar o banco a partir de fontes autorizadas fora do repositório.
.\build\Import-FarmlandItems.ps1 -SourceDirectory 'C:\dados-autorizados'

# Atualizar nomes/descrições bRO, preservando os drops existentes.
.\build\Import-FarmlandItems.ps1 -DataOnly -Lua 'C:\dados-autorizados\bRO.lua'

# Importar mapas regulares ou descrições específicas autorizadas pelo ADM.
.\build\Import-FarmlandItems.ps1 -Spawns 'C:\dados-autorizados\spawns.json'
.\build\Import-FarmlandItems.ps1 -Descriptions 'C:\dados-autorizados\descricoes.json'
```

Os parâmetros de importação acima são usados em execuções separadas. Reinicie o aplicativo para renovar os caches após atualizar o banco.

Formato mínimo da fonte de spawns:

```json
[
  { "MonsterId": 1002, "MapCode": "codigo_confirmado", "MapName": "Nome confirmado", "Count": null, "Regular": true }
]
```

Esse exemplo descreve o formato, **não um spawn real**. Informe somente pares monstro/mapa confirmados, com quantidade positiva ou `null` quando desconhecida. Não misture spawns de eventos, instâncias, quests ou invocações com os regulares.

Formato da fonte de descrições autorizadas:

```json
[
  { "Id": 2255, "Description": "Texto real fornecido e autorizado pelo ADM." }
]
```

As descrições autorizadas têm prioridade sobre o bRO de referência. Sem essa confirmação, a interface deve identificar a origem e não apresentar atributos genéricos como modificações do Farmland.

A classificação de variantes especiais fica em `Data/item-monster-rules.json`: exclusões explícitas têm prioridade sobre inclusões. Preserve os nomes oficiais e seus prefixos; mantenha nomes em inglês como alternativas de pesquisa.

## Fontes, cache e limites

- **Divine Pride:** reutilize o cliente existente, a região/idioma configurados e o cache. Buscas por nome usam o catálogo local; detalhes são consultados por ID. Não faça requisições a cada hover.
- **Mapas:** prefira dados confirmados do Farmland. Referências públicas devem manter a indicação da origem. O resolvedor filtra pares de spawn regular conhecidos; não invente mapas ou populações.
- **Respawn:** priorize dados específicos do Farmland, preservando ajustes existentes e referências identificadas. Não interprete campos de tempo da API sem verificar o significado e a unidade. Se não houver referência, mantenha o tempo desconhecido.
- **Descrições:** o serviço local carrega por demanda e reutiliza cache limitado; o tooltip usa a formatação Ragnarok existente, sem expor os códigos `^RRGGBB`.
- **Notificações:** preserve os controles por evento, o agrupamento de simultâneos e o histórico individual.

Referências Renewal locais são geradas por `build/Update-PublicFarmSpawns.ps1` e `build/Update-RespawnCatalog.ps1`, com `-RathenaRoot` apontando para uma cópia pública do rAthena. O catálogo atual referencia o commit `d4b8e7b8f16061cc2496d377ac8f777ce72a4f39`; esses dados não confirmam as regras específicas do Farmland.

Consulte [as referências do SpotLight](docs/SPOTLIGHT.md), [a conexão e referências de respawn](docs/RAGNAPLACE_API.md) e [os créditos de terceiros](licenses/THIRD-PARTY-NOTICES.md). O nome legado `RAGNAPLACE_API.md` é mantido por compatibilidade de empacotamento; o conteúdo trata do Divine Pride.

## Identidade visual

As logos oficiais ficam em `logo/`. Para atualizar os recursos e o ícone multirresolução a partir dessas artes, use:

```powershell
.\build\Update-AppBranding.ps1
```

Preserve as cores, proporções e arquivos originais. Não redesenhe a marca nem substitua os recursos por imagens sem autorização.

## Commits e pull requests

Use **gitmoji** e uma mensagem objetiva, por exemplo:

```text
📝 Organiza o manual dos jogadores e o guia de contribuição
🐛 Corrige a ordenação dos próximos eventos
✨ Adiciona filtro à pesquisa de itens
```

Descreva o comportamento final, as verificações realizadas e qualquer limitação relevante. Não inclua credenciais ou originais privados no diff.

## Releases no GitHub

O workflow é acionado por push na **main**, tags **`v*`** ou execução manual. Ele baixa o banco público conforme o manifesto, confere o SHA-256 e executa build, testes e validação WPF antes de publicar o ZIP.

Para uma **nova versão do aplicativo**, incremente `<Version>` no `.csproj`. A tag deve corresponder exatamente a essa versão, por exemplo `v4.14.0` para `4.14.0`. Uma release já existente é preservada: novos pushes com a mesma versão não substituem seus arquivos.

Se o banco público mudar, publique um **novo asset versionado** autorizado na release de dados e atualize `build/item-database.json` com o nome e SHA-256 corretos. Não substitua silenciosamente o conteúdo de um asset já referenciado. Depois, incremente a versão do aplicativo para distribuir o novo pacote.

O workflow usa o token automático do GitHub para publicar; não precisa de chave Divine Pride. Não publique arquivos pessoais, `bin/`, `obj/`, originais privados ou o diretório de trabalho inteiro como assets de release.

## Quests e rotação do SpotLight

O arquivo privado `quests.txt` é ignorado pelo Git e nunca entra no pacote. Para importar 100 quests normais e 100 raras, use o catálogo YAML autorizado apenas para relacionar AegisName e ID:

```powershell
.\build\Import-Quests.ps1 -Quests 'C:\dados-autorizados\quests.txt' -SourceDirectory 'C:\dados-autorizados'
```

A importação usa DTOs de dados, transação SQLite e preserva os demais dados. Armazena recompensas, categorias, Zeny, materiais e refinos; inclui `Certificado_de_Caca`. Itens sem correspondência ficam com ID desconhecido e identificação explícita, sem adivinhar relações. Reimporte com o catálogo atualizado para resolvê-los. Uma importação completa de itens também importa quests se `quests.txt` estiver na pasta de origem; caso contrário, preserva o catálogo de quests anterior.

Favoritos e materiais obtidos ficam em `quest-progress.json` na persistência do usuário. A sequência completa do SpotLight fica em `spotlight-selection.json`, junto do grupo e horário de referência. A ordem do catálogo não define a rotação oficial. O cálculo considera as viradas às 20h de São Paulo, sem exigir que o aplicativo permaneça aberto.

Antes de distribuir esta versão via Actions, publique o novo banco autorizado com quests e atualize o manifesto de dados conforme a seção de releases. A validação WPF exige as 200 quests no pacote para evitar uma release com a aba vazia.

Quando `farmland.lua` estiver na pasta de origem das quests, o importador lê seus nomes/descrições como dados e prioriza essa fonte autorizada sobre o bRO. Correspondências confirmadas: `Gigant_Helm_BR` → 19220, `Crow_Tengu_MaskF` → 19456 e `Crow_Tengu_MaskM` → 19457. O original é privado e ignorado pelo Git. Nenhuma relação adicional é inferida por semelhança de nomes. A atualização completa de Lua e quests ocorre em uma cópia temporária do banco, substituída somente após sucesso.

Complemento de identificação: `farmlandc.lua` fornece o Martelo Cósmico (16074), associado à recompensa `Meteor_Strike_BR`; `bRO.lua` fornece Livro Muscular (540127), associado a `Livro_Muscular`, mantendo a descrição como referência bRO. `Valkyrjas_Dagger` corresponde à Adaga das Valquírias, ID 28728, confirmado pelo usuário. Apenas as entradas necessárias são extraídas do complemento, e `farmlandc.lua` também é privado e ignorado pelo Git.

## MVPs oficiais e sprites

`MVP.txt` é privado e ignorado pelo Git. O catálogo derivado embarcado `Data/farmland-mvp-respawns.json` contém os 55 pontos fornecidos pelo ADM. Para regenerar com a mecânica adicional confirmada pelo usuário:

```powershell
.\build\Import-FarmlandMvps.ps1 -VariationMode Additional
```

A conversão preserva segundos e trata `-` como variação zero. A fonte oficial substitui referências genéricas para esses monstros; escolhas manuais explícitas permanecem. Migração por monstro/mapa preserva as chaves dos favoritos e as mortes; timers genéricos existentes passam a usar a referência oficial disponível. Mapas fora do novo catálogo são mantidos como dados legados, sem associá-los a outro spawn.

Os cards usam [imagens públicas do Divine Pride](https://www.divine-pride.net/database/monster/1039/baphomet), na rota `https://static.divine-pride.net/images/mobs/png/{id}.png`. As imagens pertencem aos respectivos titulares. Cache em `%LOCALAPPDATA%\FarmlandCompanion\Cache\mvp-sprites\`, sem chave API; limite de duas transferências simultâneas, timeout e ícone de fallback. A validação isolada da interface não baixa imagens da rede.

As receitas são lidas exclusivamente de `quests.txt`; os antigos arquivos de mensagens não são aceitos. `Loja_Quest` define Normais e `Loja_Quest2_*` define Raras. Categorias normais vêm das posições dos equipamentos. Duas receitas diferentes de Mine_Helm são preservadas como alternativas; receitas idênticas são rejeitadas. As relações de dependência usam AegisName. A migração mantém IDs de receitas raras anteriores e posições dos materiais inalterados para preservar favoritos e progresso.
