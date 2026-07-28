# Padrão de entrega das evoluções

Toda evolução do FlowSentinel deve ser entregue de forma completa, sem depender de uma solicitação adicional.

## Artefatos obrigatórios

- projeto-fonte completo da versão;
- pacote contendo somente os arquivos alterados;
- patch compatível com `git apply`;
- commit compatível com `git am`;
- script PowerShell de aplicação;
- relatório técnico;
- checksums SHA-256;
- branch;
- título e descrição do Pull Request;
- mensagem de commit;
- mensagem de merge.

## Identidade visual do desenvolvedor

Quando a entrega envolver splash, About, instalador, documentação institucional ou material de distribuição, incluir também:

- logo PNG em alta resolução;
- logo PNG 512 px;
- logo PNG 256 px;
- ícone ICO multirresolução;
- pacote ZIP das variações.

A logo da WWSoftware's Sistemas e Tecnologias pertence ao desenvolvedor e não deve substituir automaticamente a identidade principal do produto.
