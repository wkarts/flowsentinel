# FlowSentinel

FlowSentinel é uma plataforma Windows genérica para monitoramento orientado a regras. Ela lê múltiplas fontes, acompanha cada registro individualmente, mantém o estado de cada ocorrência e distribui notificações para vários canais e destinatários simultaneamente.

## Principais capacidades

- Vários clientes e situações na mesma planilha.
- Chave composta por registro, evitando confusão entre documentos do mesmo cliente.
- Múltiplas fontes correlacionadas pela mesma chave.
- Grupos lógicos AND/OR aninhados.
- Critérios separados de entrada, permanência, suspensão e conclusão.
- Várias ações por ocorrência.
- Vários canais por ação.
- Vários destinatários no mesmo canal.
- Destinatários fixos, vindos dos dados ou pertencentes a grupos.
- Templates com campos do registro.
- Fila persistente, retentativa, idempotência e auditoria.
- Aplicação em System Tray e modo Windows Service.
- Publicação autocontida para `win-x86` e `win-x64`.

## Stack

- C# / .NET 10 LTS
- WinForms
- Generic Host / BackgroundService
- SQLite + Entity Framework Core
- ClosedXML
- MailKit
- ADO.NET para SQL Server, MySQL, PostgreSQL, Firebird e SQLite

## Estrutura

```text
src/
├── FlowSentinel.Domain
├── FlowSentinel.Application
├── FlowSentinel.Infrastructure
├── FlowSentinel.Desktop
└── FlowSentinel.Service

tests/
├── FlowSentinel.Domain.Tests
└── FlowSentinel.Application.Tests
```

## Compilação local

Requisitos:

- Windows 10/11 ou Windows Server compatível.
- SDK .NET 10.0.300 ou patch posterior da mesma feature band.

```powershell
dotnet restore FlowSentinel.sln
dotnet build FlowSentinel.sln -c Release
dotnet test FlowSentinel.sln -c Release --no-build
```

## Execução desktop

```powershell
dotnet run --project src/FlowSentinel.Desktop/FlowSentinel.Desktop.csproj
```

Na primeira execução é criado o banco em:

```text
%LOCALAPPDATA%\WWSoftwares\FlowSentinel\data\flowsentinel.db
```

## Publicação local

```powershell
./scripts/publish.ps1 -Version 0.3.0
```

Os arquivos serão gerados em `artifacts/`.

## Criação de release

1. Atualize `eng/Version.props` e `CHANGELOG.md`.
2. Valide:

```powershell
./scripts/validate.ps1
```

3. Crie e envie a tag:

```powershell
git tag v0.3.0
git push origin v0.3.0
```

O workflow de release irá:

- validar a versão da tag;
- restaurar, compilar e testar;
- publicar Desktop e Service para x86 e x64;
- assinar binários quando os secrets estiverem configurados;
- gerar ZIPs portáteis;
- gerar instaladores Inno Setup;
- gerar `SHA256SUMS.txt`;
- publicar a GitHub Release.

## Assinatura opcional

Cadastre estes secrets no GitHub:

```text
WINDOWS_CERTIFICATE_BASE64
WINDOWS_CERTIFICATE_PASSWORD
```

O primeiro deve conter o PFX convertido para Base64.

## Configuração visual das automações

A partir da versão 0.2.0, o fluxo principal de cadastro é visual. O assistente permite:

- selecionar planilhas Excel, CSV e TXT pelo explorador de arquivos;
- escolher a aba da planilha e visualizar uma amostra dos registros;
- definir chaves simples ou compostas;
- configurar SQLite, SQL Server, MySQL, PostgreSQL e Firebird;
- testar fontes e consultas de leitura;
- montar critérios de abertura, permanência, conclusão e suspensão com grupos E/OU;
- cadastrar várias ações, canais e destinatários;
- configurar grupos de contatos;
- criar mensagens com variáveis do registro;
- revisar e validar a automação antes de salvar.

O JSON continua disponível na aba **Avançado**, além das funções de importação e exportação. Um exemplo funcional está em:

```text
docs/examples/automation-clientes.json
```

Consulte também `docs/visual-editor.md`.


## Monitoramento administrativo de planilhas complexas

A versão 0.3.0 adiciona um fluxo específico para controles com várias seções, empresas, períodos, situações e cores na mesma aba.

Na tela principal:

- **Novo monitoramento guiado** cria uma única automação para toda a planilha;
- **Painel de planilhas** exibe a estrutura original, indicadores, empresas, situações e mudanças;
- **Legenda de situações** permite explicar códigos como `X`, `SM`, `M` e outros sem pressupor seu significado;
- **Gravar linha de base** e **Comparar alterações** mostram exatamente o que mudou entre duas leituras.

O sistema gera registros por empresa, por célula de período e por indicador agregado. Portanto, não é necessário cadastrar uma automação por cliente.

Consulte `docs/complex-workbook-monitoring.md` e o exemplo `docs/examples/source-planilha-matriz-contabil.json`.

## Segredos protegidos

As telas visuais de canais e bancos protegem automaticamente credenciais com DPAPI por usuário ou por máquina. Internamente, valores protegidos usam:

```text
dpapi:<conteudo-base64>
```

## Evolution API

O conector possui perfis V1 e V2 e permite sobrescrever os caminhos HTTP e o formato do payload. Isso evita acoplamento rígido a uma única revisão da Evolution API.

## Observação operacional

Não execute o Desktop e o Windows Service ao mesmo tempo usando o mesmo diretório de dados. O projeto possui trava de instância, mas o modo operacional deve ser escolhido por instalação.
## Experiência Desktop, tray e Windows Service

A versão 0.2.3 adiciona uma experiência operacional completa para o Windows:

- splash screen no início manual;
- inicialização silenciosa no tray ao entrar no Windows;
- tela **Sobre** com os contatos do desenvolvedor;
- tela **Configurações** para comportamento ao fechar, notificações, workers, dados e logs;
- gerenciamento do Windows Service com elevação administrativa somente quando necessária;
- parâmetros de intervalo, lote e paralelismo atualizados em tempo real;
- serviço incluído no instalador e dentro do pacote Desktop na pasta `service`.

As preferências do Desktop são gravadas em:

```text
%LocalAppData%\FlowSentinel\desktop-settings.json
```

Os parâmetros do serviço são gravados em:

```text
%ProgramData%\FlowSentinel\service-settings.json
```

Consulte `docs/desktop-experience.md` para os argumentos de inicialização e o fluxo de administração do serviço.
