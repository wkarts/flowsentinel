# Changelog


Todas as alterações relevantes deste projeto serão documentadas neste arquivo.
O formato segue Keep a Changelog e o projeto utiliza versionamento semântico.

## [Não publicado]

## [0.4.4] - 2026-07-29

### Corrigido

- Corrigido o `OverflowException` na primeira atualização do splash, causado pela subtração entre `Stopwatch.Elapsed` e `TimeSpan.MinValue`.
- O marcador da atualização visual passou a ser anulável; a primeira atualização ocorre sem subtração e as seguintes mantêm o intervalo de 250 ms.
- O tempo decorrido é capturado uma única vez por iteração, mantendo as verificações de atualização e timeout consistentes.
- A tarefa da etapa de inicialização é cancelada quando ocorre timeout ou outra falha, evitando trabalho residual após o fechamento do splash.
- Adicionados testes de regressão para primeira atualização, intervalo de 250 ms e `TimeSpan.MaxValue`.

### Compatibilidade

- Nenhuma automação, modelo, canal, contato, histórico, banco ou regra de pendência foi alterada.
- A correção é cumulativa sobre a versão 0.4.3 e não exige excluir `flowsentinel.db`.

## [0.4.3] - 2026-07-29

### Corrigido

- Corrigido o bloqueio do Desktop durante o splash, mantendo criação e message loop do WinForms na mesma thread STA.
- A inicialização do banco, dos catálogos e do host passa a possuir acompanhamento visual e limites de tempo, evitando espera indefinida.
- Removida a corrida entre a migração explícita do banco e os serviços em segundo plano.
- Aplicado timeout de espera de 15 segundos a todas as conexões SQLite para tratar contenção transitória sem falha imediata.
- Corrigida a baixa visibilidade da marca no painel escuro do splash com um ativo branco dedicado.
- Eliminadas consultas e gravações SQLite por registro e por ação nos ciclos sem mudanças.
- Reduzida a atualização massiva de `LastEvaluatedAt` para uma janela consolidada de cinco minutos.
- Removidos, durante a migração, somente estados de ação inativos e sem histórico produzidos pelas versões 0.4.1/0.4.2.
- Filtrados logs informativos do Entity Framework e do `HttpClient`, mantendo avisos e erros e reduzindo contenção de disco.

### Desempenho

- Ocorrências abertas e estados das ações são carregados uma única vez por execução da automação.
- Registros sem alteração deixam de produzir `UPDATE` individual no banco.
- A migração de datas antigas passa a usar SQL em lote somente nas linhas com fuso explícito.
- Agendador e processador de entregas aguardam dois segundos após a abertura para permitir que a interface seja exibida antes da carga de trabalho.

### Compatibilidade

- Nenhuma automação, ocorrência, entrega, contato, grupo, modelo ou regra de pendência é removida.
- Não é necessário excluir ou recriar o banco existente.
- Permanecem compatíveis as automações das versões 0.3.0 a 0.4.2.

## [0.4.2] - 2026-07-29

### Corrigido

- Corrigido o teste `DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais`, que validava destaque visual sem habilitar explicitamente `IncludeFormatting`; o comportamento de produção permanece opt-in e não foi alterado.
- Mantida a política segura da 0.4.x: formatação e agregações somente são processadas quando configuradas.

### Validado

- Adicionado teste de regressão confirmando que pendências recorrentes aceitam letra, número, palavra ou frase, como `A`, `7`, `AGUARDANDO DOCUMENTAÇÃO` e `Em análise`.
- Confirmado que `P` e `X` são apenas valores iniciais do assistente, não códigos fixos do motor.
- Preservada a compatibilidade das automações 0.3.0, 0.4.0 e 0.4.1.

## [0.4.1] - 2026-07-29

### Corrigido

- Corrigidos os três testes de infraestrutura que falhavam no CI após a alteração segura do padrão `GenerateAggregateRecords` para `false`; os testes que validam agregações agora habilitam essa capacidade explicitamente.
- Mantido o padrão seguro da versão 0.4.0: indicadores agregados continuam opcionais e não voltam a gerar cascatas de notificações automaticamente.
- Preservada a compatibilidade das automações existentes, inclusive chaves de idempotência legadas, aliases de planilha e modelos de notificação anteriores.

### Adicionado

- Ciclo de ação persistente com condições independentes de ativação, permanência e conclusão.
- Lembretes recorrentes enquanto uma pendência permanecer sem atingir o valor esperado.
- Encerramento automático do ciclo e cancelamento seletivo das entregas pendentes da ação concluída.
- Reinício controlado da contagem quando a mesma pendência voltar a ocorrer, com número de episódio persistido.
- Agenda por ação com dias da semana, horário inicial, horário final e suporte a janelas que atravessam a meia-noite.
- Configuração visual de pendências no assistente de planilhas e no editor avançado de ações.
- Histórico persistente resumido de cada execução e histórico detalhado das mudanças de registros, complementando o histórico já existente de ocorrências e entregas.
- Testes de regressão para agenda, serialização de ações antigas, estado dos episódios, cancelamento seletivo, histórico e ciclo completo `P → X → P`.

