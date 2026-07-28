# Política de segurança

## Versões suportadas

Somente a versão estável mais recente recebe correções de segurança.

## Relato responsável

Não abra issue pública contendo credenciais, tokens, connection strings ou dados pessoais.
Envie o relato diretamente ao mantenedor do repositório por um canal privado.

## Credenciais

O FlowSentinel suporta valores protegidos com Windows DPAPI. Campos sensíveis podem ser armazenados com o prefixo `dpapi:`. Nunca confirme segredos em arquivos JSON versionados.
