# Farmland Companion

## Baixar o Companion

**[Baixar Farmland Companion para Windows x64](https://github.com/RodCas11/FarmlandCompanion/releases/latest/download/FarmlandCompanion-win-x64.zip)** · [Todas as versões](https://github.com/RodCas11/FarmlandCompanion/releases)

1. Baixe o arquivo **FarmlandCompanion-win-x64.zip** na release mais recente.
2. Extraia **todo o ZIP** para uma pasta no seu computador.
3. Abra **FarmlandCompanion.exe**. Não é necessário instalar .NET ou compilar o projeto.

Requer **Windows 11 x64**. Mantenha as pastas `Data`, `docs` e `licenses` junto do executável. Para atualizar, use **Sair** na bandeja da versão antiga antes de abrir a nova; suas configurações e sua chave API são preservadas. Na página de releases, escolha o ZIP do aplicativo; os downloads **Source code** são para desenvolvedores.

Aplicativo WPF para Windows 11 x64, C#/.NET 8 e MVVM. Mineração, timers MVP, catálogo híbrido com RagnaPlace e uma única janela que alterna entre painel e modo compacto. Executável self-contained: não exige runtime instalado manualmente, administrador, drivers ou serviços.

## Executar

Abra `dist/latest/FarmlandCompanion.exe`. Não há instalador. Se a versão anterior estiver aberta, use **Sair** na bandeja antes de abrir a build latest; ambas usam a mesma pasta de dados. O .NET extrai bibliotecas nativas automaticamente na primeira execução para uma pasta temporária do usuário. Mantenha documentação, fontes de dados e licenças de `dist/latest/` ao redistribuir. Uma única instância por sessão do Windows evita gravações concorrentes.

## SpotLight

Em **SpotLight**, selecione o grupo informado no jogo para ver todos os mapas. Pesquisa, filtros por recomendado, ordenação nos cabeçalhos, monstros no tooltip e links Divine Pride estão disponíveis. O nível do personagem é opcional e destaca faixas compatíveis. A seleção é manual; às 20h de São Paulo ela fica pendente de confirmação. O compacto mostra grupo, total, contador e seleção rápida na mesma janela.

O catálogo vem da [wiki oficial Farmland](https://farmland.net.br/wiki/index.php/SpotLight), é salvo localmente após a primeira consulta e funciona offline. A distribuição não inclui uma cópia do catálogo completo, pois a fonte não declara licença de redistribuição. Consulte [docs/SPOTLIGHT.md](docs/SPOTLIGHT.md) para origem, contagens e limitações.

## Eventos e notificações

A tela **Eventos** mostra o próximo evento, contagem regressiva, agenda do dia em ordem cronológica e controles de alertas. Os horários iniciais foram fornecidos pela administração no pedido de implementação; o aplicativo não consulta o servidor para confirmar atividade ou alterações.

| Evento | Horários diários | Avisos padrão |
|---|---|---|
| 👻 Fantasminhas Dudu | 00:30, 05:30, 10:30, 15:30, 20:30, 22:30 | 00:25, 05:25, 10:25, 15:25, 20:25, 22:25 |
| ⚔ KVM: Batalha de Midgard | Aberturas às 20:00 e 22:00 | 19:55 e 21:55 |
| 🔥 Maré Infernal | 00:30, 02:30, 04:30, 06:30, 08:30, 10:30, 12:30, 14:30, 16:30, 18:30, 20:30 e 22:30 | Sempre 5 minutos antes, por padrão |

O Dudu acontece em mapa sorteado pelo servidor no momento do evento; o aplicativo não prevê o mapa. O KVM informa a **abertura**, sem presumir o início do combate: times Vermelho e Azul, taxa de 50.000z e acesso pelo botão Batalhas Campais. Os detalhes de cada evento aparecem ao passar o mouse na agenda e no editor.

O fuso inicial é **America/Sao_Paulo**, configurável em **Configurações → Notificações de eventos** ou na própria tela Eventos. Ative/desative todos os avisos, Dudu, KVM e Maré Infernal individualmente, além dos sons. Escolha uma ou mais antecedências: **1, 5, 10, 15 ou 30 minutos**; o padrão é 5. Em **Gerenciar eventos**, edite horários, nome, descrição, ícone, som e antecedências próprias, desative eventos oficiais ou cadastre eventos personalizados. Os oficiais são identificados separadamente e não podem ser excluídos. Atualizações futuras dos padrões locais preservam preferências e horários que você personalizou.

O agendador usa o tick central do aplicativo e recalcula ocorrências pela data e pelo fuso do servidor. Funciona com a janela visível, compacta, minimizada ou na bandeja. Abrir ou retomar o app durante uma janela de aviso permite um único alerta com o tempo restante; antecedências ultrapassadas não são disparadas em sequência. Eventos já iniciados são ignorados. Cada ocorrência e antecedência é registrada antes do envio para evitar repetição após reinício ou mudança do relógio. O histórico mantém ocorrências recentes e é limpo gradualmente após 30 dias.

Os avisos usam a **notificação nativa do Windows**, com o ícone do aplicativo, símbolo e nome do evento, horário e tempo restante. Podem ser dispensados pelos controles do Windows e compartilham a fila dos alertas de mina/MVPs. A aparência, o fechamento e a exibição seguem o sistema operacional; não há uma janela WPF sobre o jogo nem um botão próprio “Entendi”. Sons dos eventos têm controle separado. Foco/Não incomodar e tela cheia exclusiva podem ocultar os avisos. O app não instala serviço ou tarefa agendada: **encerrado completamente, não emite alertas**.

Os arquivos `events.json`, `event-settings.json` e `event-notification-history.json` ficam em **`%APPDATA%\FarmlandCompanion\`**, com escrita atômica e backup. Os dados anteriores de mineração/MVPs/Spotlight e a chave DPAPI permanecem em `%APPDATA%\RagnarokCompanion\`, sem migração destrutiva. O histórico registra encaminhamento à fila do Windows, não comprova que o usuário viu o aviso. Nenhum desses dados pessoais é incluído no ZIP ou no Git.

O ícone original do Companion é incorporado no executável, na janela/barra de tarefas e na bandeja. A arte vetorial fica em `src/RagnarokCompanion/Assets/AppIcon.xaml`; `build/Generate-AppIcon.ps1` regenera o ICO nas resoluções 16–256 px. Não usa imagens do jogo.

A Maré Infernal tem 12 ocorrências diárias e é adicionada automaticamente ao atualizar o aplicativo, preservando horários personalizados, preferências e histórico dos eventos existentes. Compartilha o fuso configurado, reagenda o próximo dia e permite as mesmas antecedências de 1/5/10/15/30 minutos. Em **Gerenciar eventos**, desmarque **Usar antecedências gerais** para escolher seus avisos e ajuste **Som deste evento** para silenciá-la individualmente.

Quando avisos de eventos com o mesmo horário ficam devidos no mesmo ciclo, o aplicativo os reúne em um único pop-up, por exemplo: “Fantasminhas Dudu e Maré Infernal começam em 5 minutos!”. As coincidências oficiais são 00:30, 10:30, 20:30 e 22:30. O histórico JSON registra cada evento individualmente, com ID, nome, horário e antecedência; os identificadores persistidos continuam evitando repetição após reinício. Preferências são revalidadas no envio: eventos desativados saem do grupo, e o som toca somente se o controle geral e pelo menos um membro permitirem. Antecedências diferentes podem produzir avisos em momentos diferentes, respeitando a escolha de cada evento. Avisos expirados são descartados.

## RagnaPlace e catálogo

Ainda não tem uma chave? Veja o [guia para criar sua conta, solicitar a API key e configurá-la no aplicativo](docs/RAGNAPLACE_API.md#como-obter-e-configurar-sua-chave-api).

1. Em **Configurações → Integração RagnaPlace**, informe a API Key no campo mascarado e clique **Proteger e salvar chave**. O campo é limpo após salvar.
2. Clique **Atualizar gateways**, selecione um Renewal ativo e clique **Testar conexão autenticada**.
3. Em **MVPs**, pesquise no catálogo local, selecione o nome e clique na estrela para favoritar, se desejar.
4. Informe a morte em **HH:mm** e clique **Registrar morte**. A data padrão é hoje; use **Dia anterior** quando necessário. Horários futuros são recusados.
5. Para ampliar o catálogo, abra **Configurações → MVPs: respawn, consulta online e seleção do compacto**, use **Buscar online** e **Consultar selecionado**. A paginação é manual.

A chave fica apenas no arquivo `ragnaplace-key.dpapi`, protegida por **DPAPI CurrentUser**, nunca em JSON, URL, exportação ou código. Não é transferível entre usuários/computadores. **Apagar chave** remove a credencial e invalida o cache. Gateways é uma rota pública e não prova autenticação; o teste usa uma consulta autenticada pequena.

Dados online fornecidos por **[RagnaPlace](https://ragnaplace.com)**, para uso pessoal e não comercial conforme as [regras da API](https://ragnaplace.com/pt/api). Não há consultas periódicas, paginação automática, extração em massa nem espelhamento do banco. O [OpenAPI oficial](https://api.ragnaplace.com/v1/openapi.json) foi inspecionado antes de implementar a integração. Consulte [docs/RAGNAPLACE_API.md](docs/RAGNAPLACE_API.md) para rotas, campos, limites e decisões.

O catálogo inicial fornece 16 identidades MVP verificadas no rAthena Renewal, sem presumir mapas ou respawns do Farmland. O recorte de dados em formato fonte e a licença GPL-3.0-or-later acompanham `Data/` e `licenses/`. Esta versão reconhece URLs de imagem no contrato, mas não baixa/distribui sprites do jogo.

## Farmland, tempos e favoritos

O **Farmland é Renewal sem terceiras classes**. Um gateway oficial não representa seus tempos ou configurações. A interface diferencia dados RagnaPlace/gateway, informação não verificada, personalizado e Farmland confirmado **pelo usuário**.

Quando faltar respawn confiável, o registro pede somente mínimo e máximo opcional; deixar o máximo vazio define um tempo fixo. Depois, os próximos registros reutilizam essa configuração. Ajustes detalhados ficam em Configurações. O mapa é opcional se desconhecido. Use mínimo igual ao máximo para respawn fixo, ou dois limites para uma janela variável. Os valores são minutos, acima de zero e até 10080. Marque a confirmação Farmland somente quando tiver verificado esses tempos. As configurações locais têm prioridade sobre valores genéricos do mesmo monstro/mapa recebidos da API.

O contrato fornece um único respawn em segundos por mapa, sem máximo variável. O app converte a unidade e exige aceitação explícita para usar essa referência no timer; não inventa um máximo ou presume que ela vale no Farmland. As previsões não confirmam que o monstro está vivo.

Clique na estrela ao lado do nome para favoritar ou desfavoritar sem iniciar um timer. A busca é imediata e mostra favoritos primeiro. Favoritos e tempos personalizados funcionam offline. Um novo horário substitui a morte do mesmo ponto; corrigir para uma morte mais antiga pede confirmação. Cadastro manual, edição, filtros avançados, respawn e consulta RagnaPlace ficam em Configurações. O histórico mantém mortes, correções e alertas.

## Painel e compacto na mesma janela

**Modo compacto ↗** reduz a própria `MainWindow`; **Expandir ↗** a expande. Nenhuma segunda janela de timers é criada. As trocas de modo e consultas assíncronas preservam estado e timestamps.

O compacto tem quatro abas fixas: **⛏ Mina**, **☠ MVPs**, **🔔 Eventos** e **☀ Spot** (SpotLight). Somente o módulo selecionado é exibido. A última aba fica salva em `settings.json` e é preservada ao expandir, compactar e reiniciar. Trocar de aba não reinicia cronômetros, não muda a seleção automaticamente e não interrompe notificações.

- **Mina:** status, cronômetro grande, progresso, próxima entrada e botão de entrada quando disponível. Mantém os ciclos de 30 + 60 minutos e durações já configuradas.
- **MVPs:** até cinco favoritos, com timers ativos primeiro em ordem do próximo respawn; os demais mostram “Sem horário”. Se houver mais de cinco favoritos, escolha quais exibir em Configurações. **Limpar MVPs** pede confirmação e limpa todos os timers de MVP, preservando favoritos, catálogo, tempos configurados e histórico.
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
- `ragnaplace.json`: gateway e última consulta bem-sucedida, sem chave.
- `ragnaplace-key.dpapi`: credencial cifrada por usuário do Windows.

Preferências são salvas automaticamente; textos são aplicados ao perder foco. JSONs usam arquivo temporário, flush e substituição atômica por arquivo com `.bak`. Arquivos inválidos são preservados como `.invalid-<timestamp>` e recuperados da cópia anterior ou padrões. Não há transação única entre os arquivos: uma interrupção entre gravações pode deixar o histórico uma revisão atrás do timer.

O cache de respostas da API é **somente em memória**, por dois minutos e até 100 entradas. Não foi encontrada permissão explícita para redistribuir/cachear o banco em disco; somente escolhas operacionais são persistidas. Itens consultados e não favoritados/configurados podem precisar de nova consulta em outra sessão. A última consulta bem-sucedida pode ser atendida pelo cache; não significa atualização de todo o catálogo.

Importação/exportação preserva o formato anterior: preferências principais e cadastros/timers MVP, sem chave. Para backup completo, copie também os JSONs de favoritos e gateway. Cadastre novamente a credencial em outro usuário/computador. Importar não apaga o ciclo da mina nem o histórico.

## Gepard Shield

O programa não acessa memória/processos do jogo, não injeta DLLs, não captura pacotes, não usa hooks de teclado/mouse e não automatiza ações. Usa janela WPF comum e `Topmost`, sem overlay gráfico. A comunicação online ocorre com RagnaPlace quando solicitada.

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

A integração é coberta por testes HTTP simulados. Para confirmar a conta sem alterar timers ou preferências, use Configurações → Conexão com o RagnaPlace → Testar conexão autenticada, ou execute o aplicativo com --check-ragnaplace. O diagnóstico usa a chave DPAPI já armazenada, valida o gateway Renewal, faz uma busca e consulta detalhes de um MVP, com até três requisições limitadas e sem exibir credenciais. O resultado fica em %TEMP%\RagnarokCompanion-validation\ragnaplace-check.txt, ou no arquivo passado em --check-output=CAMINHO. O teste de publicação nunca usa a API real.

## Interface simplificada

O smoke WPF alterna as quatro abas compactas, verifica que cada uma mostra apenas seu módulo, testa o limite de cinco favoritos, registro em HH:mm e limpeza dos timers com preservação de dados, preservação de serviços/timestamps, notificações em aba inativa e restauração da aba após reinício. Também gera PNGs em 96, 120, 144 e 192 DPI (100%, 125%, 150% e 200%). Essas renderizações não substituem um teste físico de troca entre monitores com escalas diferentes. O manifesto declara [PerMonitorV2 conforme a documentação do Windows](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process); mudanças de DPI solicitam novo ajuste do compacto.

A navegação tem Início, MVPs, Histórico, Configurações, SpotLight e Eventos. Em MVPs, o fluxo é buscar, selecionar, favoritar se desejar e registrar a morte em HH:mm. A tela contém uma lista e um único formulário; os controles avançados ficam em Configurações. O Início oferece acesso direto à pesquisa de MVPs. O compacto adapta a altura à aba e às linhas exibidas e restaura o tamanho anterior ao expandir.

## Publicação: somente latest

### Releases no GitHub

A Action [Release Windows](https://github.com/RodCas11/FarmlandCompanion/actions/workflows/release.yml) compila no Windows, executa os testes e o smoke WPF offline, gera o ZIP e seu SHA256 e publica uma release quando uma tag `v*` é enviada. A tag deve corresponder à `<Version>` do projeto, por exemplo `v4.0.0` para `4.0.0`.

Para publicar uma nova versão, atualize a versão do projeto, faça commit usando gitmoji e envie uma nova tag:

```powershell
git tag -a v4.0.1 -m "🚀 Release v4.0.1"
git push origin v4.0.1
```

O botão **Run workflow** na Action gera o pacote como artifact para conferência, sem criar uma release quando executado na branch `main`. O workflow usa o token automático do GitHub; não precisa de API key RagnaPlace nem de credenciais pessoais nos secrets.

Para gerar o mesmo pacote localmente:

```powershell
powershell -ExecutionPolicy Bypass -File build-release.ps1
```

O resultado fica em `artifacts/release/FarmlandCompanion-win-x64.zip`, com `FarmlandCompanion-win-x64.sha256` ao lado. O ZIP contém o executável self-contained, README, guias, licenças e os dados fonte do catálogo inicial. Código do projeto, SDK, símbolos de depuração, testes, relatórios, caches e credenciais não são incluídos. O SHA256 também é conferido pela Action antes de publicar a release.

### Build local segura

Execute .\build-latest.ps1 (se a política local impedir scripts, use powershell -ExecutionPolicy Bypass -File build-latest.ps1). O fluxo compila Release, executa todos os testes, publica em diretório temporário, verifica o pacote e abre o novo executável em smoke test isolado antes de substituir dist/latest/FarmlandCompanion.exe. build.ps1 é um atalho para o mesmo fluxo; não permite pular testes nem criar executáveis com nomes diferentes.

A substituição usa um manifesto SHA256 e restaura a distribuição anterior se a troca falhar. A build antiga não é apagada se compilação, testes, publicação ou smoke test falharem. Arquivos em uso interrompem a troca com mensagem: use Sair na bandeja e tente novamente. Arquivos desconhecidos/alterados, diretórios sem identificação e links/junções são preservados. Dados do usuário em APPDATA e credenciais nunca são alvo da limpeza. Não são alterados código-fonte, tags, releases ou histórico Git.

A migração dos antigos executáveis da raiz de dist usa uma lista explícita de hashes dos artefatos gerados anteriormente pelo projeto. Nenhum arquivo é reconhecido apenas pela extensão. O relatório de remoções e preservações fica em artifacts/publication.json; testes e screenshots ficam fora de dist. Não edite/adicione arquivos dentro de latest: se o conteúdo não corresponder ao manifesto, o script preserva o diretório e interrompe a substituição para revisão.
