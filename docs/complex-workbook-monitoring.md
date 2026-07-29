# Monitoramento genérico de planilhas estruturadas

## O que é genérico e o que é apenas modelo

O modo `SectionedMatrix` é um mecanismo genérico. Ele não depende de contabilidade, empresas, meses, regimes tributários ou da planilha RP-102.

A planilha RP-102 foi utilizada como um caso real de teste porque possui uma estrutura difícil: vários blocos, cabeçalhos repetidos, colunas de acompanhamento, listas especiais, cores e anos diferentes. A configuração RP-102 permanece disponível somente como **modelo opcional** para preencher rapidamente os parâmetros dessa planilha.

```text
Motor genérico SectionedMatrix
├── configuração visual livre
├── qualquer entidade por linha
├── qualquer grupo ou categoria
├── qualquer responsável
├── qualquer conjunto de períodos/etapas
└── modelos opcionais
    └── RP-102 (exemplo contábil)
```

Nenhum termo do modelo RP-102 é obrigatório no parser. Cabeçalho, rótulos, prefixos, colunas, períodos, grupos especiais, campos ignorados e nomes exibidos são parametrizáveis.

## Quando usar cada modo

### Tabela simples

Use quando existe uma única linha de cabeçalho e cada linha representa um registro convencional.

Exemplos:

- cadastro de clientes;
- pedidos;
- contas a receber;
- exportação de banco de dados;
- inventário com uma linha por item.

### Matriz estruturada

Use quando a planilha possui um ou mais destes elementos:

- blocos ou grupos repetidos;
- cabeçalhos no meio da aba;
- várias colunas de situação, etapa ou período;
- células vazias que também têm significado;
- cores ou destaques;
- listas especiais sem todas as colunas;
- necessidade de totais por valor, grupo ou responsável.

## Unidade de monitoramento

Uma única fonte pode monitorar toda a planilha. Não é necessário cadastrar uma automação para cada empresa, equipamento, tarefa ou documento.

O sistema normaliza a planilha em três tipos de registro:

```text
Planilha / aba
├── Entidade consolidada
│   ├── cliente
│   ├── equipamento
│   ├── contrato
│   ├── tarefa
│   └── qualquer outro registro da linha
├── Valor por coluna de acompanhamento
└── Indicadores agregados
```

Os nomes exibidos são definidos na fonte:

- nome singular e plural da entidade;
- nome do responsável;
- nome do grupo/categoria;
- nome do período/etapa;
- nome do código/chave;
- nome do valor monitorado.

Internamente, aliases legados como `Company`, `CompanyKey`, `Collaborator` e `Status` são preservados apenas para manter compatibilidade com automações criadas durante as versões anteriores. Os aliases genéricos recomendados são:

```text
Entity
EntityKey
Owner
Category
Period
Code
Value
CurrentValue
```

## Configuração visual genérica

Em **Nova automação avançada**, adicione uma fonte Excel e selecione:

```text
Modo de leitura: Matriz estruturada com grupos e colunas
```

A tela permite configurar:

1. arquivo e seleção da aba;
2. marcador exato ou texto contido no cabeçalho;
3. rótulos reservados das colunas;
4. prefixos que identificam títulos de grupos;
5. prefixos removidos do nome exibido do grupo;
6. coluna de número;
7. coluna do grupo/categoria;
8. coluna da entidade;
9. coluna da chave/código;
10. coluna do responsável;
11. primeira e última coluna monitorada;
12. grupos independentes;
13. grupos sem colunas de acompanhamento;
14. cálculo do valor atual;
15. mapa de calendário, quando aplicável;
16. períodos que não participam do valor atual;
17. nomes administrativos exibidos no painel;
18. legenda dos valores;
19. geração de totais e monitoramento de formatação.

## Modelo opcional RP-102

O botão **Modelo RP-102 (opcional)** não altera o motor e não cria uma regra global fixa. Ele apenas preenche os parâmetros conhecidos do arquivo de referência, como:

- colunas A até T;
- cabeçalho identificado por `Nº` e `EMPRESAS`;
- períodos JAN a DEZ;
- colunas auxiliares `BAL`;
- grupos especiais usados naquele arquivo.

Após aplicar o modelo, todos os parâmetros continuam editáveis.

O botão da tela principal também é apresentado como **Modelo RP-102 (opcional)** para deixar claro que ele não é o único fluxo de monitoramento.

## Exemplo genérico

Uma planilha de frota pode ser configurada assim:

```text
Entidade: Equipamento
Grupo: Unidade
Responsável: Mecânico
Períodos/etapas: INSPEÇÃO | MANUTENÇÃO | DOCUMENTAÇÃO
Valor: OK | PENDENTE | BLOQUEADO
```

Uma planilha de projetos poderia usar:

```text
Entidade: Projeto
Grupo: Cliente
Responsável: Gestor
Períodos/etapas: ANÁLISE | DESENVOLVIMENTO | HOMOLOGAÇÃO | ENTREGA
Valor: NÃO INICIADO | EM ANDAMENTO | CONCLUÍDO
```

O mesmo parser e o mesmo painel atendem aos dois casos.

## Painel administrativo

O botão **Painel de planilhas** abre uma visão independente dos disparos. O painel apresenta:

- planilha na disposição visual da aba original;
- valores, posições, dimensões, cores e destaques;
- quantidade de entidades;
- quantidade de grupos;
- quantidade de valores monitorados;
- totais por valor, período, grupo e responsável;
- filtros administrativos;
- linha de base;
- comparação de inclusões, remoções e alterações;
- avisos de estrutura e chaves duplicadas.

Os títulos do painel usam os nomes configurados na fonte. Portanto, ele pode mostrar “Clientes”, “Equipamentos”, “Tarefas”, “Contratos” ou qualquer outra entidade sem alteração de código.

## Valor atual

O valor consolidado da entidade pode ser calculado por:

- **último valor preenchido**; ou
- **calendário configurado**, usando um mapa `RÓTULO=NÚMERO`.

O calendário não é limitado a nomes de meses. Os rótulos são definidos pelo usuário. Valores auxiliares também podem ser excluídos desse cálculo.

## Legenda de valores

O FlowSentinel não presume o significado de códigos. A legenda é cadastrada pelo usuário e pode representar qualquer domínio.

```text
OK        → concluído
PENDENTE  → requer ação
BLOQ      → bloqueado
```

## Linha de base e mudanças

A linha de base permite identificar:

- entidade adicionada ou removida;
- troca de responsável;
- mudança de valor em uma coluna;
- mudança do valor atual;
- mudança de cor ou destaque;
- alteração de quantidade agregada.

A linha de base fica no diretório local de dados e não modifica a planilha original.

## Compatibilidade

- o modo Tabela simples continua disponível;
- CSV, TXT e bancos de dados continuam independentes;
- configurações RP-102 existentes continuam válidas;
- aliases legados continuam disponíveis;
- nenhuma automação por entidade é exigida;
- nenhuma regra contábil foi incorporada ao motor genérico.
