using System.Net;
using System.Security.Cryptography;
using System.Text;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Passwords;
using Microsoft.Extensions.Logging.Abstractions;

namespace LexFlow.Identity.UnitTests;

public sealed class PwnedPasswordValidatorTests
{
    private const string Password = "correct horse battery staple";

    private static readonly ApplicationUser User = ApplicationUser.Register(Guid.CreateVersion7(), "ana@exemplo.com.br", "Ana", DateTimeOffset.UnixEpoch);

#pragma warning disable CA5350 // o protocolo da API usa SHA-1
    private static readonly string Hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Password)));
#pragma warning restore CA5350

    private sealed class FakeApi(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static (PwnedPasswordValidator Validator, FakeApi Api) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var api = new FakeApi(respond);
        var http = new HttpClient(api) { BaseAddress = PwnedPasswordValidator.BaseAddress };
        return (new PwnedPasswordValidator(http, NullLogger<PwnedPasswordValidator>.Instance), api);
    }

    private static HttpResponseMessage Range(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Breached_password_is_rejected_and_only_the_hash_prefix_leaves_the_server()
    {
        var (validator, api) = Create(_ => Range($"0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n{Hash[5..].ToLowerInvariant()}:3730471\r\n"));

        var result = await validator.ValidateAsync(null!, User, Password);

        result.Errors.Single().Code.ShouldBe(PasswordPolicy.BreachedErrorCode);
        var request = api.Requests.Single();
        request.RequestUri!.AbsoluteUri.ShouldBe($"https://api.pwnedpasswords.com/range/{Hash[..5]}");
        request.Headers.GetValues("Add-Padding").Single().ShouldBe("true");
    }

    [Fact]
    public async Task Padding_entry_with_zero_count_is_not_a_breach()
    {
        var (validator, _) = Create(_ => Range($"{Hash[5..]}:0\r\n"));

        (await validator.ValidateAsync(null!, User, Password)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Password_missing_from_the_range_is_accepted()
    {
        var (validator, _) = Create(_ => Range("0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n"));

        (await validator.ValidateAsync(null!, User, Password)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Unavailable_api_does_not_block_the_password()
    {
        var (failing, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (throwing, _) = Create(_ => throw new HttpRequestException("sem rede"));

        (await failing.ValidateAsync(null!, User, Password)).Succeeded.ShouldBeTrue();
        (await throwing.ValidateAsync(null!, User, Password)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Empty_password_is_left_to_the_other_validators()
    {
        var (validator, api) = Create(_ => Range(string.Empty));

        (await validator.ValidateAsync(null!, User, string.Empty)).Succeeded.ShouldBeTrue();
        api.Requests.ShouldBeEmpty();
    }
}
