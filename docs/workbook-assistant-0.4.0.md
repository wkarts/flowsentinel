# Assistente profissional de planilhas — FlowSentinel 0.4.0

## Objetivo

O assistente de planilhas transforma uma pasta de trabalho Excel em uma automação monitorável sem exigir o cadastro manual de uma automação por linha. O motor continua genérico: uma linha pode representar empresa, cliente, equipamento, tarefa, contrato, documento ou qualquer outra entidade.

## Fluxo em seis etapas

### 1. Origem

- nome do monitoramento;
- arquivo `.xlsx` ou `.xlsm`;
- aba fixa, aba anual mais recente ou todas as abas compatíveis;
- intervalo de leitura.

O arquivo é aberto somente para leitura.

### 2. Mapeamento

O usuário define ou revisa:

- linha do cabeçalho;
- primeira e última linha de dados;
- coluna de número;
- coluna de categoria ou grupo;
- coluna da entidade;
- coluna de código ou chave;
- coluna do responsável;
- primeira e última coluna de acompanhamento;
- marcadores, prefixos e seções especiais;
- nomes administrativos exibidos na interface e nas mensagens.

Valores `0` nas linhas de dados permitem detecção automática.

### 3. Pré-visualização e seleção de área

A análise mostra as abas reconhecidas, células, dimensões, larguras, alturas, cores e avisos estruturais. A seleção visual pode ser aplicada como:

- intervalo de linhas monitoradas;
- intervalo de colunas de acompanhamento;
- coluna da célula atual;
- linha de cabeçalho atual.

Isso permite corrigir o mapeamento sem editar JSON.

### 4. Eventos

Eventos de negócio disponíveis:

- mudança de valor por célula;
- mudança do valor atual do registro;
- mudança de responsável;
- mudança de cor ou destaque;
- mudança de indicadores agregados.

Indicadores agregados e formatação ficam desativados por padrão. Essa política evita a cascata em que uma única célula alterada também modifica totais globais, por categoria, por responsável, valores vazios e valor atual.

### 5. Notificações

Cada canal pode usar contatos, grupos ou endereços manuais. A forma de entrega é configurada separadamente:

- **Individual:** uma mensagem por alteração;
- **Agrupar por registro:** reúne na mesma mensagem as mudanças de uma empresa, cliente, equipamento ou outro registro;
- **Resumo único:** reúne todas as mudanças do ciclo em uma mensagem.

Notificações locais do Windows permanecem sempre individuais.

### 6. Revisão

Antes da criação, o assistente apresenta:

- perfil escolhido;
- arquivo e intervalo;
- mapeamento de linhas e colunas;
- eventos ativos;
- canais, destinatários e forma de envio;
- resultado da análise estrutural.

## Perfis disponíveis

- Matriz contábil RP-102;
- Matriz de acompanhamento por períodos;
- Controle de tarefas e responsáveis;
- Controle documental;
- Modelo personalizado.

Os perfis apenas preenchem valores iniciais. O parser e as regras permanecem configuráveis e reutilizáveis.

## Atualização de automações RP-102 antigas

Na atualização do armazenamento, o FlowSentinel:

- desativa a antiga ação de quantidades agregadas;
- desativa a geração automática de agregados da fonte RP-102;
- remove agrupamentos por categoria e responsável do perfil legado;
- exclui valores vazios dos agregados;
- cancela entregas agregadas ainda pendentes;
- mantém notificações do Windows no modo individual;
- reclassifica como ignoradas as entregas de canais removidos ou desabilitados, sem mantê-las como falhas operacionais.

As mudanças por célula, valor atual e responsável continuam preservadas. A migração não apaga o histórico enviado e não altera falhas reais de canais ainda ativos.
