# Diagnóstico das notificações — FlowSentinel 0.4.0

## Escopo analisado

A análise foi realizada sobre o banco SQLite e os logs operacionais enviados junto com a automação RP-102. Nenhuma credencial de canal foi incluída neste documento.

## Resultado objetivo

O banco continha 254 entregas associadas ao monitoramento analisado:

| Ação | Entregas | Participação |
|---|---:|---:|
| Mudança de quantidade por situação | 200 | 78,7% |
| Mudança de situação por célula | 40 | 15,7% |
| Mudança do valor atual do registro | 12 | 4,7% |
| Mudança de cor ou destaque | 2 | 0,8% |

A maior parte das mensagens não correspondia a novas alterações independentes da planilha. Elas eram efeitos derivados da mesma mudança: ao preencher uma célula, o parser recalculava totais `ValuesByPeriod` e `EntitiesByCurrentValue` nos escopos global, categoria e responsável, incluindo o valor preenchido e o valor vazio.

## Por que uma alteração produzia tantas mensagens

Uma mudança como `vazio → X` podia gerar simultaneamente:

1. mudança da célula do registro;
2. mudança do valor atual da entidade;
3. incremento do total de `X` global;
4. decremento do total de vazios global;
5. os mesmos dois totais por categoria;
6. os mesmos dois totais por responsável;
7. atualização de entidades por valor atual.

Esse comportamento era matematicamente coerente com os indicadores, mas inadequado como política padrão de comunicação externa.

## Falhas que não eram falhas reais

Foram encontradas 58 entregas marcadas como falha porque uma ação antiga ainda referenciava o canal local do Windows, embora a configuração estivesse desabilitada. O erro registrado era equivalente a “configuração de canal inexistente ou desabilitada”.

A versão 0.4.0 corrige esse cenário em duas camadas:

- novas entregas não são criadas quando o canal está ausente, desabilitado ou incompatível;
- a migração interna v4 converte para `Skipped` as entregas antigas pendentes, em retentativa, em processamento ou falhas que pertençam a canal removido/desabilitado.

O histórico enviado permanece intacto.

## Falhas externas preservadas

Também foram encontradas 8 entregas em retentativa do canal Evolution API devido a resposta HTTP 500 com indicação de conexão encerrada. Esse é um erro externo/transitório do canal ativo, não uma falha provocada pelo FlowSentinel. Seis dessas retentativas pertencem à antiga ação de indicadores agregados e são canceladas pela atualização porque essa ação deixa de ser executada; as duas retentativas ligadas a mudanças individuais continuam na política normal de retentativa. A correção não transforma falhas reais de canais ativos em sucesso nem em entrega ignorada.

## Projeção da migração sobre o banco analisado

Aplicando as regras da migração v4 aos 254 registros observados, o resultado esperado é:

| Situação após a atualização | Quantidade | Motivo |
|---|---:|---|
| Enviada | 188 | histórico de entregas concluídas preservado |
| Ignorada (`Skipped`) | 58 | referências históricas ao canal local desabilitado |
| Cancelada | 6 | retentativas pertencentes à ação agregada desativada |
| Em retentativa | 2 | falhas HTTP 500 de mudanças individuais no canal ativo |
| Falha | 0 | não havia outra falha definitiva de canal ativo no conjunto analisado |

Essa projeção se refere exclusivamente ao banco enviado e não é uma regra fixa para outras instalações.

## Política adotada na versão 0.4.0

- mudanças individuais são o padrão;
- indicadores agregados ficam desativados por padrão;
- totais vazios não participam de agregados, salvo escolha explícita;
- cada canal externo pode enviar individualmente, agrupado por entidade ou em resumo único;
- notificações locais do Windows são sempre individuais;
- automações RP-102 legadas são migradas para desativar a antiga ação de agregados e cancelar suas entregas ainda pendentes.

## Resultado esperado após a atualização

Para uma alteração comum de célula na RP-102, o comportamento padrão passa a ser uma mensagem de mudança da célula e, quando aplicável, uma mensagem de alteração do valor atual. Mensagens de totais somente serão produzidas quando o usuário habilitar explicitamente os indicadores agregados e associar uma ação a eles.
