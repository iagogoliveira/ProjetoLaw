using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForensicCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "platform",
                table: "holidays",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "span_days",
                schema: "platform",
                table: "holidays",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "state_code",
                schema: "platform",
                table: "holidays",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("150e8013-68ba-8901-8aeb-25847bebe326"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("325b2ff0-a806-8040-ac82-9debfd544345"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("3ce60634-65f5-81c5-967a-079f63d9e6dc"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("56c45ce7-cdb7-8262-bb60-1175d20a9ae7"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("6889e49a-93e0-854b-a012-6e4e1c1d56c6"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("7b4f5cbf-80e7-8adf-9d52-69db35b3decc"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("9b4f671b-da73-83fd-9541-16295f4fd8a1"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("be8709e9-fc92-8d17-ba36-b12e3806840b"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("c78aeb20-19dd-8238-9dad-fdee8eea890b"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.UpdateData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("ee30731b-33e3-88ad-ae27-7195aa241602"),
                columns: new[] { "kind", "span_days", "state_code" },
                values: new object[] { "Holiday", 1, null });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "holidays",
                columns: new[] { "id", "day", "easter_offset_days", "kind", "legal_basis", "month", "name", "rule", "scope", "span_days", "state_code", "valid_from_year", "valid_to_year" },
                values: new object[,]
                {
                    { new Guid("1970d9a1-f0fd-8b06-a58f-43f9f668334b"), 13, null, "Holiday", "Constituição do Estado do Amapá", 9, "Criação do Território Federal do Amapá", "FixedDate", "State", 1, "AP", 1991, null },
                    { new Guid("19b37bdc-4dfc-8009-abe0-32fa58b0a193"), 20, null, "Suspension", "CPC, art. 220", 12, "Suspensão dos prazos (recesso forense)", "FixedDate", "National", 32, null, 2016, null },
                    { new Guid("1ffd2abe-dfae-81b8-bec2-d6faea7d654c"), null, 8, "Holiday", "Lei estadual 11.010/2019", null, "Nossa Senhora da Penha", "EasterRelative", "State", 1, "ES", 2020, null },
                    { new Guid("21136135-2085-8643-b7a8-9af7074f8cef"), 23, null, "Holiday", "Lei estadual 1.538/2004", 1, "Dia do Evangélico", "FixedDate", "State", 1, "AC", 2004, null },
                    { new Guid("305b5cbd-e764-845d-a7c7-f7ef305463ad"), 11, null, "Holiday", "Lei 5.010/1966, art. 62, IV", 8, "Dia da criação dos cursos jurídicos", "FixedDate", "FederalJustice", 1, null, 1967, null },
                    { new Guid("34d21d69-2ff3-8911-b6bb-c695ac6cba6d"), 2, null, "Holiday", "Constituição do Estado da Bahia, art. 6º, § 3º", 7, "Independência da Bahia", "FixedDate", "State", 1, "BA", 1989, null },
                    { new Guid("3a19f119-b2b0-8245-a763-8bf1fd05c842"), 30, null, "Holiday", "Lei distrital 963/1995", 11, "Dia do Evangélico", "FixedDate", "State", 1, "DF", 1995, null },
                    { new Guid("3a6c9bdc-6a5f-8828-b8c9-968fe9e82826"), null, -47, "Holiday", "Lei estadual 5.243/2008", null, "Terça-feira de Carnaval", "EasterRelative", "State", 1, "RJ", 2009, null },
                    { new Guid("3ffdc618-b886-8d16-a85e-5840420218c5"), 30, null, "Holiday", "Lei estadual 7.530/2013", 11, "Dia do Evangélico", "FixedDate", "State", 1, "AL", 2014, null },
                    { new Guid("46796972-8847-87d4-8328-ba8a057541ff"), 15, null, "Holiday", "Lei estadual 5.999/1996", 8, "Adesão do Pará à Independência", "FixedDate", "State", 1, "PA", 1996, null },
                    { new Guid("476f749d-96e6-89f8-a855-f8a5cf74e984"), 20, null, "Holiday", "Constituição do Estado do Rio Grande do Sul, art. 6º, parágrafo único", 9, "Revolução Farroupilha", "FixedDate", "State", 1, "RS", 1989, null },
                    { new Guid("4d37a985-8eac-87db-803b-a9041f27084a"), 1, null, "Holiday", "Lei 5.010/1966, art. 62, IV", 11, "Dia de Todos os Santos", "FixedDate", "FederalJustice", 1, null, 1967, null },
                    { new Guid("4faf27e8-4a3d-8134-9791-a87b62cc9cde"), null, 60, "Holiday", "Lei estadual 11.002/2025", null, "Corpus Christi", "EasterRelative", "State", 1, "RJ", 2026, null },
                    { new Guid("5053c96d-56b4-8d57-9f02-95100439b84f"), 8, null, "Holiday", "Lei estadual 1.411/2001", 3, "Dia Internacional da Mulher", "FixedDate", "State", 1, "AC", 2001, null },
                    { new Guid("54cce250-cd30-861b-9e66-268b424b24de"), 6, null, "Holiday", "Lei estadual 16.241/2017", 3, "Data Magna de Pernambuco (Revolução de 1817)", "FixedDate", "State", 1, "PE", 2018, null },
                    { new Guid("5e30ac51-f5bc-861c-a10e-fe286454f5cf"), 15, null, "Holiday", "Lei estadual 14/1964", 6, "Aniversário do Acre", "FixedDate", "State", 1, "AC", 1964, null },
                    { new Guid("60967c46-56ac-8cf9-a197-1990ae3994e6"), 5, null, "Holiday", "Lei estadual 3.489/1967", 8, "Fundação do Estado da Paraíba", "FixedDate", "State", 1, "PB", 1967, null },
                    { new Guid("60a6ad27-20e4-84bb-9743-8739a5ebc223"), 18, null, "Holiday", "Lei estadual 1.026/2001", 6, "Dia do Evangélico", "FixedDate", "State", 1, "RO", 2002, null },
                    { new Guid("6580b955-0204-8bc1-b44b-687f884c3afd"), 8, null, "Holiday", "Lei 5.010/1966, art. 62, IV", 12, "Dia da Justiça", "FixedDate", "FederalJustice", 1, null, 1967, null },
                    { new Guid("66298bf0-85ed-873c-8921-8e6bc5bbd2ab"), 8, null, "Holiday", "Lei estadual 627/1993", 9, "Nossa Senhora da Natividade", "FixedDate", "State", 1, "TO", 1994, null },
                    { new Guid("6748dbef-b7a9-8b89-9bc0-8dc6e5484cce"), 19, null, "Holiday", "Lei estadual 667/2002", 3, "Dia de São José", "FixedDate", "State", 1, "AP", 2002, null },
                    { new Guid("694b6af2-c900-8cb9-9982-f7a3a7173ff1"), 4, null, "Holiday", "Lei estadual 2.291/2010", 1, "Criação do Estado de Rondônia", "FixedDate", "State", 1, "RO", 2011, null },
                    { new Guid("738ce185-0e0f-8e6d-abf3-a830680c998e"), 5, null, "Holiday", "Constituição do Estado de Roraima, art. 9º", 10, "Criação do Estado de Roraima", "FixedDate", "State", 1, "RR", 1991, null },
                    { new Guid("778db1d1-d5b1-88d2-be30-2d84c938f03f"), 9, null, "Holiday", "Lei estadual 9.497/1997", 7, "Revolução Constitucionalista de 1932", "FixedDate", "State", 1, "SP", 1997, null },
                    { new Guid("7fa4e684-7ccf-859f-a9f4-483d4174c308"), 8, null, "Holiday", "Constituição do Estado de Sergipe, art. 269", 7, "Emancipação Política de Sergipe", "FixedDate", "State", 1, "SE", 1989, null },
                    { new Guid("7ff37277-a6c4-83f8-897e-9f07efce4844"), null, -48, "Holiday", "Lei 5.010/1966, art. 62, III", null, "Segunda e terça-feira de Carnaval", "EasterRelative", "FederalJustice", 2, null, 1967, null },
                    { new Guid("84a39dc8-941c-8c32-8979-975eb65592a9"), 23, null, "Holiday", "Lei estadual 5.198/2008", 4, "Dia de São Jorge", "FixedDate", "State", 1, "RJ", 2008, null },
                    { new Guid("a2340822-4383-8674-80f3-c7d6a945485e"), 24, null, "Holiday", "Lei estadual 5.508/1993", 6, "São João", "FixedDate", "State", 1, "AL", 1993, null },
                    { new Guid("a4e8322c-95b1-8b81-b4ad-c06bfd61f15a"), 28, null, "Holiday", "Lei estadual 2.457/1964", 7, "Adesão do Maranhão à Independência", "FixedDate", "State", 1, "MA", 1964, null },
                    { new Guid("a7ab48c4-5dec-85ea-9ee7-49fc1a19f314"), 3, null, "Holiday", "Lei estadual 8.913/2006", 10, "Mártires de Cunhaú e Uruaçu", "FixedDate", "State", 1, "RN", 2007, null },
                    { new Guid("c8bdd021-cd71-8c57-9aba-2f2c3a916266"), 29, null, "Holiday", "Lei estadual 5.509/1993", 6, "São Pedro", "FixedDate", "State", 1, "AL", 1993, null },
                    { new Guid("d342e488-82a6-82ac-bb36-7b6ccf1f7631"), 5, null, "Holiday", "Lei estadual 98/1989", 10, "Criação do Estado do Tocantins", "FixedDate", "State", 1, "TO", 1990, null },
                    { new Guid("df338c76-4962-8b5d-b96b-2af9296a54d8"), 20, null, "Holiday", "Lei 5.010/1966, art. 62, I", 12, "Recesso da Justiça Federal", "FixedDate", "FederalJustice", 18, null, 1967, null },
                    { new Guid("e06588d6-3f82-8853-8349-12b36f0ed5f6"), null, -4, "Holiday", "Lei 5.010/1966, art. 62, II", null, "Semana Santa (quarta-feira a domingo de Páscoa)", "EasterRelative", "FederalJustice", 5, null, 1967, null },
                    { new Guid("e7a66df6-eae8-8fb6-be5e-2bc262d33f23"), 5, null, "Holiday", "Lei estadual 25/1977", 9, "Elevação do Amazonas à categoria de Província", "FixedDate", "State", 1, "AM", 1977, null },
                    { new Guid("ec895e5b-dd3b-80e7-9758-29880488e633"), 15, null, "Holiday", "Lei estadual 4.509/2024", 4, "Senhor do Bonfim", "FixedDate", "State", 1, "TO", 2025, null },
                    { new Guid("f77037dc-a1c0-8d77-8c28-a8fbd179101c"), 5, null, "Holiday", "Lei estadual 1.526/2004", 9, "Dia da Amazônia", "FixedDate", "State", 1, "AC", 2004, null },
                    { new Guid("f7edd53e-71a7-8548-954d-658e3e5d9e03"), 25, null, "Holiday", "Constituição do Estado do Ceará, art. 18, parágrafo único", 3, "Data Magna do Ceará", "FixedDate", "State", 1, "CE", 1989, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_holidays_scope_state_code",
                schema: "platform",
                table: "holidays",
                columns: new[] { "scope", "state_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_holidays_scope_state_code",
                schema: "platform",
                table: "holidays");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("1970d9a1-f0fd-8b06-a58f-43f9f668334b"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("19b37bdc-4dfc-8009-abe0-32fa58b0a193"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("1ffd2abe-dfae-81b8-bec2-d6faea7d654c"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("21136135-2085-8643-b7a8-9af7074f8cef"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("305b5cbd-e764-845d-a7c7-f7ef305463ad"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("34d21d69-2ff3-8911-b6bb-c695ac6cba6d"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("3a19f119-b2b0-8245-a763-8bf1fd05c842"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("3a6c9bdc-6a5f-8828-b8c9-968fe9e82826"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("3ffdc618-b886-8d16-a85e-5840420218c5"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("46796972-8847-87d4-8328-ba8a057541ff"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("476f749d-96e6-89f8-a855-f8a5cf74e984"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("4d37a985-8eac-87db-803b-a9041f27084a"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("4faf27e8-4a3d-8134-9791-a87b62cc9cde"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("5053c96d-56b4-8d57-9f02-95100439b84f"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("54cce250-cd30-861b-9e66-268b424b24de"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("5e30ac51-f5bc-861c-a10e-fe286454f5cf"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("60967c46-56ac-8cf9-a197-1990ae3994e6"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("60a6ad27-20e4-84bb-9743-8739a5ebc223"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("6580b955-0204-8bc1-b44b-687f884c3afd"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("66298bf0-85ed-873c-8921-8e6bc5bbd2ab"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("6748dbef-b7a9-8b89-9bc0-8dc6e5484cce"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("694b6af2-c900-8cb9-9982-f7a3a7173ff1"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("738ce185-0e0f-8e6d-abf3-a830680c998e"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("778db1d1-d5b1-88d2-be30-2d84c938f03f"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("7fa4e684-7ccf-859f-a9f4-483d4174c308"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("7ff37277-a6c4-83f8-897e-9f07efce4844"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("84a39dc8-941c-8c32-8979-975eb65592a9"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("a2340822-4383-8674-80f3-c7d6a945485e"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("a4e8322c-95b1-8b81-b4ad-c06bfd61f15a"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("a7ab48c4-5dec-85ea-9ee7-49fc1a19f314"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("c8bdd021-cd71-8c57-9aba-2f2c3a916266"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("d342e488-82a6-82ac-bb36-7b6ccf1f7631"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("df338c76-4962-8b5d-b96b-2af9296a54d8"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("e06588d6-3f82-8853-8349-12b36f0ed5f6"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("e7a66df6-eae8-8fb6-be5e-2bc262d33f23"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("ec895e5b-dd3b-80e7-9758-29880488e633"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("f77037dc-a1c0-8d77-8c28-a8fbd179101c"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "holidays",
                keyColumn: "id",
                keyValue: new Guid("f7edd53e-71a7-8548-954d-658e3e5d9e03"));

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "platform",
                table: "holidays");

            migrationBuilder.DropColumn(
                name: "span_days",
                schema: "platform",
                table: "holidays");

            migrationBuilder.DropColumn(
                name: "state_code",
                schema: "platform",
                table: "holidays");
        }
    }
}
