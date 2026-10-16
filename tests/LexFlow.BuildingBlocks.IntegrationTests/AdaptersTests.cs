using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>Adaptadores contra os serviços reais: armazenamento S3, SMTP (Mailpit), ClamAV e Hangfire.</summary>
public sealed class AdaptersTests(AdaptersFixture fixture) : IClassFixture<AdaptersFixture>
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private IServiceProvider Services => fixture.Host.Services;

    [Fact]
    public async Task Object_storage_puts_reads_and_deletes_under_the_tenant_path()
    {
        var storage = Services.GetRequiredService<IObjectStorage>();
        var key = new ObjectKey(Guid.CreateVersion7(), "Documents", Guid.CreateVersion7());
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("petição inicial")))
        {
            await storage.PutAsync(key, content, "text/plain", Ct);
        }

        (await storage.ExistsAsync(key, Ct)).ShouldBeTrue();
        await using (var stream = await storage.OpenReadAsync(key, Ct))
        using (var reader = new StreamReader(stream))
        {
            (await reader.ReadToEndAsync(Ct)).ShouldBe("petição inicial");
        }

        key.Path.ShouldBe($"{key.OrganizationId}/documents/{key.Id}");
        await storage.DeleteAsync(key, Ct);
        (await storage.ExistsAsync(key, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Presigned_urls_allow_direct_upload_and_download()
    {
        var storage = Services.GetRequiredService<IObjectStorage>();
        var key = new ObjectKey(Guid.CreateVersion7(), "documents", Guid.CreateVersion7());
        using var http = new HttpClient();

        var uploadUrl = await storage.CreateUploadUrlAsync(key, "application/pdf");
        using var body = new ByteArrayContent([1, 2, 3, 4]);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        (await http.PutAsync(uploadUrl, body, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var downloadUrl = await storage.CreateDownloadUrlAsync(key);
        (await http.GetByteArrayAsync(downloadUrl, Ct)).ShouldBe([1, 2, 3, 4]);
        downloadUrl.Query.ShouldContain("X-Amz-Expires=300");
    }

    [Fact]
    public async Task Email_is_delivered_by_smtp()
    {
        var subject = $"Teste LexFlow {Guid.CreateVersion7()}";

        await Services.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage(["advogada@escritorio.example"], subject, "<p>Prazo amanhã</p>", "Prazo amanhã"), Ct);

        using var http = new HttpClient { BaseAddress = fixture.MailpitApi };
        var json = await http.GetStringAsync(new Uri($"/api/v1/search?query=subject:\"{subject}\"", UriKind.Relative), Ct);
        var messages = JsonDocument.Parse(json).RootElement.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(1);
        messages[0].GetProperty("To")[0].GetProperty("Address").GetString().ShouldBe("advogada@escritorio.example");
    }

    [Fact]
    public async Task Clean_file_passes_the_antivirus()
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("documento comum"));

        (await Services.GetRequiredService<IFileScanner>().ScanAsync(content, Ct)).Status.ShouldBe(FileScanStatus.Clean);
    }

    [Fact]
    public async Task Eicar_test_file_is_detected_as_infected()
    {
        // Arquivo de teste padrão EICAR, guardado em Base64 para não ser posto em quarentena no disco.
        var eicar = Convert.FromBase64String("WDVPIVAlQEFQWzRcUFpYNTQoUF4pN0NDKTd9JEVJQ0FSLVNUQU5EQVJELUFOVElWSVJVUy1URVNULUZJTEUhJEgrSCo=");
        await using var content = new MemoryStream(eicar);

        var result = await Services.GetRequiredService<IFileScanner>().ScanAsync(content, Ct);

        result.Status.ShouldBe(FileScanStatus.Infected);
        result.Signature.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Job_runs_with_the_tenant_and_correlation_of_who_scheduled_it()
    {
        var organization = Guid.CreateVersion7();
        var marker = Guid.CreateVersion7().ToString();

        using (AmbientContext.Begin(organization, "job-test"))
        {
            await using var scope = Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().Enqueue<RecordingJob>(job => job.RunAsync(marker));
        }

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!RecordingJob.Runs.ContainsKey(marker) && elapsed.Elapsed < TimeSpan.FromSeconds(60))
        {
            await Task.Delay(250, Ct);
        }

        RecordingJob.Runs.ShouldContainKey(marker);
        RecordingJob.Runs[marker].ShouldBe((organization, "job-test"));
    }
}
