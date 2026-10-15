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

## RagnaPlace e catálogo

Ainda não tem uma chave? Veja o [guia para criar sua conta, solicitar a API key e configurá-la no aplicativo](docs/RAGNAPLACE_API.md#como-obter-e-configurar-sua-chave-api).

1. Em **Configurações → Integração RagnaPlace**, informe a API Key no campo mascarado e clique **Proteger e salvar chave**. O campo é limpo após salvar.
2. Clique **Atualizar gateways**, selecione um Renewal ativo e clique **Testar conexão autenticada**.
3. Em **Catálogo de MVPs**, digite um nome e clique **Pesquisar no RagnaPlace**. A navegação entre páginas é manual.
4. Selecione um resultado e clique **Detalhes do resultado selecionado**. A busca pode trazer monstros comuns; os detalhes confirmam MVP por `exp.mvp` positivo e mostram entradas separadas por mapa.
5. Selecione o MVP e clique **Morreu agora**, ou informe uma morte antiga em `dd/MM/aaaa HH:mm:ss` (hora local, sem data futura).

A chave fica apenas no arquivo `ragnaplace-key.dpapi`, protegida por **DPAPI CurrentUser**, nunca em JSON, URL, exportação ou código. Não é transferível entre usuários/computadores. **Apagar chave** remove a credencial e invalida o cache. Gateways é uma rota pública e não prova autenticação; o teste usa uma consulta autenticada pequena.

Dados online fornecidos por **[RagnaPlace](https://ragnaplace.com)**, para uso pessoal e não comercial conforme as [regras da API](https://ragnaplace.com/pt/api). Não há consultas periódicas, paginação automática, extração em massa nem espelhamento do banco. O [OpenAPI oficial](https://api.ragnaplace.com/v1/openapi.json) foi inspecionado antes de implementar a integração. Consulte [docs/RAGNAPLACE_API.md](docs/RAGNAPLACE_API.md) para rotas, campos, limites e decisões.

O catálogo inicial fornece 16 identidades MVP verificadas no rAthena Renewal, sem presumir mapas ou respawns do Farmland. O recorte de dados em formato fonte e a licença GPL-3.0-or-later acompanham `Data/` e `licenses/`. Esta versão reconhece URLs de imagem no contrato, mas não baixa/distribui sprites do jogo.

## Farmland, tempos e favoritos

O **Farmland é Renewal sem terceiras classes**. Um gateway oficial não representa seus tempos ou configurações. A interface diferencia dados RagnaPlace/gateway, informação não verificada, personalizado e Farmland confirmado **pelo usuário**.

Quando faltar respawn confiável, configure somente os tempos no cartão de detalhes. O mapa é opcional se desconhecido. Use mínimo igual ao máximo para respawn fixo, ou dois limites para uma janela variável. Os valores são minutos, acima de zero e até 10080. Marque a confirmação Farmland somente quando tiver verificado esses tempos. As configurações locais têm prioridade sobre valores genéricos do mesmo monstro/mapa recebidos da API.

O contrato fornece um único respawn em segundos por mapa, sem máximo variável. O app converte a unidade e exige aceitação explícita para usar essa referência no timer; não inventa um máximo ou presume que ela vale no Farmland. As previsões não confirmam que o monstro está vivo.

Use **Favoritar / desfavoritar** para salvar seleções. Favoritos e tempos personalizados funcionam offline, inclusive no seletor do modo compacto. Um favorito previamente usado pode reutilizar a configuração de seu timer sem nova consulta. O **MVP Tracker** preserva cadastro/edição/exclusão manual, múltiplos timers, busca, filtro por mapa e correção da última morte. O histórico registra mortes, ciclos, correções e alertas.

## Painel e compacto na mesma janela

**Ativar modo compacto** reduz a própria `MainWindow`; **Painel ↗** a expande. Nenhuma segunda janela de timers é criada. As trocas de modo e consultas assíncronas preservam estado e timestamps.

Arraste pelo título, redimensione pelo canto/bordas e use os botões de minimizar/fechar. No compacto, acompanhe mina/cooldown e próximos MVPs, registre mortes e selecione favoritos. A fixação usa exclusivamente `Topmost` do WPF. Nas Configurações ajuste fonte, opacidade de 50% a 100%, exibição de mina/MVPs/ambos, limite 1/3/5 e abertura automática ao iniciar app/timer. Posição/tamanho são salvos; coordenadas fora dos monitores são recuperadas.

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

Gera relatório e PNGs fora da distribuição, em `%TEMP%\RagnarokCompanion-validation` (ou no diretório passado em `--smoke-output=CAMINHO`). Exercita cinco páginas, Spotlight, modos da mesma janela, timers, bandeja, recuperação de posição, favoritos, configuração Farmland, persistência e envio de notificação nativa. Os testes HTTP usam mocks e não consomem a API real.

A integração é coberta por testes HTTP simulados. Para confirmar a conta sem alterar timers ou preferências, use Configurações → Conexão com o RagnaPlace → Testar conexão autenticada, ou execute o aplicativo com --check-ragnaplace. O diagnóstico usa a chave DPAPI já armazenada, valida o gateway Renewal, faz uma busca e consulta detalhes de um MVP, com até três requisições limitadas e sem exibir credenciais. O resultado fica em %TEMP%\RagnarokCompanion-validation\ragnaplace-check.txt, ou no arquivo passado em --check-output=CAMINHO. O teste de publicação nunca usa a API real.

## Interface simplificada

A navegação tem Início, MVPs, Histórico, Configurações e SpotLight. No Início, inicie a mineração quando disponível, pesquise/selecione um MVP e use Morreu agora ou expanda Informar outro horário. Favoritos aparecem primeiro. Os próximos eventos unem mina, cooldown e respawns em ordem cronológica. Em MVPs, cada cartão registra morte com um clique; quando falta respawn, somente essa configuração é aberta, com Salvar e registrar morte. Cadastro manual, correções e configurações avançadas permanecem em seções expansíveis. O compacto adapta a altura ao número de timers e restaura o tamanho anterior ao expandir.

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
