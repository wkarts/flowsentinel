# Experiência Desktop, Splash, Tray e Serviço

## Identidade do desenvolvedor

A logo institucional incluída nesta versão pertence ao desenvolvedor e é usada apenas no splash screen e na tela Sobre.

- Empresa: WWSoftware's Sistemas e Tecnologias
- Desenvolvedor: Wallace Kleiton
- GitHub: @wkarts
- WhatsApp: +55 75 98844-9231
- E-mail: wkarts@gmail.com

A identidade principal do produto FlowSentinel permanece independente.

## Argumentos do executável

```text
--startup    Indica inicialização automática pelo Windows.
--tray       Força o início oculto na bandeja.
--show       Força a abertura do painel.
--no-splash  Não exibe o splash screen.
```

O registro do usuário utiliza:

```text
"FlowSentinel.exe" --startup --tray
```

## Preferências locais

O arquivo abaixo controla splash, tray, fechamento e processamento do Desktop:

```text
%LocalAppData%\FlowSentinel\desktop-settings.json
```

As alterações de intervalo e paralelismo são consultadas pelos workers durante a execução e não exigem reinicialização do Desktop.

## Windows Service

O instalador e o pacote Desktop incluem:

```text
service\FlowSentinel.Service.exe
service\install-service.ps1
service\uninstall-service.ps1
```

A tela Configurações permite:

- consultar o estado;
- instalar ou atualizar;
- iniciar;
- parar;
- remover;
- escolher o tipo de inicialização;
- definir o diretório de dados;
- aplicar os parâmetros de processamento.

Operações administrativas solicitam elevação pelo UAC.

O serviço utiliza por padrão:

```text
%ProgramData%\FlowSentinel
```

O arquivo `service-settings.json` é relido automaticamente, permitindo alterar o intervalo do agendador, o intervalo da fila, o tamanho do lote e o paralelismo sem reinstalar o serviço.
