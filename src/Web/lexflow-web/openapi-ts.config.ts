import { defineConfig } from '@hey-api/openapi-ts';

// Cliente gerado do contrato versionado (docs/openapi/v1.json). NUNCA editar src/app/api à mão:
// regenere com `npm run generate:api` depois de atualizar o contrato.
export default defineConfig({
  input: '../../../docs/openapi/v1.json',
  output: { path: 'src/app/api', format: 'prettier' },
  plugins: ['@hey-api/client-angular', '@hey-api/typescript', '@hey-api/sdk'],
});
