# Processo de release

## Versão

A versão oficial fica em `eng/Version.props` e segue SemVer:

```text
MAJOR.MINOR.PATCH[-SUFIXO]
```

Exemplos:

```text
0.1.0
0.3.0-rc.1
1.0.0
```

## Fluxo recomendado

```text
develop
  ↓
release/0.3.0
  ↓
main
  ↓
tag v0.3.0
  ↓
GitHub Actions Release
```

## Preparação

```powershell
./scripts/bump-version.ps1 -Version 0.3.0
```

Atualize o `CHANGELOG.md`, execute:

```powershell
./scripts/validate.ps1
```

Depois faça o merge em `main` e publique a tag:

```powershell
git tag -a v0.3.0 -m "FlowSentinel v0.3.0"
git push origin v0.3.0
```

## Artefatos produzidos

```text
FlowSentinel-Desktop-<versão>-win-x86.zip
FlowSentinel-Desktop-<versão>-win-x64.zip
FlowSentinel-Service-<versão>-win-x86.zip
FlowSentinel-Service-<versão>-win-x64.zip
FlowSentinel-Setup-<versão>-win-x86.exe
FlowSentinel-Setup-<versão>-win-x64.exe
SHA256SUMS.txt
```

## Assinatura opcional

Configure os secrets:

```text
WINDOWS_CERTIFICATE_BASE64
WINDOWS_CERTIFICATE_PASSWORD
```

Sem esses secrets, o workflow continua e publica artefatos não assinados, sempre com SHA-256.
