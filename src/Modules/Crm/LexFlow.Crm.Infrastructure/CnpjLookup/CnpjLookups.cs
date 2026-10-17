using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain.People;
using Microsoft.Extensions.Logging;

namespace LexFlow.Crm.Infrastructure.CnpjLookup;

/// <summary>Configuração da consulta de CNPJ (seção <c>Crm:CnpjLookup</c>).</summary>
public sealed class CnpjLookupSettings
{
    public const string SectionName = "Crm:CnpjLookup";

    /// <summary><c>BrasilApi</c> (padrão) ou <c>Fake</c> (testes e ambientes sem internet).</summary>
    public string Provider { get; init; } = "BrasilApi";

    public Uri BaseAddress { get; init; } = new("https://brasilapi.com.br/");
}

/// <summary>
/// BrasilAPI (decisão de 2026-10-17): gratuita, sem chave, com os dados da Receita Federal. Não tem SLA, então
/// falha de rede, tempo esgotado ou erro do servidor viram "indisponível" e o cadastro segue manual.
/// </summary>
internal sealed partial class BrasilApiCnpjLookup(HttpClient http, ILogger<BrasilApiCnpjLookup> logger) : ICnpjLookup
{
    public async Task<CnpjLookupResult> LookupAsync(string cnpj, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri($"api/cnpj/v1/{Uri.EscapeDataString(cnpj)}", UriKind.Relative), cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return CnpjLookupResult.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, (int)response.StatusCode);
                return CnpjLookupResult.ServiceUnavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<BrasilApiCompany>(cancellationToken);
            return body is null ? CnpjLookupResult.NotFound : CnpjLookupResult.Found(body.ToRegistration(cnpj));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogFailure(logger, ex);
            return CnpjLookupResult.ServiceUnavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consulta de CNPJ indisponível (BrasilAPI respondeu {Status}).")]
    private static partial void LogUnavailable(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consulta de CNPJ indisponível (BrasilAPI).")]
    private static partial void LogFailure(ILogger logger, Exception exception);

    internal sealed record BrasilApiCompany(
        [property: JsonPropertyName("razao_social")] string? LegalName,
        [property: JsonPropertyName("nome_fantasia")] string? TradeName,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("ddd_telefone_1")] string? Phone,
        [property: JsonPropertyName("descricao_situacao_cadastral")] string? Status,
        [property: JsonPropertyName("logradouro")] string? Street,
        [property: JsonPropertyName("numero")] string? Number,
        [property: JsonPropertyName("complemento")] string? Complement,
        [property: JsonPropertyName("bairro")] string? District,
        [property: JsonPropertyName("municipio")] string? City,
        [property: JsonPropertyName("uf")] string? State,
        [property: JsonPropertyName("cep")] string? PostalCode)
    {
        public CompanyRegistration ToRegistration(string cnpj) => new(
            cnpj,
            LegalName?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(TradeName) ? null : TradeName.Trim(),
            string.IsNullOrWhiteSpace(Email) ? null : Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Status,
            string.IsNullOrWhiteSpace(Street) || string.IsNullOrWhiteSpace(City) || string.IsNullOrWhiteSpace(State)
                ? null
                : new AddressInput(Street.Trim(), Number?.Trim(), Complement?.Trim(), District?.Trim(), City.Trim(), State.Trim(), PostalCode ?? string.Empty, IsBilling: true));
    }
}

/// <summary>
/// Consulta falsa e determinística (testes e ambientes sem internet): todo CNPJ válido existe, menos os que
/// terminam em <c>0000</c> antes dos dígitos verificadores, que "não existem".
/// </summary>
internal sealed class FakeCnpjLookup : ICnpjLookup
{
    public Task<CnpjLookupResult> LookupAsync(string cnpj, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(cnpj[8..12], "0000", StringComparison.Ordinal)
            ? CnpjLookupResult.NotFound
            : CnpjLookupResult.Found(new CompanyRegistration(
                cnpj,
                $"Empresa {cnpj[..8]} Ltda.",
                $"Empresa {cnpj[..4]}",
                $"contato{cnpj[..4]}@exemplo.com.br",
                "1133334444",
                "ATIVA",
                new AddressInput("Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", "01310100", IsBilling: true))));
}
