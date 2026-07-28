# flowsentinel

Repositorio criado online e pre-configurado para:

- Codigo-fonte
- Releases GitHub
- Docker Images
- GitHub Packages / GHCR

## Visibilidade configurada

Repository: private

## Imagem Docker GHCR

ghcr.io/wkarts/flowsentinel:latest

## Pull da imagem

Se o package estiver publico:

docker pull ghcr.io/wkarts/flowsentinel:latest

Se o package estiver privado:

echo SEU_TOKEN_GITHUB | docker login ghcr.io -u SEU_USUARIO --password-stdin
docker pull ghcr.io/wkarts/flowsentinel:latest

## Observacoes

- Releases seguem a visibilidade do repositorio.
- O package Docker/GHCR so existe depois da primeira publicacao da imagem.
- A imagem e vinculada ao repositorio usando o label OCI:

org.opencontainers.image.source=https://github.com/wkarts/flowsentinel

Apos a primeira publicacao, valide em:

GitHub -> Profile/Organization -> Packages -> Package settings

Confira:

Repository conectado
Manage Actions access / Inherit access from source repository
Visibility: Public ou Private