### Alterado

- Persistência SQLite atualizada de forma aditiva para a versão interna 5, sem recriar o banco nem remover dados existentes.
- Ações `WhileActive` podem optar por avaliação na abertura da ocorrência; o assistente habilita isso somente nos novos lembretes de pendência, mantendo ações antigas inalteradas.

### Corrigido

- Corrigido o erro de compilação `CS0103` em `ExcelSectionedMatrixParser`, restaurando o acesso às configurações da matriz na geração de agregados por entidade.
- Adicionado teste de regressão para garantir que agregados `EntitiesByCurrentValue` utilizem o nome plural configurado da entidade, sem fixar o domínio da planilha.
- Eliminado o conflito entre o namespace `FlowSentinel.Application` e `System.Windows.Forms.Application`, corrigindo os erros `CS0234` em `Program`, `MainForm`, `TrayApplicationContext` e `StartupRegistration`.
- Substituída a propriedade de estado `AllowClose` por campo privado e método interno, removendo definitivamente a origem do `WFO1000`.
- Consolidada a correção de validação em um pacote cumulativo para evitar aplicação parcial de patches anteriores.
- Corrigida a atribuição de parâmetros SQL nulos em `DatabaseSourceReader`, eliminando o erro de compilação `CS0019`.
- Definido `FlowSentinel.Infrastructure` como `net10.0-windows`, refletindo o uso intencional do Windows DPAPI e eliminando avisos `CA1416`.
- Adicionada permissão `actions: read` ao workflow CodeQL para leitura de metadados das execuções.

- Corrige o erro `WFO1000` do analisador WinForms, restringindo propriedades internas de estado e resultado dos formulários.
- Remove a referência redundante a `System.Text.Encoding.CodePages` no .NET 10, eliminando o aviso `NU1510`.
- Atualiza e fixa a família `SQLitePCLRaw` em `2.1.12`, removendo a dependência nativa vulnerável `2.1.11` (`GHSA-2m69-gcr7-jv3q`).

### Planejado

- Editor visual avançado de grupos lógicos.
- Conectores adicionais por plugins externos.
- Administração centralizada opcional.

## [0.4.0] - 2026-07-29

### Adicionado

- Assistente profissional de planilhas em seis etapas: origem, mapeamento, pré-visualização, eventos, notificações e revisão.
- Pré-visualização visual das abas com seleção de linhas, colunas, cabeçalho e área monitorada.
- Menu **Modelos** com perfis para RP-102, matrizes periódicas, tarefas, documentos e configuração personalizada.
- Políticas de entrega por canal: individual, agrupada por registro ou resumo único.
- Catálogo central de contatos e grupos, com endereços por canal, permissões por automação e comandos diretos de criação, importação e exportação JSON/CSV no menu **Contatos**.
- Seleção de contatos, grupos e destinatários manuais diretamente no assistente de planilhas e no editor avançado de ações.
- Splash screen com estágios reais de inicialização para armazenamento, automações, canais, contatos e grupos.

### Alterado

- A janela principal foi reorganizada com menu estruturado, barra de ações, central de monitoramento e submenu de modelos.
- Indicadores agregados e monitoramento de formatação passaram a ficar desativados por padrão no assistente.
- Notificações locais do Windows são sempre individuais, independentemente da política dos canais externos.
- Grupos incorporados à automação foram mantidos somente para compatibilidade e identificados como recurso legado.
- O texto introdutório específico e pouco profissional do modelo RP-102 foi removido.

### Corrigido

- Eliminada a geração padrão da cascata de mensagens `ValuesByPeriod` e `EntitiesByCurrentValue` nas automações RP-102.
- A atualização do armazenamento desativa a ação agregada legada da RP-102, remove agregações automáticas da fonte e cancela entregas agregadas ainda pendentes.
- Canais removidos, desabilitados ou incompatíveis deixaram de criar novas entregas e de ser contabilizados como falhas operacionais.
- A migração interna v4 reclassifica como `Skipped` as entregas pendentes, em retentativa, em processamento ou anteriormente falhas que pertençam a canais removidos ou desabilitados.
- Restaurado o acesso às configurações da matriz na geração de agregados, corrigindo `CS0103` em `ExcelSectionedMatrixParser`.
- Corrigida a resolução assíncrona de destinatários para contatos e grupos do catálogo central.

## [0.3.0] - 2026-07-28

