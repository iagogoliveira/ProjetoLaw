using Microsoft.AspNetCore.Http;

namespace LexFlow.Web.Common.Results;

/// <summary>Resposta de operação longa (seção 4.4): <c>202 Accepted</c> com <c>Location: /api/v1/jobs/{id}</c>.</summary>
public static class JobResults
{
    public static string LocationFor(Guid jobId) => $"/api/v1/jobs/{jobId:D}";

    public static IResult Accepted(Guid jobId) =>
        TypedResults.Accepted(LocationFor(jobId), new AcceptedJob(jobId, "queued"));
}

public sealed record AcceptedJob(Guid Id, string Status);
