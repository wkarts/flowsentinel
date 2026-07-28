# GitHub Flow do projeto

## Branches permanentes

- `main`: releases estáveis.
- `develop`: integração da próxima versão.

## Branches temporárias

- `feature/<assunto>`
- `fix/<assunto>`
- `hotfix/<assunto>`
- `release/<versao>`

## Proteção recomendada de `main`

- exigir pull request;
- exigir ao menos uma revisão;
- exigir CI e CodeQL aprovados;
- impedir push forçado;
- impedir exclusão;
- exigir resolução de conversas;
- usar squash merge ou merge commit padronizado.

## Ambientes

Crie o ambiente `release` no repositório quando quiser exigir aprovação manual antes da publicação. O workflow pode então receber:

```yaml
environment: release
```

## Convenção de commits

```text
feat: adiciona fonte XML
fix: corrige repetição de uma entrega
perf: reduz leitura de planilhas inalteradas
refactor: separa contrato do canal
chore: atualiza dependências
```