### Adicionado

- Modelo guiado RP-102 opcional e editor visual genérico para criar um único monitoramento de toda uma planilha estruturada.
- Análise estrutural antes da criação, com quantidade de entidades, grupos, valores monitorados, abas e avisos.
- Modo Excel `SectionedMatrix` configurável para reconhecer vários blocos, cabeçalhos repetidos, entidades, responsáveis e colunas de acompanhamento na mesma aba.
- Seleção automática da aba anual mais recente, seleção fixa ou leitura de todas as abas compatíveis.
- Registros normalizados por entidade e por célula de valor, sem exigir uma automação por item.
- Cálculo configurável do valor atual por último preenchido ou calendário definido pelo usuário, com exclusões opcionais.
- Indicadores de entidades por valor atual e células por valor/período.
- Agregações gerais, por grupo/categoria e por responsável.
- Painel administrativo com visualização semelhante à planilha original, preservando posição, conteúdo, larguras de colunas, alturas de linhas, cores e destaques.
- Filtros por tipo, entidade, código, grupo, responsável, período e valor.
- Linha de base local e comparação detalhada de inclusões, remoções e alterações.
- Legenda administrativa configurável para códigos de situação.
- Notificações específicas para mudança de valor, valor atual, responsável, quantidades, cores e destaques.
- Suporte a variáveis `{{previous.Campo}}` nos templates de mensagens.
- Reconhecimento configurável de listas especiais sem código, responsável ou períodos.
- Testes para matrizes contábeis opcionais e para estruturas genéricas de outros domínios.

### Corrigido durante a validação do PR

- Corrigido o teste de detecção de células destacadas, tornando a leitura de preenchimento compatível com cores de fundo e padrão.
- Eliminados os avisos de nulabilidade no parser, no assistente, nas fontes do sistema e nos testes.
- Removidos do núcleo do parser os rótulos contábeis fixos, meses, `BAL`, `EMPRESAS`, `SIMPLES` e `SEM MOVIMENTO`.
- Corrigida a detecção de cabeçalho quando o marcador está vazio, evitando classificar linhas comuns como cabeçalho.
- Adicionados aliases genéricos `Entity`, `EntityKey`, `Owner`, `Category`, `Value` e `CurrentValue`, preservando aliases legados.
- Adicionados nomes administrativos configuráveis para entidade, responsável, grupo, período, código e valor.
- Tornado explícito na interface que RP-102 é apenas um modelo opcional.
- Adicionado teste de matriz genérica de equipamentos sem vocabulário contábil.

### Alterado

- A tela principal passou a destacar o painel de planilhas; o atalho RP-102 é identificado como modelo opcional.
- O leitor Excel de tabela simples foi preservado e passou a coexistir com o modo administrativo.
- A versão do produto, instalador e pipeline foi atualizada para `0.3.0`.

## [0.2.3] - 2026-07-28

### Adicionado

- Splash screen leve com status de inicialização, versão do produto e identidade do desenvolvedor.
- Tela Sobre com informações da WWSoftware's Sistemas e Tecnologias, Wallace Kleiton, GitHub, WhatsApp e e-mail.
- Logo institucional do desenvolvedor em PNG e ICO, mantida separada da identidade visual principal do FlowSentinel.
- Tela central de configurações para comportamento do Desktop, inicialização, tray, processamento, dados, logs e Windows Service.
- Inicialização automática por usuário com os argumentos `--startup --tray`.
- Controle visual para instalar, atualizar, iniciar, parar, remover e consultar o Windows Service.
- Parâmetros dinâmicos do agendador e da fila de entregas para Desktop e serviço.
- Inclusão do binário e dos scripts do Windows Service nos pacotes Desktop e nos instaladores.
- Menu Configurações e Sobre na janela principal e no tray.

### Alterado

- O fechamento da janela agora respeita a política configurada: minimizar, perguntar ou encerrar.
- Os workers deixaram de utilizar intervalos fixos e passam a consultar os parâmetros de execução em tempo real.
- O instalador registra a inicialização automática diretamente no tray e inclui os arquivos necessários para administrar o serviço.
- Metadados de autoria e publicação atualizados para WWSoftware's Sistemas e Tecnologias / Wallace Kleiton.

## [0.2.2] - 2026-07-28

### Corrigido

- Corrigida a exceção `ArgumentOutOfRangeException` ao abrir os CRUDs visuais quando o `ComboBox` possuía `DataSource`, mas sua coleção `Items` ainda não havia sido materializada pelo WinForms.
- As listas fixas de tipos, fontes, provedores, ações e políticas passaram a ser carregadas diretamente em `ComboBox.Items`, sem dependência de `BindingContext`.
- A seleção visual agora calcula e aplica o índice somente sobre a coleção real do controle, com validação dos limites antes de alterar `SelectedIndex`.
- Adicionados testes de regressão para ComboBox com DataSource não inicializado e seleção imediata de itens materializados.

