# Contatos, grupos e agrupamento de notificações

## Catálogo central

O catálogo é armazenado em:

```text
%LOCALAPPDATA%\FlowSentinel\data\contacts.json
```

Uma cópia de segurança automática é mantida em `contacts.json.bak` a cada gravação subsequente.

## Contatos

Cada contato possui:

- identificador GUID;
- nome e situação ativa/inativa;
- um ou vários números de WhatsApp;
- um ou vários endereços de e-mail;
- um ou vários identificadores do Telegram;
- observações;
- permissão para todas as automações ou apenas automações selecionadas.

## Grupos

Um grupo possui:

- identificador textual estável;
- nome;
- contatos participantes;
- situação ativa/inativa;
- permissão global ou restrita por automação.

Ao selecionar um grupo em uma ação, o resolvedor expande somente os contatos ativos, autorizados e que possuam endereço para o canal executado.

## Acesso pela interface

O menu **Contatos** da janela principal possui atalhos para:

- novo contato;
- novo grupo de contatos;
- catálogo de contatos;
- grupos de contatos;
- importação de catálogo JSON ou contatos CSV;
- exportação de catálogo JSON ou contatos CSV.

## Importação e exportação

O gerenciador oferece:

- catálogo completo em JSON, preservando IDs, grupos e permissões;
- contatos em CSV para intercâmbio simples.

Formato CSV:

```text
Nome;WhatsApp;Email;Telegram;Observacoes
```

Vários endereços podem ser separados por ponto e vírgula dentro de um campo entre aspas.

## Compatibilidade

Automações antigas podem possuir grupos incorporados em `AutomationDefinition.ContactGroups`. Esses grupos continuam sendo resolvidos quando não existe um grupo correspondente no catálogo central. A interface os identifica como compatibilidade legada e recomenda o catálogo para novos monitoramentos.

## Agrupamento por canal

A política fica em `ActionChannelDefinition`:

```json
{
  "groupingMode": "ByEntity",
  "groupField": "EntityKey",
  "groupingWindowSeconds": 8
}
```

Modos:

- `Individual`;
- `ByEntity`;
- `SingleMessage`.

O dispatcher agrupa entregas compatíveis por automação, configuração de canal, destinatário e chave de agrupamento. Em `ByEntity`, procura primeiro o campo configurado e depois os aliases `EntityKey`, `CompanyKey`, `Entity`, `Company`, `Code` e `record.key`.

O canal `LocalWindows` ignora qualquer configuração diferente de `Individual` por regra de domínio.

## Disponibilidade dos canais

Antes de criar uma entrega, o executor confirma que a configuração do canal existe, está ativa e corresponde ao tipo definido na ação. Uma referência antiga a um canal removido, desabilitado ou incompatível é ignorada e registrada apenas em log de diagnóstico.

Entregas antigas que ainda estejam pendentes, em retentativa, em processamento ou marcadas como falha por um canal removido/desabilitado são convertidas para `Skipped` pela migração v4. Isso evita que o painel apresente uma falha operacional para algo que o próprio usuário decidiu não executar.

Falhas reais de canais ativos — por exemplo, erro HTTP, autenticação rejeitada ou indisponibilidade temporária — continuam como falha ou retentativa e não são mascaradas.
