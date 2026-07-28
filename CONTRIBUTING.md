# Contribuindo

## Fluxo de branches

- `main`: versão estável.
- `develop`: integração contínua.
- `feature/<descricao>`: funcionalidades.
- `fix/<descricao>`: correções comuns.
- `hotfix/<descricao>`: correções urgentes de produção.
- `release/<versao>`: preparação de release.

## Validação local

```powershell
./scripts/validate.ps1
```

## Commits

Use mensagens objetivas, preferencialmente no padrão Conventional Commits:

```text
feat: adiciona monitoramento de planilha
fix: impede duplicidade de entrega
chore: atualiza workflow de release
```

## Pull request

A PR deve informar problema, solução, riscos, testes e impacto de compatibilidade.
