# Correção complementar de validação — agregados de matriz

## Contexto

Foram analisados integralmente os pacotes:

- `logs_82432687883.zip`;
- `logs_82432691966.zip`;
- `logs_82432691981.zip`.

Os jobs **Build e testes** e **Análise C#** foram interrompidos pela mesma falha de compilação:

```text
ExcelSectionedMatrixParser.cs(387,49): error CS0103:
The name 'settings' does not exist in the current context

ExcelSectionedMatrixParser.cs(387,92): error CS0103:
The name 'settings' does not exist in the current context
```

## Causa raiz

Durante a generalização do modo `SectionedMatrix`, o método
`AddCompanyStatusAggregates` passou a usar `EntityPluralName` para definir a
unidade administrativa do agregado. Entretanto, a assinatura do método não
recebeu a instância de `ExcelMatrixSettings` utilizada pelo método chamador.

A falha era exclusivamente de escopo e impedia a compilação antes da execução
dos testes.

## Correção aplicada

- `CreateAggregateRecords` passa a encaminhar `ExcelMatrixSettings` para todas
  as chamadas de `AddCompanyStatusAggregates`.
- `AddCompanyStatusAggregates` recebe explicitamente as configurações da matriz.
- O rótulo continua genérico e configurável por `EntityPluralName`.
- Nenhum termo do modelo RP-102 foi introduzido no núcleo.

## Teste de regressão

O teste de matriz genérica de equipamentos agora valida que o agregado:

```text
Metric = EntitiesByCurrentValue
Scope = Global
Status = PENDENTE
Unit = Equipamentos
Count = 1
```

Isso protege simultaneamente contra:

- nova perda de escopo da configuração;
- retorno acidental ao rótulo fixo `Registros`;
- acoplamento a empresas ou ao modelo contábil.

## Compatibilidade

A correção não altera:

- esquema SQLite;
- formato JSON das automações;
- modo Excel `Table`;
- leitores CSV, TXT ou bancos de dados;
- canais de notificação;
- Desktop, Windows Service, win-x86 ou win-x64;
- versão `0.3.0` da funcionalidade ainda em validação.

## Limitação da validação local

O SDK .NET 10.0.301 não está disponível no ambiente de correção e o acesso
externo para instalá-lo está bloqueado. Foram executadas validações estruturais,
de formato, integridade e aplicação do patch. A confirmação semântica final de
build, testes, CodeQL e publicações Windows deve ocorrer no GitHub Actions após
o push.