## [0.2.1] - 2026-07-28

### Corrigido

- Corrigida a abertura dos formulários visuais quando os `ComboBox` ainda não materializaram seus itens vinculados.
- Removidas seleções com `First(...)` que causavam `Sequence contains no matching element` ao criar ou editar automações, canais, fontes, regras e ações.
- Adicionada seleção tolerante com fallback para valores legados, desconhecidos ou temporariamente indisponíveis.
- Endurecidos os CRUDs visuais contra linhas desatualizadas ou itens removidos durante a edição.
- Adicionado tratamento global de exceções da interface para impedir a caixa de depuração JIT e manter a aplicação aberta.
- Adicionado registro de falhas inesperadas em `unhandled-ui.log`.
- Adicionados testes de regressão para seleção de valores, fallback legado e listas vazias.

## [0.2.0] - 2026-07-28

### Adicionado

- Assistente visual para criação e edição de automações sem necessidade de escrever JSON.
- Cadastro visual de fontes Excel, CSV, TXT, SQLite, SQL Server, MySQL, PostgreSQL e Firebird.
- Seleção de arquivos, listagem de abas do Excel, pré-visualização de registros e sugestão de campos-chave.
- Teste de fontes e consultas antes da ativação.
- Editor visual recursivo de critérios com grupos E/OU aninhados e negação.
- Critérios separados de abertura, permanência, conclusão e suspensão.
- Cadastro visual de ações, repetição, múltiplos canais, múltiplos destinatários e templates.
- Cadastro visual de grupos de contatos com endereços por WhatsApp, e-mail e Telegram.
- Formulários amigáveis para Evolution API V1/V2, Telegram, SMTP, Gmail, Outlook/Hotmail e Microsoft 365.
- Proteção automática de tokens, senhas e connection strings com Windows DPAPI.
- Serviço de design de fontes para amostra de dados e descoberta de abas.
- Teste de integração para a pré-visualização de fontes CSV.

### Alterado

- O botão principal de edição agora abre o assistente visual; o editor JSON permanece como modo avançado.
- A versão do produto, instalador e pipeline foi atualizada para `0.2.0`.

### Corrigido

- Corrigida a referência ao catálogo de canais no editor de ações, eliminando o erro de compilação `CS0103`.
- Ajustado o carregamento visual de destinatários com canal opcional, eliminando o aviso de nulabilidade `CS8604`.
- Ajustado o carregamento de parâmetros SQL nulos na grade do editor de fontes, eliminando o aviso de nulabilidade `CS8604`.

## [0.1.1] - 2026-07-28

### Corrigido

- Removido o uso de `DateTimeOffset` nas entidades persistidas pelo SQLite, mantendo `DateTimeOffset` apenas nos contratos públicos da aplicação.
- Datas persistidas passaram a ser normalizadas em UTC com `DateTime`, permitindo comparação, ordenação e agregações no servidor SQLite.
- Corrigidas as consultas de painel e agendamento que utilizavam `MaxAsync` sobre `DateTimeOffset` e causavam erro em toda atualização da interface.
- Adicionada migração interna idempotente baseada em `PRAGMA user_version` para normalizar datas já gravadas sem apagar o banco existente.
- Adicionados testes de integração do `FlowStore` cobrindo painel, automações vencidas, agregação de ações e fila de entregas no SQLite real.

## [0.1.0] - 2026-07-28

### Corrigido

- Inicialização de `DataSourceDefinition.Configuration` com um objeto JSON vazio válido, evitando `InvalidOperationException` na serialização de fontes sem configuração explícita.
- Teste de serialização atualizado para validar o `JsonElement` padrão e a persistência dos enums como texto.
- Workflow de testes ajustado para gerar diretórios e arquivos TRX independentes por projeto, eliminando a sobrescrita dos resultados.

### Adicionado

- Aplicação Windows em bandeja e executável de Windows Service.
- Motor de regras com grupos AND/OR aninhados.
- Critérios independentes de entrada, permanência, suspensão e conclusão.
- Monitoramento de XLSX, CSV, TXT e bancos SQL Server, MySQL, PostgreSQL, Firebird e SQLite.
- Correlação de registros entre múltiplas fontes pela chave composta.
- Múltiplos canais, destinatários, templates e ações por automação.
- Evolution API V1/V2 configurável, Telegram Bot, SMTP e alerta local.
- SQLite local, fila persistente, retentativas, idempotência e auditoria.
- Pipelines CI, CodeQL e Release para win-x86 e win-x64.
- Pacotes portáteis, instaladores, checksums e scripts de serviço.
