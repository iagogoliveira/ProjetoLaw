using System.Net;
using System.Text.Json;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

public sealed class ProblemDetailsTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await TestWebHost.StartAsync(app =>
        {
            app.MapGet("/errors/{type}", (ErrorType type) => Result.Failure<int>(new Error("sample.failure", "Falhou.", type)).ToHttpResult());
            app.MapGet("/validation", () => Result.Failure<int>(new ValidationError(
            [
                new FieldError("title", "case.title_required", "Informe o título."),
                new FieldError("title", "case.title_too_long", "Título muito longo."),
                new FieldError("clientId", "case.client_required", "Informe o cliente."),
            ])).ToHttpResult());
            app.MapGet("/ok", () => Result.Ok(7).ToHttpResult());
            app.MapGet("/no-content", () => Result.Success.ToHttpResult());
            app.MapGet("/boom", IResult () => throw new InvalidOperationException("segredo interno"));
        });
        _client = _app.GetTestClient();
    }

    [Theory]
    [InlineData(ErrorType.Validation, HttpStatusCode.BadRequest)]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorType.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(ErrorType.BusinessRule, HttpStatusCode.UnprocessableEntity)]
    public async Task Error_type_maps_to_status_with_code_and_trace(ErrorType type, HttpStatusCode status)
    {
        var (response, body) = await GetProblemAsync($"/errors/{type}");

        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        body.GetProperty("code").GetString().ShouldBe("sample.failure");
        body.GetProperty("title").GetString().ShouldBe("Falhou.");
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Validation_errors_are_grouped_by_field_with_codes()
    {
        var (response, body) = await GetProblemAsync("/validation");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("code").GetString().ShouldBe(ValidationError.DefaultCode);
        var errors = body.GetProperty("errors");
        errors.GetProperty("title").GetArrayLength().ShouldBe(2);
        errors.GetProperty("title")[0].GetProperty("code").GetString().ShouldBe("case.title_required");
        errors.GetProperty("clientId")[0].GetProperty("message").GetString().ShouldBe("Informe o cliente.");
    }

    [Fact]
    public async Task Success_maps_to_200_and_204()
    {
        (await _client.GetStringAsync(new Uri("/ok", UriKind.Relative), TestContext.Current.CancellationToken)).ShouldBe("7");
        (await _client.GetAsync(new Uri("/no-content", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_without_leaking_details()
    {
        var (response, body) = await GetProblemAsync("/boom");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        body.GetProperty("code").GetString().ShouldBe(ProblemDetailsSetup.UnexpectedErrorCode);
        body.ToString().ShouldNotContain("segredo interno");
        body.ToString().ShouldNotContain("InvalidOperationException");
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var (response, body) = await GetProblemAsync("/nao-existe");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().ShouldBe("http.not_found");
    }

    private async Task<(HttpResponseMessage Response, JsonElement Body)> GetProblemAsync(string path)
    {
        var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, JsonDocument.Parse(json).RootElement.Clone());
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
