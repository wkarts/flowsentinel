# Pull Request — FlowSentinel 0.3.0

## Branch

```text
feature/v0.3.0-workbook-intelligence-dashboard
```

## Título

```text
feat: adiciona monitoramento genérico de planilhas estruturadas
```

## Descrição

### Objetivo

Adicionar um modo administrativo e genérico para monitorar planilhas com múltiplos grupos, cabeçalhos repetidos, colunas de acompanhamento, responsáveis, cores e indicadores, sem exigir uma automação por entidade e sem incorporar regras de uma planilha específica ao motor.

### Esclarecimento sobre o RP-102

O arquivo RP-102 é somente um modelo opcional e um caso real de validação.

O motor `SectionedMatrix` não depende de:

- contabilidade;
- empresas;
- regimes tributários;
- meses específicos;
- colunas `BAL`;
- títulos `EMPRESAS`;
- grupos `SIMPLES` ou `SEM MOVIMENTO`.

O botão **Modelo RP-102 (opcional)** apenas preenche uma configuração inicial. Todos os parâmetros permanecem editáveis, e outras estruturas são cadastradas por **Nova automação avançada**.

### Modelo genérico

Uma única fonte monitora toda a planilha. As linhas são tratadas como entidades e as colunas acompanhadas como valores independentes.

Exemplos de entidades suportadas pelo mesmo núcleo:

- clientes;
- equipamentos;
- contratos;
- tarefas;
- documentos;
- projetos;
- pedidos;
- qualquer registro representado por uma linha.

Aliases genéricos:

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

Aliases anteriores permanecem disponíveis apenas para compatibilidade.

### Configuração visual

A fonte permite configurar:

- arquivo e seleção de abas;
- marcador e texto do cabeçalho;
- colunas de entidade, chave, grupo e responsável;
- primeira e última coluna monitorada;
- rótulos das etapas ou períodos;
- prefixos que identificam grupos;
- grupos especiais;
- mapa opcional de calendário;
- valores excluídos do cálculo atual;
- legenda dos valores;
- rótulos administrativos exibidos no painel;
- geração de entidades, valores e indicadores;
- leitura de cores e destaques.

### Painel administrativo

- visualização semelhante à planilha original;
- totais por valor, período, categoria e responsável;
- filtros por entidade, código, categoria, responsável, período e valor;
- linha de base;
- comparação de inclusões, remoções e alterações;
- detecção de alteração de valor, responsável, cor e destaque;
- títulos adaptados aos nomes configurados na fonte.

### Correções dos logs de validação

Os workflows apresentavam 0 erros de compilação, 6 avisos e uma falha de teste na detecção de célula destacada.

Correções aplicadas:

- leitura de cor de fundo e cor de padrão;
- tratamento seguro do endereço de célula;
- correção de nulabilidade no assistente e nas fontes do sistema;
- substituição de `Assert.True` por `Assert.EndsWith`;
- teste RP-102 com parâmetros explícitos;
- teste de matriz genérica de equipamentos;
- correção de cabeçalho com marcador vazio;
- remoção de vocabulário contábil do parser e dos valores padrão;
- identificadores genéricos `Entity`, `ValuesByPeriod` e `EntitiesByCurrentValue`.

### Compatibilidade

- nenhuma migração destrutiva de SQLite;
- modo Excel de tabela simples preservado;
- CSV, TXT e bancos de dados preservados;
- JSON e aliases legados preservados;
- Desktop e Windows Service preservados;
- suporte a Windows x86 e x64 preservado.

### Versão

```text
0.3.0
```

### Validação

- [x] Logs de push e pull request analisados
- [x] Falha do teste de preenchimento corrigida
- [x] Avisos identificados tratados
- [x] Motor desacoplado do perfil RP-102
- [x] Teste de domínio genérico adicionado
- [x] JSON validado
- [x] YAML validado
- [x] XML e projetos validados
- [x] Verificação lexical C# concluída
- [x] `git diff --check` aprovado
- [x] Patch validado em fonte limpa
- [x] MBOX validado em fonte limpa
- [x] Manifesto SHA-256 regenerado
- [ ] Build confirmado pelo GitHub Actions
- [ ] Testes confirmados pelo GitHub Actions
- [ ] CodeQL confirmado pelo GitHub Actions
- [ ] Publicação win-x86 confirmada
- [ ] Publicação win-x64 confirmada

## Commit

```text
fix: generaliza matriz estruturada e corrige validações
```

## Merge

```text
feat: publica monitoramento genérico de planilhas estruturadas
```
