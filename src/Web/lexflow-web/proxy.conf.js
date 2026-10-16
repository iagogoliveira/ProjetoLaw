// Encaminha /api para a API. Sob o Aspire, a URL vem de services__api__{https|http}__0.
const target =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'https://localhost:7101';

module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
};
