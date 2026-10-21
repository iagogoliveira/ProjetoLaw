using System.Globalization;
using System.Net;
using System.Text;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Platform.Contracts.Notifications;

namespace LexFlow.Platform.Infrastructure.Notifications;

internal static class DailyDigestMessage
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static EmailMessage Create(string email, DateOnly day, IReadOnlyList<DigestSection> sections)
    {
        var body = new StringBuilder();
        body.Append("Resumo diário LexFlow — ").AppendLine(day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        foreach (var section in sections)
        {
            body.AppendLine().Append(Label(section.Kind)).Append(": ").AppendLine(section.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var item in section.Items)
            {
                body.Append("- ").Append(TimeZoneInfo.ConvertTime(item.At, Zone).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture))
                    .Append(" — ").AppendLine(item.Label);
            }
            if (section.Count > section.Items.Count) { body.AppendLine("Consulte os demais itens no sistema."); }
        }
        body.AppendLine().AppendLine("Conteúdo conforme seu acesso atual no escritório. Confira os dados completos no LexFlow.");
        var text = body.ToString();
        return new EmailMessage([email], "LexFlow — resumo diário", "<pre>" + WebUtility.HtmlEncode(text) + "</pre>", text);
    }
    private static string Label(string kind) => kind switch
    {
        "agenda" => "Agenda do dia",
        "deadlines" => "Prazos dos próximos sete dias",
        "publications" => "Publicações pendentes",
        _ => kind,
    };
}
