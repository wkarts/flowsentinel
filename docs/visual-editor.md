# Editor visual do FlowSentinel 0.2.0

## Objetivo

O editor visual converte formulários WinForms em `AutomationDefinition`, preservando o mesmo JSON utilizado pelo motor e pelo Windows Service.

## Fluxo

1. **Geral** — nome, descrição, intervalo, prioridade e comportamento de registros ausentes.
2. **Fontes de dados** — uma ou mais fontes, com exatamente uma principal.
3. **Critérios** — abertura, permanência, conclusão e suspensão.
4. **Ações e notificações** — repetição, canais, destinatários e templates.
5. **Contatos e grupos** — contatos reutilizáveis por canal.
6. **Revisão** — resumo e validação do modelo.
7. **Avançado** — edição opcional do JSON completo.

## Fontes

### Excel

- arquivos `.xlsx` e `.xlsm`;
- seleção de aba;
- linha do cabeçalho;
- pré-visualização;
- chave simples ou composta.

### CSV

- delimitador;
- caractere de aspas;
- codificação;
- presença de cabeçalho;
- pré-visualização.

### TXT

- uma ocorrência por linha;
- chave e valor;
- expressão regular com grupos nomeados.

### Bancos

- SQLite;
- SQL Server;
- MySQL/MariaDB;
- PostgreSQL;
- Firebird;
- parâmetros;
- timeout;
- somente consultas `SELECT` ou `WITH`.

A connection string pode ser protegida por usuário ou por máquina. Use proteção por máquina quando a automação for executada pelo Windows Service.

## Critérios

O editor representa grupos recursivos. Cada grupo pode usar:

- **TODAS (E)**;
- **QUALQUER (OU)**;
- negação do resultado.

O modo avançado continua disponível para compatibilidade e diagnósticos.

## Canais

Os canais possuem formulários específicos. Tokens e senhas são protegidos automaticamente ao salvar.

## Compatibilidade

Automações e canais criados por versões anteriores continuam sendo carregados. Propriedades ainda não expostas visualmente permanecem disponíveis pelo JSON avançado.
