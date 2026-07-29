# FlowSentinel 0.4.3 — inicialização e desempenho

## Sintomas corrigidos

A versão 0.4.2 podia permanecer por muito tempo na etapa **Verificando o banco local** e, após abrir a janela principal, o Windows podia exibir **Não está respondendo**. O problema não estava na planilha nem exigia apagar o banco.

A causa era a combinação de quatro pontos:

1. o fluxo assíncrono da inicialização era iniciado antes do message loop definitivo do WinForms;
2. o host iniciava os trabalhadores antes da migração explícita do armazenamento, criando concorrência sobre o SQLite;
3. a migração da versão interna 5 carregava e regravava ocorrências, entregas e históricos linha por linha;
4. uma automação com milhares de registros executava consultas e gravações individuais por registro e por ação em cada ciclo.

No banco analisado havia aproximadamente 2.500 ocorrências abertas. Com intervalo de cinco segundos, o comportamento anterior podia produzir milhares de comandos SQLite em cada leitura, além de logs informativos extensos do Entity Framework.

## Inicialização do Desktop

O WinForms agora é criado e executado na mesma thread STA. As operações pesadas de banco e catálogos são executadas fora da thread visual, enquanto o splash mantém o processamento de mensagens da interface.

As etapas possuem limites explícitos:

- banco local: 120 segundos;
- automações, canais, contatos e grupos: 45 segundos;
- inicialização do host: 45 segundos.

Se um limite for ultrapassado, o splash é encerrado e o usuário recebe uma mensagem com a pasta de logs. A aplicação deixa de permanecer indefinidamente em uma tela aparentemente congelada.

O agendador e o processador de entregas aguardam dois segundos depois da inicialização do host. Isso permite que a janela principal seja exibida e atualizada antes do primeiro ciclo de carga.

## Migração incremental do SQLite

A versão interna do armazenamento passa para 6. A atualização:

- preserva automações, ocorrências, entregas, contatos e históricos;
- converte datas com fuso explícito por comandos SQL em lote;
- não carrega nem marca todas as linhas como modificadas;
- aplica timeout de espera de 15 segundos a todas as conexões SQLite, reduzindo falhas imediatas durante contenção legítima;
- remove somente estados de ação inativos, sem episódio, sem execução e sem agendamento;
- mantém estados ativos, episódios recorrentes, contadores e entregas existentes;
- continua desativando apenas indicadores agregados legados do perfil RP-102, conforme a política anterior.

Não é necessário excluir `flowsentinel.db` nem recriar automações.

## Execução das automações

Cada ciclo passa a:

- carregar ocorrências abertas uma única vez;
- carregar estados de ações uma única vez;
- comparar registros em memória;
- atualizar individualmente somente registros alterados ou com mudança de estado;
- resolver registros ausentes usando a coleção já carregada;
- consolidar `LastEvaluatedAt` em uma janela de cinco minutos, evitando reescrever milhares de linhas a cada cinco segundos;
- não persistir estado para ações de mudança cuja condição permaneceu falsa.

O histórico resumido de cada execução e o histórico detalhado das mudanças continuam sendo gravados.

## Logs

O arquivo diário deixa de registrar comandos informativos do Entity Framework e do `HttpClient`. Avisos, erros e informações próprias do FlowSentinel continuam disponíveis. O writer utiliza flush periódico para reduzir contenção de disco, mantendo flush imediato para avisos e erros.

## Splash e marca

O painel escuro utiliza `Assets/WWSoftwaresDeveloperLogoWhite.png`, fornecido pelo usuário. O splash mostra tempo decorrido e informa quando uma etapa continua em processamento, sem ocultar uma demora real.

## Compatibilidade

Nenhum modelo de monitoramento ou notificação foi removido. Permanecem suportados:

- mudança simples;
- ação na abertura;
- ação enquanto ativa;
- ação na resolução;
- pendência recorrente genérica;
- agrupamento individual, por entidade ou resumo;
- contatos e grupos;
- modelos RP-102, matriz periódica, tarefas, documentos e personalizado;
- automações criadas entre as versões 0.3.0 e 0.4.2.
