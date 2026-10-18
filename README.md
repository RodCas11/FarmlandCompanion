# Farmland Companion

## Baixar o Companion

**[Baixar Farmland Companion para Windows x64](https://github.com/RodCas11/FarmlandCompanion/releases/latest/download/FarmlandCompanion-win-x64.zip)** · [Todas as versões](https://github.com/RodCas11/FarmlandCompanion/releases)

1. Baixe o arquivo **FarmlandCompanion-win-x64.zip** na release mais recente.
2. Extraia **todo o ZIP** para uma pasta no seu computador.
3. Abra **FarmlandCompanion.exe**. Não é necessário instalar .NET ou compilar o projeto.

Requer **Windows 11 x64**. Mantenha as pastas `Data`, `docs` e `licenses` junto do executável. Para atualizar, use **Sair** na bandeja da versão antiga antes de abrir a nova; suas configurações e sua chave API são preservadas. Na página de releases, escolha o ZIP do aplicativo; os downloads **Source code** são para desenvolvedores.

Aplicativo WPF para Windows 11 x64, C#/.NET 8 e MVVM. Mineração, timers MVP, catálogo híbrido com Divine Pride e uma única janela que alterna entre painel e modo compacto. Executável self-contained: não exige runtime instalado manualmente, administrador, drivers ou serviços.

## Executar

Abra `dist/latest/FarmlandCompanion.exe`. Não há instalador. Se a versão anterior estiver aberta, use **Sair** na bandeja antes de abrir a build latest; ambas usam a mesma pasta de dados. O .NET extrai bibliotecas nativas automaticamente na primeira execução para uma pasta temporária do usuário. Mantenha documentação, fontes de dados e licenças de `dist/latest/` ao redistribuir. Uma única instância por sessão do Windows evita gravações concorrentes.

## SpotLight

Em **SpotLight**, selecionar um grupo mostra seus mapas imediatamente. A consulta e o grupo confirmado como ativo são salvos separadamente; use **Confirmar este grupo no servidor** somente após verificar no jogo. Pesquisa instantânea por nome/código, filtros combinados, limpeza dos filtros, destaque pelo nível do personagem e ordenação ficam na mesma tela. Às 20h de São Paulo, a confirmação fica desatualizada. O compacto mostra o grupo confirmado, total e contador, com consulta e confirmação rápidas na mesma janela.

O catálogo usa o JSON fornecido na raiz, `spotlights_farmland_completo.json`, extraído da [wiki oficial Farmland](https://farmland.net.br/wiki/index.php/SpotLight): sete grupos e 268 mapas, incorporados ao executável para uso offline desde a primeira abertura. Links e monstros ausentes podem ser complementados pelo catálogo verificado já salvo ou pela atualização explícita da wiki; níveis e grupos do JSON permanecem como fonte principal. Links ausentes ficam desabilitados. Consulte [docs/SPOTLIGHT.md](docs/SPOTLIGHT.md) para origem, contagens e limitações.

## Eventos e notificações

A tela **Eventos** mostra o próximo evento, contagem regressiva, agenda do dia em ordem cronológica e controles de alertas. Os horários iniciais foram fornecidos pela administração no pedido de implementação; o aplicativo não consulta o servidor para confirmar atividade ou alterações.

| Evento | Horários diários | Avisos padrão |
|---|---|---|
| 👻 Fantasminhas Dudu | 00:00, 04:00, 08:00, 12:00, 16:00, 18:00, 21:00 | 23:55 do dia anterior, 03:55, 07:55, 11:55, 15:55, 17:55, 20:55 |
| ⚔ KVM: Batalha de Midgard | Aberturas às 20:00 e 22:00 | 19:55 e 21:55 |
| 🔥 Maré Infernal | 00:30, 02:30, 04:30, 06:30, 08:30, 10:30, 12:30, 14:30, 16:30, 18:30, 20:30 e 22:30 | Sempre 5 minutos antes, por padrão |

O Dudu acontece em mapa sorteado pelo servidor no momento do evento; o aplicativo não prevê o mapa. O KVM informa a **abertura**, sem presumir o início do combate: times Vermelho e Azul, taxa de 50.000z e acesso pelo botão Batalhas Campais. Os detalhes de cada evento aparecem ao passar o mouse na agenda e no editor.

O fuso inicial é **America/Sao_Paulo**, configurável em **Configurações → Notificações de eventos** ou na própria tela Eventos. Ative/desative todos os avisos, Dudu, KVM e Maré Infernal individualmente, além dos sons. Escolha uma ou mais antecedências: **1, 5, 10, 15 ou 30 minutos**; o padrão é 5. Em **Gerenciar eventos**, edite horários, nome, descrição, ícone, som e antecedências próprias, desative eventos oficiais ou cadastre eventos personalizados. Os oficiais são identificados separadamente e não podem ser excluídos. Atualizações futuras dos padrões locais preservam preferências e horários que você personalizou.

O agendador usa o tick central do aplicativo e recalcula ocorrências pela data e pelo fuso do servidor. Funciona com a janela visível, compacta, minimizada ou na bandeja. Abrir ou retomar o app durante uma janela de aviso permite um único alerta com o tempo restante; antecedências ultrapassadas não são disparadas em sequência. Eventos já iniciados são ignorados. Cada ocorrência e antecedência é registrada antes do envio para evitar repetição após reinício ou mudança do relógio. O histórico mantém ocorrências recentes e é limpo gradualmente após 30 dias.

Os avisos usam a **notificação nativa do Windows**, com o ícone do aplicativo, símbolo e nome do evento, horário e tempo restante. Podem ser dispensados pelos controles do Windows e compartilham a fila dos alertas de mina/MVPs. A aparência, o fechamento e a exibição seguem o sistema operacional; não há uma janela WPF sobre o jogo nem um botão próprio “Entendi”. Sons dos eventos têm controle separado. Foco/Não incomodar e tela cheia exclusiva podem ocultar os avisos. O app não instala serviço ou tarefa agendada: **encerrado completamente, não emite alertas**.

Os arquivos `events.json`, `event-settings.json` e `event-notification-history.json` ficam em **`%APPDATA%\FarmlandCompanion\`**, com escrita atômica e backup. Os dados anteriores de mineração/MVPs/Spotlight e a chave DPAPI permanecem em `%APPDATA%\RagnarokCompanion\`, sem migração destrutiva. O histórico registra encaminhamento à fila do Windows, não comprova que o usuário viu o aviso. Nenhum desses dados pessoais é incluído no ZIP ou no Git.

O ícone original do Companion é incorporado no executável, na janela/barra de tarefas e na bandeja. A arte vetorial fica em `src/RagnarokCompanion/Assets/AppIcon.xaml`; `build/Generate-AppIcon.ps1` regenera o ICO nas resoluções 16–256 px. Não usa imagens do jogo.

A Maré Infernal tem 12 ocorrências diárias e é adicionada automaticamente ao atualizar o aplicativo, preservando horários personalizados, preferências e histórico dos eventos existentes. Compartilha o fuso configurado, reagenda o próximo dia e permite as mesmas antecedências de 1/5/10/15/30 minutos. Em **Gerenciar eventos**, desmarque **Usar antecedências gerais** para escolher seus avisos e ajuste **Som deste evento** para silenciá-la individualmente.

Quando avisos de eventos com o mesmo horário ficam devidos no mesmo ciclo, o aplicativo os reúne em um único pop-up, por exemplo: “Fantasminhas Dudu e Maré Infernal começam em 5 minutos!”. Os horários oficiais atuais de Dudu e Maré Infernal não coincidem; o agrupamento continua disponível para horários personalizados coincidentes. O histórico JSON registra cada evento individualmente, com ID, nome, horário e antecedência; os identificadores persistidos continuam evitando repetição após reinício. Preferências são revalidadas no envio: eventos desativados saem do grupo, e o som toca somente se o controle geral e pelo menos um membro permitirem. Antecedências diferentes podem produzir avisos em momentos diferentes, respeitando a escolha de cada evento. Avisos expirados são descartados.

## Divine Pride e catálogo

1. Acesse a [documentação oficial](https://www.divine-pride.net/tools/api-doc), entre na sua conta e obtenha sua API Key.
2. Em **Configurações → Conexão com o Divine Pride**, cole a chave e clique **Proteger e salvar chave**. Região padrão **LATAM**, idioma **pt**, ambos configuráveis.
3. Em **MVPs**, pesquise por nome ou ID no catálogo local, selecione, favorite e registre a morte em **HH:mm**. A seleção consulta detalhes pelo ID; não existe busca online por nome.

A chave usa **DPAPI CurrentUser**, em `divine-pride-key.dpapi`, separado da credencial anterior. Favoritos, mapas, ajustes e timers são preservados. O cache local dura sete dias, com até 200 resultados. Consulte [o guia da integração](docs/RAGNAPLACE_API.md).

A documentação não define unidades dos campos de `spawns`. A previsão usa fontes verificadas Farmland, referências salvas e o catálogo Renewal; sem dados, mantém a morte sem inventar horários.

## Farmland, tempos e favoritos

O **Farmland é Renewal sem terceiras classes**. Um gateway oficial não representa seus tempos ou configurações. A interface diferencia dados Divine Pride/gateway, informação não verificada, personalizado e Farmland confirmado **pelo usuário**.

O registro cotidiano pede somente **HH:mm** e a opção de dia anterior. O aplicativo associa ID, mapa e ponto ao catálogo, usa o respawn do Divine Pride quando disponível e preserva ajustes personalizados. Se não houver dados, salva a morte e o favorito sem inventar um intervalo. Importação de catálogo e correções opcionais ficam em Configurações. O mapa é opcional se desconhecido. Use mínimo igual ao máximo para respawn fixo, ou dois limites para uma janela variável. Os valores são minutos, acima de zero e até 10080. Marque a confirmação Farmland somente quando tiver verificado esses tempos. As configurações locais têm prioridade sobre valores genéricos do mesmo monstro/mapa recebidos da API.

O contrato fornece um único respawn em segundos por mapa, sem máximo variável. O app converte a unidade e usa essa referência automaticamente, identificando o gateway; não inventa uma variação ausente nem presume que o tempo vale no Farmland. As previsões não confirmam que o monstro está vivo.

Clique na estrela ao lado do nome para favoritar ou desfavoritar sem iniciar um timer. A busca é imediata e mostra favoritos primeiro. Favoritos e tempos personalizados funcionam offline. Um novo horário substitui a morte do mesmo ponto; corrigir para uma morte mais antiga pede confirmação. Cadastro manual, edição, filtros avançados, respawn e consulta Divine Pride ficam em Configurações. O histórico mantém mortes, correções e alertas.

## Painel e compacto na mesma janela

**Modo compacto ↗** reduz a própria `MainWindow`; **Expandir ↗** a expande. Nenhuma segunda janela de timers é criada. As trocas de modo e consultas assíncronas preservam estado e timestamps.

O compacto tem quatro abas fixas: **⛏ Mina**, **☠ MVPs**, **🔔 Eventos** e **☀ Spot** (SpotLight). Somente o módulo selecionado é exibido. A última aba fica salva em `settings.json` e é preservada ao expandir, compactar e reiniciar. Trocar de aba não reinicia cronômetros, não muda a seleção automaticamente e não interrompe notificações.

- **Mina:** status, cronômetro grande, progresso, próxima entrada e botão de entrada quando disponível. Mantém os ciclos de 30 + 60 minutos e durações já configuradas.
- **MVPs:** até cinco favoritos, com timers ativos primeiro em ordem do próximo respawn; os demais mostram “Sem horário”, ou “Respawn desconhecido” quando há morte salva sem intervalo. O controle **Mostrar horário previsto** alterna o contador e o horário absoluto. Se houver mais de cinco favoritos, escolha quais exibir em Configurações. **Limpar MVPs** pede confirmação e limpa todos os timers de MVP, preservando favoritos, catálogo, tempos configurados e histórico.
- **Eventos:** próximas ocorrências de qualquer tipo (Dudu, KVM, Maré Infernal e personalizados), ordenadas por horário, com nome, horário e contador. Três linhas visíveis e rolagem para até oito próximas ocorrências. Eventos simultâneos aparecem em linhas separadas. Usa os horários cadastrados e o fuso configurado.
- **SpotLight:** seleção atual, quantidade de mapas, próxima rotação, seletor e confirmação. A tabela completa permanece no painel; a primeira consulta à wiki também pode ser feita ao abrir essa aba.

Pontos âmbar nas abas indicam mineração recém-concluída (cinco minutos) ou mina novamente disponível, MVP com respawn possível/em até cinco minutos, evento em até cinco minutos ou SpotLight pendente. São indicadores de estado; não mudam a aba nem roubam foco. Os alertas continuam usando os mesmos serviços do painel.

Arraste pelo título, redimensione pelo canto/bordas e use os botões de minimizar/fechar. A largura padrão é 320 e a altura parte de 220 unidades WPF, adaptando-se à aba; a largura mínima é 300. A barra de abas e o botão Expandir ficam fora da área rolável. Fonte, opacidade (50%–100%), Topmost e abertura automática ao iniciar app/timer continuam configuráveis. Posição/tamanho são salvos e coordenadas fora dos monitores são recuperadas. Os antigos valores de exibição conjunta são mantidos nos arquivos para compatibilidade; a navegação por abas substitui o layout misto e as antigas opções de esconder módulos.

Por padrão, fechar oculta a janela na bandeja, mantendo os timers ativos. Pelo ícone, abra painel/compacto, consulte mina/próximo MVP, alterne fixação/sons ou use **Sair** para encerrar realmente. Desative **Continuar na bandeja** para encerrar ao fechar a janela.

## Mineração e notificações

**Entrei na mina** inicia 30 minutos de mineração e automaticamente 60 de cooldown. Depois, a mina volta a disponível e aguarda entrada manual. Durações são configuráveis; corrija a hora de entrada ou reinicie/libere o ciclo quando necessário. Alterações de duração afetam novas entradas/correções; ciclos já iniciados conservam os timestamps.

Alertas usam balões nativos de bandeja do Windows (`NotifyIcon`), som opcional e fila para eventos simultâneos. Mina/MVPs têm controles independentes e antecedências como `10,5,1`. Marcos persistidos impedem repetição. Ao retomar, somente a antecedência mais recente relevante é enviada e as transições vencidas são reconciliadas. O Windows pode suprimir balões/sons conforme notificações/foco; o histórico permanece disponível.

## Offline, persistência e backup

Sem internet funcionam mineração, timers, favoritos, histórico, preferências, catálogo local e configurações adotadas. Falhas da API nunca apagam timers. Não há execução após **Sair** ou com o computador desligado; na reabertura, tudo é recalculado pelos timestamps UTC e mostrado em hora local. Ajustar o relógio do Windows afeta as contagens.

Dados em `%APPDATA%\RagnarokCompanion\`:

- `settings.json`, `mining.json`, `mvps.json`, `history.json`, `window-layout.json`: estado principal, histórico de até 5000 entradas e posição do compacto.
- `catalog-choices.json`: favoritos/configurações escolhidas, sem respostas brutas da API.
- `divine-pride.json`: gateway e última consulta bem-sucedida, sem chave.
- `divine-pride-key.dpapi`: credencial cifrada por usuário do Windows.

Preferências são salvas automaticamente; textos são aplicados ao perder foco. JSONs usam arquivo temporário, flush e substituição atômica por arquivo com `.bak`. Arquivos inválidos são preservados como `.invalid-<timestamp>` e recuperados da cópia anterior ou padrões. Não há transação única entre os arquivos: uma interrupção entre gravações pode deixar o histórico uma revisão atrás do timer.

A busca traz até dez resultados por consulta, sem percorrer páginas automaticamente. Refine o nome para limitar resultados. Acentos e caixa são normalizados; aliases locais incluem Freeoni/Phreeoni e alguns nomes em português. Os demais nomes e traduções dependem do que o gateway disponibiliza; o contrato não documenta uma pesquisa específica por nome interno/sprite. O ID consulta diretamente os detalhes. Resultados repetidos no mesmo gateway/ID são reunidos, mantendo mapas e variantes distintos. Falhas mostram uma mensagem explícita e preservam a lista local; resultados vazios têm outra mensagem.

O cache de respostas da API é **somente em memória**, por dois minutos e até 100 entradas. Não foi encontrada permissão explícita para redistribuir/cachear o banco em disco; somente escolhas operacionais são persistidas. Ao selecionar um MVP confirmado, somente seu marcador essencial (ID, nome, mapa, gateway e origem) é salvo; favoritar e configurar o respawn preserva essas escolhas offline. Respostas brutas não são gravadas em disco. O intervalo operacional adotado de um ponto selecionado/favoritado é salvo com sua origem para uso offline. Resultados não selecionados podem precisar de nova consulta em outra sessão. A última consulta bem-sucedida pode ser atendida pelo cache; não significa atualização de todo o catálogo.

Importação/exportação preserva o formato anterior: preferências principais e cadastros/timers MVP, sem chave. Para backup completo, copie também os JSONs de favoritos e gateway. Cadastre novamente a credencial em outro usuário/computador. Importar não apaga o ciclo da mina nem o histórico.

## Gepard Shield

O programa não acessa memória/processos do jogo, não injeta DLLs, não captura pacotes, não usa hooks de teclado/mouse e não automatiza ações. Usa janela WPF comum e `Topmost`, sem overlay gráfico. A comunicação online ocorre com Divine Pride quando solicitada.

**Compatibilidade com Gepard não é garantida e não foi validada no jogo. Consulte as regras do servidor.** Tela cheia exclusiva pode ocultar a janela; não há técnica para forçar a exibição ou contornar o anticheat.

## Compilar e testar

Requer SDK .NET 8 no Windows. A restauração inicial precisa de internet; o aplicativo publicado usa rede apenas nas consultas opcionais.

```powershell
dotnet test tests/RagnarokCompanion.Tests/RagnarokCompanion.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File build-latest.ps1
```

O script detecta o SDK local `.tools/dotnet` e publica Release, `win-x64`, self-contained, single-file em `dist/latest/`, acompanhado de documentação, licenças e JSON fonte do catálogo inicial.

Smoke test WPF sem rede, com dados temporários isolados:

```powershell
Start-Process ./dist/latest/FarmlandCompanion.exe -ArgumentList '--smoke-test' -Wait
Get-Content "$env:TEMP/RagnarokCompanion-validation/smoke-test.txt"
```

Gera relatório e PNGs fora da distribuição, em `%TEMP%\RagnarokCompanion-validation` (ou no diretório passado em `--smoke-output=CAMINHO`). Exercita seis páginas, Eventos, Spotlight, modos da mesma janela, timers, bandeja, recuperação de posição, favoritos, configuração Farmland, persistência e envio de notificação nativa. Os testes HTTP usam mocks e não consomem a API real.

A integração possui testes HTTP simulados. Use **Testar conexão** ou `--check-divine-pride` com sua nova chave para validar a autenticação real. A publicação não usa a API real.

## Interface simplificada

O smoke WPF alterna as quatro abas compactas, verifica que cada uma mostra apenas seu módulo, testa o limite de cinco favoritos, registro em HH:mm e limpeza dos timers com preservação de dados, preservação de serviços/timestamps, notificações em aba inativa e restauração da aba após reinício. Também gera PNGs em 96, 120, 144 e 192 DPI (100%, 125%, 150% e 200%). Essas renderizações não substituem um teste físico de troca entre monitores com escalas diferentes. O manifesto declara [PerMonitorV2 conforme a documentação do Windows](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process); mudanças de DPI solicitam novo ajuste do compacto.

A navegação tem Início, MVPs, Histórico, Configurações, SpotLight e Eventos. Em MVPs, o fluxo é buscar, selecionar, favoritar se desejar e registrar a morte em HH:mm. A tela contém uma lista e um único formulário; os controles avançados ficam em Configurações. O Início oferece acesso direto à pesquisa de MVPs. O compacto adapta a altura à aba e às linhas exibidas e restaura o tamanho anterior ao expandir.

## Publicação: somente latest

### Releases no GitHub

A Action [Release Windows](https://github.com/RodCas11/FarmlandCompanion/actions/workflows/release.yml) compila no Windows, executa os testes e o smoke WPF offline, gera o ZIP e seu SHA256 e publica uma release ao enviar uma nova versão para `main` ou uma tag `v*`. A tag deve corresponder à `<Version>` do projeto, por exemplo `v4.0.0` para `4.0.0`.

Para publicar uma nova versão, atualize a versão do projeto, faça commit usando gitmoji e envie uma nova tag:

```powershell
git tag -a v4.0.1 -m "🚀 Release v4.0.1"
git push origin v4.0.1
```

O botão **Run workflow** na branch `main` também publica a versão definida no projeto. Releases existentes são preservadas: incremente `<Version>` para gerar outra. O workflow usa o token automático do GitHub; não precisa de API key Divine Pride nem de credenciais pessoais nos secrets.

Para gerar o mesmo pacote localmente:

```powershell
powershell -ExecutionPolicy Bypass -File build-release.ps1
```

O resultado fica em `artifacts/release/FarmlandCompanion-win-x64.zip`, com `FarmlandCompanion-win-x64.sha256` ao lado. O ZIP contém o executável self-contained, README, guias, licenças e os dados fonte do catálogo inicial. Código do projeto, SDK, símbolos de depuração, testes, relatórios, caches e credenciais não são incluídos. O SHA256 também é conferido pela Action antes de publicar a release.

### Build local segura

Execute .\build-latest.ps1 (se a política local impedir scripts, use powershell -ExecutionPolicy Bypass -File build-latest.ps1). O fluxo compila Release, executa todos os testes, publica em diretório temporário, verifica o pacote e abre o novo executável em smoke test isolado antes de substituir dist/latest/FarmlandCompanion.exe. build.ps1 é um atalho para o mesmo fluxo; não permite pular testes nem criar executáveis com nomes diferentes.

A substituição usa um manifesto SHA256 e restaura a distribuição anterior se a troca falhar. A build antiga não é apagada se compilação, testes, publicação ou smoke test falharem. Arquivos em uso interrompem a troca com mensagem: use Sair na bandeja e tente novamente. Arquivos desconhecidos/alterados, diretórios sem identificação e links/junções são preservados. Dados do usuário em APPDATA e credenciais nunca são alvo da limpeza. Não são alterados código-fonte, tags, releases ou histórico Git.

A migração dos antigos executáveis da raiz de dist usa uma lista explícita de hashes dos artefatos gerados anteriormente pelo projeto. Nenhum arquivo é reconhecido apenas pela extensão. O relatório de remoções e preservações fica em artifacts/publication.json; testes e screenshots ficam fora de dist. Não edite/adicione arquivos dentro de latest: se o conteúdo não corresponder ao manifesto, o script preserva o diretório e interrompe a substituição para revisão.

Os critérios, cálculos, atualização e a lista dos 68 IDs sem spawn convencional estão em [Respawns automáticos](docs/RAGNAPLACE_API.md#respawns-automaticos).

## Buscar Itens — banco Farmland offline

**Pesquisar item → Ver melhores monstros → Ver melhor mapa.** A pesquisa por nome/ID é local e instantânea, com filtro inicial de 10% e até cinco recomendações por maior drop. Variantes Furious, Swift, Solid, Ringleader, campeões, eventos, invocações, quests e identificadores reconhecidos de instância são excluídos por padrão; MVPs e classes especiais também são classificados separadamente. Não basta `Class: Normal` para liberar um monstro.

Os quatro YAMLs originais do ADM **são privados**: não fazem parte do Git, do executável nem do pacote. Somente o banco `Data/farmland-items.sqlite` acompanha a distribuição, com IDs, nomes públicos, porcentagens, chaves de pesquisa e um indicador derivado para excluir variantes. Nenhum AegisName, script, atributo de combate ou configuração original é armazenado. `AegisName` e `Drops.Item` são usados apenas em memória para relacionar IDs; `Rate / 100` converte para porcentagem. Os índices SQLite cobrem IDs, nomes e relações de drop.

### Gerar ou atualizar o banco

Mantenha os arquivos privados fora do Git (por exemplo, em `private-data/`) e execute:

```powershell
powershell -ExecutionPolicy Bypass -File build/Import-FarmlandItems.ps1 -SourceDirectory C:\CAMINHO\PRIVADO
```

A pasta deve conter `item_db_equip.yml`, `item_db_etc.yml`, `item_db_usable.yml` e `mob_db.yml`. Por padrão o banco vai para `src/RagnarokCompanion/Data/farmland-items.sqlite`; use `-OutputDatabase CAMINHO` para outro destino. A substituição só ocorre após importação e validação completas; os originais permanecem intactos. Depois, execute `build-latest.ps1`. Em CI/publicação, disponibilize previamente o SQLite público gerado; os YAMLs não são necessários para compilar ou executar o aplicativo. A publicação exige esse banco e não gera uma base fictícia.

Para atualizar uma instalação, feche o Companion e substitua somente `Data/farmland-items.sqlite` pelo novo banco público gerado. Não é preciso alterar o aplicativo. Não coloque os YAMLs em `dist/latest` nem os anexe ao GitHub. Bancos SQLite e YAMLs de origem estão ignorados pelo Git.

### Exclusões e exceções

`Data/item-monster-rules.json` mantém IDs excluídos, IDs permitidos e padrões de nomes/identificadores. `ExcludeIds` tem prioridade; `IncludeIds` permite exceções explícitas. Os padrões Aegis são aplicados durante a importação, sem exportar seus valores. Nomes e IDs permitem revisar a classificação no aplicativo. Após mudar as regras, regenere o banco e reinicie o Companion. Uma exceção **não confirma spawn** e não cria mapa/população.

### Mapas e futura importação de spawns

`mob_db.yml` sozinho não comprova spawns ou populações. Na ausência de spawns do Farmland, o aplicativo usa referências públicas de mapas regulares do Renewal, identificadas nos resultados. Dados sem fonte suficiente continuam como **Mapa não identificado** e **Quantidade desconhecida**. Referências Renewal e Divine Pride não são apresentadas como confirmação do Farmland. **Melhor farm** só usa chance × população quando existe população importada; é um indicador comparativo, não itens por hora.

O banco já aceita uma importação pública futura de spawns, separada dos originais:

```json
[{"MonsterId":1102,"MapCode":"mapa_confirmado","MapName":"Nome público","Count":null,"Regular":true}]
```

Use `build/Import-FarmlandItems.ps1 -Spawns CAMINHO.json`. Informe `Regular: true` somente após verificar o spawn permanente nos arquivos do servidor; mapas de instância, quantidades inválidas e registros sem essa confirmação são recusados. Pontos regulares do mesmo monstro/mapa são somados; quantidade desconhecida permanece desconhecida. Reimportar os quatro YAMLs atualiza os drops, preserva os spawns Farmland existentes de IDs ainda presentes e atualiza a referência pública de mapas.

A aba **Itens** do compacto usa a mesma MainWindow e o mesmo banco. A seleção e os filtros permanecem salvos. Divine Pride é opcional: imagens/traduções são consultadas apenas pelo botão de informações adicionais; o ranking continua usando os drops do ADM. O cache e a chave protegida existentes são reaproveitados. Favoritos MVP, mineração, eventos e SpotLight continuam independentes.

### Nomes em português e mapas automáticos

Coloque `bRO.lua` na pasta privada dos YAMLs antes de regenerar a base. O importador lê somente dados, sem executar Lua: detecta UTF-8 ou Windows-1252 e extrai `identifiedDisplayName` por ID como nome principal. O nome inglês original e `unidentifiedDisplayName` permanecem como alternativas de pesquisa, com índices normalizados sem acentos; a apresentação mantém acentos e prefixos. Exemplo: **661 → [Q] Avental Elegante**, pesquisável também por **Avental Elegante** e pelo nome inglês; **1061 → Areia Estelar de Bruxa**. O Lua original está ignorado pelo Git e não acompanha o aplicativo.

Para atualizar somente tradução e referências de mapas, preservando drops e dados existentes:

```powershell
powershell -ExecutionPolicy Bypass -File build/Import-FarmlandItems.ps1 -DataOnly -Lua C:\PRIVADO\bRO.lua
```

O SQLite versão 2 acrescenta nome inglês, nome não identificado, índices de busca e procedência dos mapas. A migração ocorre em transação, sem apagar itens, drops ou spawns existentes. A regeneração completa também integra automaticamente o Lua presente na pasta de origem.

Ao selecionar um item, até cinco monstros comuns têm os mapas complementados pelo cliente Divine Pride existente, por ID e região. Foram verificados os campos públicos `spawns[].mapName` (código), `mapDisplayName` e `quantity`; `respawnTime` não é interpretado. Apenas pares monstro/mapa constantes do catálogo público de spawns permanentes são aceitos, excluindo instâncias, callbacks, scripts de evento e mapas de quest conhecidos. A consulta reaproveita o cache de sete dias, com uma única atualização de registros antigos sem campos de mapa. Não há enumeração da API nem consulta por tecla. Dados explícitos do Farmland têm prioridade.

A referência offline em `Data/public-regular-spawns.json` deriva exclusivamente dos arquivos públicos rAthena Renewal no commit `d4b8e7b8f16061cc2496d377ac8f777ce72a4f39`; não utiliza spawns privados do ADM. O gerador é `build/Update-PublicFarmSpawns.ps1 -RathenaRoot CAMINHO`. Mapas com população conhecida são ordenados pela maior quantidade; dados ausentes permanecem desconhecidos. Aliza (1737) tem referência regular em `kh_dun01` (55) e `kh_dun02` (13); `kh_kiehl01` é excluído como mapa de quest. Para o drop **0,01%** do item 661, escolha **Todas** ou **Mostrar chances menores**, disponível também no compacto.

### Consulta simplificada

O cabeçalho mostra nome português, ID, ícone e **Ver no Divine Pride ↗**. Não exibe nem consulta descrições, efeitos ou requisitos de equipamento. O link aponta à seção da região/idioma configurados na página pública do item; o servidor global do site continua sendo uma preferência do navegador. O endereço e cache de imagens existentes são reaproveitados. Os resultados de farm e os dados armazenados permanecem preservados.

### Descrição por hover

Passe o mouse sobre o ícone ou nome do item no painel ou compacto. Após 400 ms, um Popup WPF escuro mostra o nome, ícone e descrição local formatada. Há rolagem para textos grandes, largura de 340 px e 250 ms de tolerância ao sair, permitindo mover o ponteiro para ler e rolar. O popup não recebe foco e fecha ao sair, trocar item, esconder o painel ou desativar a janela. A interface principal continua sem descrições e mantém **Ver no Divine Pride ↗**.

O texto é carregado por ID apenas no hover, fora da thread da interface, com cache limitado a 64 itens e sem chamadas à API. A fonte bRO é identificada como referência fornecida, sem declarar modificações Farmland confirmadas. Se o ADM fornecer descrições próprias atualizadas, mantenha o arquivo original dentro de `private-data/` e importe somente uma projeção pública autorizada:

```json
[{"Id":2255,"Description":"Descrição atualizada fornecida pelo ADM, com os códigos de cor originais."}]
```

```powershell
powershell -ExecutionPolicy Bypass -File build/Import-FarmlandItems.ps1 -Descriptions private-data/descricoes-autorizadas.json
```

A tabela `item_description_overrides` tem prioridade sobre o bRO e não altera seu texto original. Use essa opção somente com descrições cuja divulgação foi autorizada pelo ADM. Reinicie o aplicativo após atualizar o banco para renovar o cache. O Lua continua sendo lido somente como dados, e os originais privados não integram Git ou distribuição.

O CI obtém somente o SQLite público autorizado da release de dados indicada em `build/item-database.json` e valida seu SHA256 antes dos testes e da publicação. YAMLs e Lua originais não são enviados ao GitHub. Ao atualizar a base, envie um novo asset público versionado à release de dados e atualize esse manifesto antes do push. A tag da aplicação é criada somente após compilação, testes, smoke WPF e verificação do pacote.
