# Validação técnica — FlowSentinel 0.4.0

## Escopo

Validação da evolução do assistente profissional de planilhas, catálogo de contatos, agrupamento de notificações, migração de automações RP-102 e carregamento inicial da aplicação.

Data da validação: 29/07/2026.

## Fontes verificadas

- código-fonte base do FlowSentinel 0.3.0;
- pacote operacional enviado, incluindo banco SQLite e logs;
- planilha `RP-102 Controle de conferência contábil 2018 .xlsx`;
- histórico funcional fornecido pelo usuário;
- imagens da interface e das notificações recebidas.

Nenhuma credencial encontrada no ambiente operacional foi copiada para os relatórios ou artefatos de entrega.

## Diagnóstico operacional confirmado

- 1 automação;
- 2.500 ocorrências;
- 254 entregas;
- 200 entregas da ação agregada `Mudança de quantidade por situação` — 78,7% do total;
- 58 falhas históricas decorrentes de referência a canal local desabilitado;
- 8 retentativas reais da Evolution API por HTTP 500/conexão encerrada;
- projeção da migração v4: 188 enviadas, 58 ignoradas, 6 canceladas e 2 em retentativa.

## Validações estruturais executadas

- integridade de 4 arquivos JSON;
- integridade de 7 arquivos YAML de automação e GitHub Actions;
- integridade de 13 arquivos XML/projetos/manifestos;
- 13 referências entre projetos verificadas;
- 9 projetos da solução verificados;
- 164 declarações de tipos identificadas;
- 89 arquivos C# verificados quanto ao balanceamento de delimitadores, ignorando comentários e literais;
- 161 arquivos da árvore do projeto inventariados antes da geração deste relatório;
- ausência de `AccountingMonitorWizardForm.cs` confirmada;
- ausência da mensagem informal do antigo modelo RP-102 no código confirmada;
- ausência do botão antigo `Modelo RP-102 (opcional)` no código confirmada;
- versão do produto confirmada como 0.4.0;
- versão de armazenamento SQLite confirmada como v4;
- catálogo de contatos confirmado no registro de dependências;
- referências da solução e dos projetos conferidas;
- verificação de espaços inválidos com `git diff --check` equivalente;
- CRC dos dois ZIPs enviados e da planilha XLSX aprovado.

## Testes de regressão incorporados

Foram adicionados ou ampliados testes para:

- composição de lotes individuais, por entidade e em resumo único;
- resolução de contatos e grupos com permissões por automação;
- validação de agrupamento local do Windows sempre individual;
- seleção de áreas e agregações do parser de matriz estruturada;
- desativação apenas de automações legadas identificadas como RP-102;
- preservação de matrizes genéricas que usam indicadores agregados;
- cancelamento de entregas agregadas pendentes;
- reclassificação de entregas de canais desabilitados para `Skipped`;
- persistência do resultado de entrega ignorada.

## Limitação do ambiente

O projeto fixa o SDK .NET `10.0.301` em `global.json`. O ambiente utilizado para esta entrega não possui `dotnet`, `csc`, `mcs` ou `msbuild`, e não permite obter externamente o SDK exigido. Por isso, não foi possível executar localmente:

- `dotnet restore`;
- `dotnet build`;
- `dotnet test`;
- publicação Windows x86/x64;
- CodeQL compilado.

Essas etapas permanecem obrigatórias no GitHub Actions. A entrega não afirma que houve compilação executável neste ambiente.

## Critérios esperados no CI

A Pull Request somente deve ser integrada quando:

1. restore, build e testes forem concluídos sem erro;
2. CodeQL não apresentar erro de compilação;
3. publicação `win-x86` e `win-x64` for concluída;
4. testes de migração SQLite passarem;
5. testes de agrupamento e catálogo de contatos passarem;
6. o instalador aceitar atualização sobre a versão 0.3.0 sem apagar dados do usuário.
