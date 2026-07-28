# Monitoramento administrativo de planilhas complexas

## Conceito operacional

Uma planilha inteira deve ser cadastrada como uma fonte de monitoramento. Não é necessário criar uma automação para cada empresa ou cliente.

No modo **Matriz com múltiplas seções e períodos**, o FlowSentinel normaliza a planilha em três níveis:

```text
Planilha / aba
├── Empresa
├── Situação da empresa em cada período
└── Indicadores agregados
```

Cada linha reconhecida gera um registro de empresa. Cada célula de período gera um registro de situação independente. O sistema também gera indicadores de quantidade por situação, seção, colaborador e período.

## Assistente guiado

Na tela principal, utilize **Novo monitoramento guiado**.

O assistente permite:

1. selecionar o arquivo Excel;
2. escolher a aba mais recente, uma aba específica ou todas as abas anuais;
3. analisar a estrutura antes de salvar;
4. escolher quais mudanças devem gerar notificações;
5. vincular vários canais e destinatários;
6. criar uma única automação para toda a carteira de empresas.

O perfil contábil padrão considera:

| Coluna | Conteúdo |
|---|---|
| A | Número sequencial |
| B | Seção ou nome da empresa |
| C | Código da empresa |
| D | Colaborador responsável |
| E até T | Períodos e situações |

Essas colunas permanecem configuráveis no editor de fonte.

## Painel administrativo

O botão **Painel de planilhas** abre uma área independente das notificações. O painel apresenta:

- a planilha na mesma disposição visual da aba original;
- valores, posição das células, larguras de colunas, alturas de linhas, cores e destaques;
- quantidade de clientes;
- quantidade de seções;
- quantidade de células de situação;
- clientes por situação atual;
- células por situação e período;
- filtros por empresa, código, seção, colaborador, período e situação;
- comparação do estado atual com uma linha de base;
- lista detalhada do que foi incluído, removido ou alterado;
- avisos de chaves duplicadas ou blocos não reconhecidos.

## Situação atual do cliente

Além de monitorar cada célula mensal, o sistema calcula a **situação atual** de cada cliente. Por padrão, em abas do ano corrente são considerados apenas os períodos até o mês atual; em anos anteriores, é utilizado o último período preenchido. A fonte também pode ser configurada para usar sempre o último período preenchido.

Isso permite responder perguntas como:

- quantos clientes estão atualmente com situação X;
- quantos clientes estão atualmente com situação SM;
- quais clientes mudaram de situação atual;
- qual foi o último período preenchido de cada cliente.

## Legenda de situações

O FlowSentinel não presume o significado de códigos como `X`, `SM`, `M`, `J` ou outros valores da planilha.

No painel, utilize **Legenda de situações** para associar cada código à descrição adotada pela empresa. A legenda fica armazenada na configuração da fonte e aparece nos registros, totais e notificações.

Exemplo meramente ilustrativo:

```text
X  → descrição definida pelo usuário
SM → descrição definida pelo usuário
M  → descrição definida pelo usuário
```

## Linha de base e mudanças

Após a primeira análise, clique em **Gravar linha de base**. Nas análises seguintes, **Comparar alterações** mostra:

- empresa adicionada ou removida;
- mudança de colaborador;
- mudança de situação mensal;
- mudança da situação atual;
- alteração de cor ou destaque;
- alteração de quantidades agregadas.

A linha de base é gravada no diretório de dados local e não altera a planilha original.

## Notificações

Uma única automação pode gerar ações independentes para:

- mudança de qualquer célula de situação;
- mudança da situação atual do cliente;
- mudança do colaborador responsável;
- mudança da quantidade de clientes por situação;
- mudança da quantidade de células por situação e período.

Cada ação pode utilizar vários canais e vários destinatários simultaneamente.

## Compatibilidade

O modo de tabela simples continua disponível para planilhas convencionais com um único cabeçalho. O modo administrativo é adicional e não altera automações existentes.
