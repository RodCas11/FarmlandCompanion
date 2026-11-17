# Builds comunitárias

O editor funciona offline e guarda rascunhos em `%APPDATA%/RagnarokCompanion/builds.sqlite`, separado do catálogo de itens. Salva automaticamente após a edição e ao sair. A seleção inicial é Lorde / Early / PvM. Trocar finalidade não cria uma combinação extra.

São **18 classes e 54 combinações**, conforme a confirmação do usuário: transclasses, Superaprendiz, Ninja, Justiceiro, Mestre Taekwon e Espiritualista. Não inclui Taekwon básico nem terceiras classes. Fonte: [wiki Farmland](https://farmland.net.br/wiki/index.php/Classes).

## Editor e critérios

Clique em um slot, pesquise por nome português, inglês ou ID e use o item escolhido. A busca alterna entre equipamento, carta e encantamento. Refino, grade, cartas/encantamentos por ID e Random Options (uma por linha) são editáveis. Nenhum bônus é calculado ou inventado. Os textos das opções devem reproduzir os dados reais do [Farmland](https://farmland.net.br/wiki/index.php/Random_Options).

- Early: slots normais obrigatórios preenchidos; não exige refino.
- Mid: normais e visuais preenchidos; todos os visuais exigem Random Options.
- End: normais grade B ou superior e no mínimo +9; Shadows +10; normais e Shadows com Random Options; três opções em cada visual.
- Armas de duas mãos dispensam escudo normal e impedem equipar um escudo simultâneo. Equipamentos que ocupam mais de uma posição de cabeça cobrem os slots correspondentes.
- Cartas e encantamentos adicionais obrigatórios podem ser definidos pelo ADM, por slot, em `build-criteria.json` no armazenamento pessoal (`BuildCriteria`). Essa configuração muda apenas a validação do rascunho. O backend deverá sempre aplicar os seus próprios critérios oficiais.

A compatibilidade usa um catálogo derivado do rAthena Renewal (somente ID, tipo, slots, localização e restrições de classe). Alterações exclusivas do Farmland exigem metadados autorizados pelo ADM; um item desconhecido pode ficar salvo como rascunho, mas não é considerado confirmado/completo. Os scripts YAML não são executados nem incluídos no pacote. Gerador: `--import-build-catalog --equipment=... --etc=... --output=...`; fonte: `db/re/item_db_equip.yml`, `db/re/item_db_etc.yml` do [rAthena](https://github.com/rathena/rathena). Sprites de classe: [iRO Wiki](https://irowiki.org/wiki/Category:Job_Images), gráficos Gravity; imagens de itens: Divine Pride. Cache local por imagem, sem chamadas à API no hover.

## Firebase integrado

Login Google pelo navegador, Firestore protegido por regras e cache SQLite. Implantação, autorização de administradores e liberação de publicações estão em [FIREBASE.md](FIREBASE.md). Não há autoaprovação ou código de recompensa local.

## Telas e recuperação de builds

O módulo possui três áreas: **Editor**, **Minhas Builds** e **Comunidade**. Os filtros da comunidade são independentes da classe/estágio/finalidade em edição. Minhas Builds oferece abrir, duplicar, renomear e excluir rascunhos; o status "completa localmente" não é aprovação. Publicações próprias podem ser consultadas e editadas após login; a estrutura de avaliações está protegida pelas regras e ainda não possui interface.

Ao abrir uma build, a seleção é capturada antes da atualização da lista. Coleções e componentes ausentes são normalizados para permitir carregamento parcial. Dados originais problemáticos são preservados em `%APPDATA%/RagnarokCompanion/build-recovery`; diagnósticos ficam em `%APPDATA%/RagnarokCompanion/logs/builds.log`. Os registros não contêm tokens Firebase.

## Random Options contextuais

O catálogo `Data/build-random-options.json` deriva das tabelas públicas da [wiki Farmland, revisão 5998](https://farmland.net.br/wiki/index.php?title=Random_Options&oldid=5998). Armas/acessórios usam sua tabela específica; topo, meio, baixo, armaduras, capas, calçados, visuais e Shadows usam a tabela dos demais equipamentos. Equipamentos comuns têm até cinco linhas (a wiki descreve três positivas e duas negativas); visuais mantêm o limite de três. Perfis de forja para armas possuem faixas próprias por ranking, selecionadas explicitamente, sem presumir que toda arma é forjada. As sugestões só aparecem após identificar um item compatível, e os valores adicionados precisam estar na faixa publicada. Linhas manuais permanecem editáveis para revisão do ADM.

Regras específicas conhecidas por item podem ser fornecidas em `build-random-option-rules.json`, no armazenamento pessoal: lista de objetos `ItemId`, `AllowedSlots` e `AllowedOptionIds`. Use IDs do catálogo de opções e IDs de slots de `BuildCatalog`. Uma lista vazia impede sugestões para o item; essas regras locais nunca aprovam builds nem alteram o progresso do servidor.

Corre��es locais de compatibilidade: M�scaras do Tengu Corvo 19456 e 19457 usam Meio, todas as classes e um socket conforme os dados fornecidos do Farmland. Armas com localiza��o Both_Hand s�o aceitas no slot Arma e dispensam o escudo; n�o s�o oferecidas como escudos.


A comunidade oferece Ver build: consulta visual em modo somente leitura, sem criar rascunho; copiar para o editor � uma a��o separada. Aprova��es reutiliza o mesmo painel com classe, est�gio, finalidade, atributos e grupos Normal/Visual/Shadow. Os cards usam os tooltips completos da configura��o enviada, com refino, grade, cartas, encantamentos e Random Options.


Editor 4.24: Equipamentos e Visuais / Shadows s�o as duas visualiza��es. Clicar no slot abre um modal com c�pia tempor�ria; Salvar aplica somente o slot editado, Cancelar/Esc descarta. Limpar slot tamb�m depende de Salvar. Arma de duas m�os libera o escudo somente ao confirmar. Ovo de Mascote � opcional em Early/Mid/End, sem Loyalty, com tipos PetEgg derivados do cat�logo p�blico e imagens/descri��es do cat�logo existente.
