# ADR 0004 — RustFS no lugar do MinIO como armazenamento S3 local

- **Status:** Aceita (revisão humana em 2026-10-16)
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

As seções 2.3, 8.1 e 8.3 usam o MinIO como armazenamento de objetos local e como opção para o VPS. Em 2026-10-16:

- `minio/minio` não existe mais no Docker Hub (`pull access denied ... repository does not exist`);
- `quay.io/minio/minio` exige autenticação (HTTP 401).

O MinIO deixou de distribuir imagens e binários da edição comunitária. Sem uma imagem, o AppHost não sobe, e o critério de aceite "`/health/ready` verifica ... MinIO" não pode ser atendido como está escrito.

O código depende só da **API S3** (`AWSSDK.S3`, interface `IObjectStorage`, seção 8.3), então o container pode ser trocado sem mudar código de negócio.

## Opções avaliadas

| Opção | Licença | Observação |
| --- | --- | --- |
| **RustFS** (`rustfs/rustfs:1.0.2`) | Apache-2.0 | Substituto direto do MinIO: porta 9000 (S3), 9001 (console), `/health`, credenciais via `RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY` |
| SeaweedFS | Apache-2.0 | Maduro, mas a configuração do gateway S3 e das credenciais é mais trabalhosa |
| Garage | AGPL-3.0 | Licença mais restritiva |
| Compilar o MinIO a partir do código (AGPL-3.0) | AGPL-3.0 | Exigiria manter um build e um registry próprios |

## Decisão

Usar **RustFS 1.0.2** no AppHost (recurso `storage`) e nos testes de integração. A configuração da aplicação continua genérica (`Storage:ServiceUrl`, `Storage:AccessKey`, `Storage:SecretKey`), e o health check (`ObjectStorageHealthCheck`) usa só `ListBuckets` da API S3.

## Consequências

- Os critérios de aceite da Etapa 0 que citam o MinIO são atendidos pelo armazenamento S3 (RustFS). O nome do recurso é `storage`, e não `minio`, para não amarrar o código a um produto.
- **Revisar antes do primeiro deploy (Opção A, VPS):** escolher entre RustFS, SeaweedFS ou um bucket S3 externo (R2, B2). A ADR de escolha de provedor deve tratar disso.
- A especificação (seções 2.3, 8.1 e 8.3) deve ser atualizada por quem a mantém, se esta ADR for aceita.
