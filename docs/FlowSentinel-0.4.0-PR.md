# Pull Request — FlowSentinel 0.4.0

## Branch

```text
feature/v0.4.0-professional-workbook-assistant
```

## Título

```text
feat: profissionaliza assistente de planilhas e centraliza contatos
```

## Descrição

Esta evolução transforma o monitoramento de planilhas em um fluxo visual completo e resolve a cascata de notificações derivadas observada no perfil RP-102.

### Principais entregas

- nova janela principal com menus organizados, barra de ações e submenu **Modelos**;
- assistente de planilhas em seis etapas, com análise visual e seleção de áreas;
- perfis RP-102, matriz periódica, tarefas, documentos e personalizado;
- indicadores agregados desativados por padrão;
- agrupamento de mensagens individual, por registro ou em resumo único por canal;
- notificações locais do Windows obrigatoriamente individuais;
- catálogo central de contatos e grupos com permissões por automação;
- importação e exportação JSON/CSV;
- seleção de destinatários reutilizáveis nos assistentes;
- splash com carregamento real de armazenamento, automações, canais e contatos;
- migração das automações RP-102 legadas e cancelamento de entregas agregadas pendentes;
- canais removidos, desabilitados ou incompatíveis ignorados sem gerar falsas falhas;
- migração v4 para reclassificar entregas históricas de canais desabilitados como `Skipped`.

### Causa da quantidade excessiva de mensagens

Uma alteração real de célula também modificava registros sintéticos `ValuesByPeriod` e `EntitiesByCurrentValue`. Como a ação de agregados estava habilitada, cada mudança gerava notificações adicionais para:

- total global;
- total por categoria;
- total por responsável;
- valor preenchido;
- valor vazio;
- valor atual da entidade.

Esses registros permanecem disponíveis para análise administrativa quando explicitamente habilitados, mas não são mais notificados por padrão.

No banco operacional analisado, a ação agregada respondeu por 200 das 254 entregas (78,7%). Também havia 58 falhas históricas vinculadas a um canal local desabilitado; elas passam a ser classificadas como ignoradas. Foram identificadas 8 retentativas reais da Evolution API por HTTP 500: 6 pertenciam à ação agregada descontinuada e são canceladas na migração, enquanto 2 mudanças individuais permanecem na política de retentativa.

### Compatibilidade

- aliases antigos `Company`, `Collaborator` e `CurrentStatus` continuam suportados;
- grupos incorporados às automações antigas continuam disponíveis como recurso legado;
- definições sem política de agrupamento permanecem no modo `Individual`;
- contatos manuais continuam aceitos.

## Commit sugerido

```text
feat: adiciona assistente profissional, contatos reutilizáveis e notificações agrupadas
```

## Merge sugerido

```text
feat: publica FlowSentinel 0.4.0 com assistente profissional de planilhas
```
