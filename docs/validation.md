# Validação do projeto

Antes de publicar uma versão, execute em Windows:

```powershell
./scripts/validate.ps1
```

A validação executa restauração, análise de formatadores, compilação Release, testes e cobertura. O GitHub Actions repete essas verificações em `windows-latest` antes de gerar qualquer release.

## Validação estrutural aplicada ao pacote-fonte

- JSON, YAML e XML analisados sintaticamente.
- Referências entre projetos verificadas.
- IDs e aliases de fontes validados pelo domínio.
- Fonte primária obrigatoriamente habilitada.
- IDs de ações, fontes e grupos sem duplicidade.
- Canais sem configuração e destinatários vazios rejeitados.
- Workflows separados para CI, CodeQL e release.

A compilação definitiva ocorre no runner Windows porque o projeto contém WinForms, publicação `win-x86`/`win-x64`, Inno Setup e DPAPI.
