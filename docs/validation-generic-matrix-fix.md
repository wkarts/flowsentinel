# Correção de validação e desacoplamento do modo matriz — FlowSentinel 0.3.0

## Resposta objetiva sobre a planilha RP-102

A planilha RP-102 é somente um perfil opcional e um caso de teste. Ela não deve definir o comportamento do motor.

A implementação inicial da branch 0.3.0 tinha a intenção correta, mas ainda carregava alguns padrões contábeis no parser, como meses, `BAL`, títulos iniciados por `EMPRESAS` e nomes de registros orientados a empresa. Esta revisão remove esse acoplamento do núcleo.

O modo `SectionedMatrix` agora depende exclusivamente da configuração da fonte:

- colunas;
- marcador e texto do cabeçalho;
- prefixos de grupos;
- nomes removidos dos grupos;
- rótulos das colunas monitoradas;
- mapa de calendário;
- valores excluídos do cálculo atual;
- grupos especiais;
- nomes administrativos apresentados ao usuário;
- legenda de valores.

O RP-102 permanece como botão **Modelo RP-102 (opcional)**. O botão apenas preenche esses parâmetros e todos eles continuam editáveis.

## Operação genérica

Uma fonte monitora toda a planilha. Cada linha é normalizada como entidade e cada coluna acompanhada é normalizada como valor independente. Não existe exigência de criar uma automação por empresa, cliente, equipamento ou tarefa.

Aliases genéricos principais:

```text
Entity
EntityKey
Owner
Category
Period
Code
Value
CurrentValue
ValueMeaning
```

Aliases anteriores (`Company`, `Collaborator`, `Status`, entre outros) são mantidos apenas para compatibilidade.

Os identificadores produzidos pelo núcleo também foram generalizados:

```text
__recordType = Entity
Metric = ValuesByPeriod
Metric = EntitiesByCurrentValue
Scope = Category | Owner | Global
```

## Logs analisados

Foram analisadas duas execuções do workflow CI:

- `logs_82391594618.zip`;
- `logs_82391754097.zip`.

As duas execuções apresentaram o mesmo resultado:

- compilação: 0 erros e 6 avisos;
- testes Application: 5 aprovados;
- testes Desktop: 9 aprovados;
- testes Domain: 3 aprovados;
- testes Infrastructure: 7 aprovados e 1 reprovado.

Teste reprovado:

```text
ExcelSectionedMatrixParserTests
.DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais
```

A falha estava na validação de uma célula destacada. A cor havia sido aplicada como cor de fundo, enquanto a leitura considerava de forma insuficiente a cor de padrão.

## Correções de validação

- Leitura de destaque considera cor de fundo e cor de padrão.
- Cores vazias, transparentes, pretas ou brancas não são consideradas destaque.
- Endereço de célula é normalizado para texto não nulo.
- Objeto `Root` do conjunto de regras é inicializado antes do acesso.
- Fontes do sistema são tratadas como não nulas nos pontos indicados pelo analisador.
- Teste de caminho usa `Assert.EndsWith` em vez de `Assert.True`.
- Teste contábil recebe a configuração RP-102 explicitamente.
- Adicionado teste de uma matriz de equipamentos, sem vocabulário contábil.
- Corrigida a detecção de cabeçalho quando o marcador está vazio.

## Alterações para evitar engessamento

- Removidos do parser os meses fixos.
- Removidos do parser os termos `BAL`, `EMPRESAS`, `SIMPLES` e `SEM MOVIMENTO`.
- Removido o perfil contábil como valor padrão.
- Valores de período e calendário são definidos por configuração.
- Títulos especiais e prefixos são definidos por configuração.
- Rótulos de entidade, responsável, categoria, período, código e valor são definidos por configuração.
- O painel usa os nomes configurados em vez de títulos fixos.
- O assistente contábil foi renomeado para indicar claramente que é um modelo opcional.
- O fluxo principal para qualquer formato continua sendo **Nova automação avançada**.

## Compatibilidade

- A versão permanece `0.3.0`, porque o PR ainda está aberto.
- Nenhuma migração destrutiva de SQLite foi adicionada.
- O modo Excel `Table` permanece inalterado.
- CSV, TXT e bancos de dados permanecem independentes.
- Configurações e aliases anteriores continuam aceitos.
- Windows Desktop, Windows Service, x86 e x64 permanecem preservados.

## Validações locais realizadas

- leitura integral dos dois pacotes de logs;
- validação JSON;
- validação YAML;
- validação XML, `.csproj` e `.props`;
- verificação lexical dos arquivos C#;
- `git diff --check`;
- auditoria de termos específicos dentro do parser e settings;
- conferência das referências após a renomeação do assistente;
- regeneração da árvore do projeto;
- regeneração do manifesto SHA-256;
- aplicação do patch em cópia limpa;
- aplicação do MBOX em cópia limpa;
- comparação das árvores Git resultantes;
- validação de integridade dos pacotes ZIP.

O SDK .NET não está disponível no ambiente de geração. A confirmação de compilação, testes, CodeQL e publicação x86/x64 será feita pelo GitHub Actions após o push da correção para a branch do PR.
