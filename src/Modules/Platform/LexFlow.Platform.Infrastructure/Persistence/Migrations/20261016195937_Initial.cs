using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    acronym = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    month = table.Column<int>(type: "integer", nullable: true),
                    day = table.Column<int>(type: "integer", nullable: true),
                    easter_offset_days = table.Column<int>(type: "integer", nullable: true),
                    legal_basis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valid_from_year = table.Column<int>(type: "integer", nullable: false),
                    valid_to_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "courts",
                columns: new[] { "id", "acronym", "code", "name", "segment", "state" },
                values: new object[,]
                {
                    { new Guid("01cd9c61-8419-86dc-bdfc-e1cee921feb6"), "TRT9", "09", "Tribunal Regional do Trabalho da 9ª Região", 5, "PR" },
                    { new Guid("038a604c-673c-8131-bc37-421578cbb5a6"), "STF", "00", "Supremo Tribunal Federal", 1, null },
                    { new Guid("04bb0844-5996-8602-8d1d-cca27d3a3976"), "TJPE", "17", "Tribunal de Justiça (PE)", 8, "PE" },
                    { new Guid("05b6b831-dc00-8198-998a-e42c89568e58"), "TRE-RS", "21", "Tribunal Regional Eleitoral (RS)", 6, "RS" },
                    { new Guid("08d38c51-e9ed-822d-9532-1b8e706cf7d6"), "TRE-AC", "01", "Tribunal Regional Eleitoral (AC)", 6, "AC" },
                    { new Guid("0b587f1d-763b-82e0-96f3-1b46b4bc059b"), "8ª CJM", "08", "8ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("0dfc5a93-173c-8ae3-a59b-075503a46542"), "TJGO", "09", "Tribunal de Justiça (GO)", 8, "GO" },
                    { new Guid("1734618f-16a5-84c9-a925-6c0660026d78"), "TJRO", "22", "Tribunal de Justiça (RO)", 8, "RO" },
                    { new Guid("1895fe55-ae5b-8a55-b8d7-10f69ca603d8"), "TRT8", "08", "Tribunal Regional do Trabalho da 8ª Região", 5, "PA" },
                    { new Guid("191f1932-5e33-8da1-bfff-8e0991502f71"), "9ª CJM", "09", "9ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("19d4a0b4-8a3a-8a4b-a7fa-f5b17d97b5da"), "TRE-PE", "17", "Tribunal Regional Eleitoral (PE)", 6, "PE" },
                    { new Guid("1aff10fa-123c-85e6-ab4f-f1fa888d506d"), "TRT15", "15", "Tribunal Regional do Trabalho da 15ª Região", 5, "SP" },
                    { new Guid("1d6b528f-2007-8d60-b3cd-ecc55382e423"), "TJMMG", "13", "Tribunal de Justiça Militar de Minas Gerais", 9, "MG" },
                    { new Guid("1ec1f440-7ee2-88d6-bb29-f3ff6232b094"), "TJPR", "16", "Tribunal de Justiça (PR)", 8, "PR" },
                    { new Guid("20457b5f-9c2a-8d5b-8e2e-416873d3e38e"), "TRE-MS", "12", "Tribunal Regional Eleitoral (MS)", 6, "MS" },
                    { new Guid("2143d3a8-f1ae-8c42-ae06-fd008798860b"), "TJRR", "23", "Tribunal de Justiça (RR)", 8, "RR" },
                    { new Guid("2575b9b1-0979-8e6e-b393-abfeb002e1d4"), "TRE-DF", "07", "Tribunal Regional Eleitoral (DF)", 6, "DF" },
                    { new Guid("276c9ad9-88a6-830e-9986-e49bfe08d2c6"), "TRT13", "13", "Tribunal Regional do Trabalho da 13ª Região", 5, "PB" },
                    { new Guid("28726e35-7628-8c1d-943b-beb363f80dd0"), "TJMRS", "21", "Tribunal de Justiça Militar do Rio Grande do Sul", 9, "RS" },
                    { new Guid("28fb2654-8346-8dfa-9b54-befb8a3aace3"), "4ª CJM", "04", "4ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("3061d1c4-49c7-8b73-943a-b2f575db877f"), "TJAM", "04", "Tribunal de Justiça (AM)", 8, "AM" },
                    { new Guid("32948f1a-5b89-80e6-be11-d1e6c55a71bb"), "TRE-AP", "03", "Tribunal Regional Eleitoral (AP)", 6, "AP" },
                    { new Guid("35dd61a6-f766-8c4f-bb55-78a57635b5ed"), "TRT6", "06", "Tribunal Regional do Trabalho da 6ª Região", 5, "PE" },
                    { new Guid("38ae2963-026d-8b7c-8ff5-332cdbd044ed"), "TRT24", "24", "Tribunal Regional do Trabalho da 24ª Região", 5, "MS" },
                    { new Guid("3a37f768-d879-8c05-b25c-f0e704958bc5"), "TRT21", "21", "Tribunal Regional do Trabalho da 21ª Região", 5, "RN" },
                    { new Guid("4370fb61-5810-807a-9fb9-35c166295192"), "TJMSP", "26", "Tribunal de Justiça Militar de São Paulo", 9, "SP" },
                    { new Guid("48a64d92-c882-887b-ad4f-cdad87f4a428"), "TRT11", "11", "Tribunal Regional do Trabalho da 11ª Região", 5, "AM" },
                    { new Guid("493e38c4-3ffc-8635-86f0-3117bda3b623"), "TRT7", "07", "Tribunal Regional do Trabalho da 7ª Região", 5, "CE" },
                    { new Guid("49a75ec4-6f46-88c5-a5a7-18ef6ec6d230"), "1ª CJM", "01", "1ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("4a92f7bc-8a1b-81db-9dfa-af80ba820354"), "TRT18", "18", "Tribunal Regional do Trabalho da 18ª Região", 5, "GO" },
                    { new Guid("4af92733-f106-8e70-bf6d-52ce78567645"), "TRT1", "01", "Tribunal Regional do Trabalho da 1ª Região", 5, "RJ" },
                    { new Guid("4b8a3673-651d-85ec-991a-33ab2b19c879"), "TJRN", "20", "Tribunal de Justiça (RN)", 8, "RN" },
                    { new Guid("4ca1dcd1-9716-8dd2-8ff8-b0de504a4693"), "TRE-TO", "27", "Tribunal Regional Eleitoral (TO)", 6, "TO" },
                    { new Guid("54a3517e-3688-87ce-948f-b106d38da2cb"), "TRE-MA", "10", "Tribunal Regional Eleitoral (MA)", 6, "MA" },
                    { new Guid("59ad8fff-f61e-8d3a-9c3f-ab521f658d66"), "TRF3", "03", "Tribunal Regional Federal da 3ª Região", 4, "SP" },
                    { new Guid("5d063f40-b8ef-813e-9e6c-62ebc15f39a6"), "TJTO", "27", "Tribunal de Justiça (TO)", 8, "TO" },
                    { new Guid("6162b688-f665-8c8d-a989-4bf24c4e1fca"), "CSJT", "90", "Conselho Superior da Justiça do Trabalho", 5, null },
                    { new Guid("63e832d7-87dd-848e-bd26-66b769081f2f"), "TRT14", "14", "Tribunal Regional do Trabalho da 14ª Região", 5, "RO" },
                    { new Guid("6aa94bef-f783-80c6-9c47-0a49790160dc"), "TRT17", "17", "Tribunal Regional do Trabalho da 17ª Região", 5, "ES" },
                    { new Guid("6b3a1e33-ca6c-8505-99c1-d389d7b17ea7"), "TRT20", "20", "Tribunal Regional do Trabalho da 20ª Região", 5, "SE" },
                    { new Guid("6bbcd24a-3125-8f23-991c-cbaf0cb14172"), "TRE-PB", "15", "Tribunal Regional Eleitoral (PB)", 6, "PB" },
                    { new Guid("6e325e2e-643b-89bf-92dd-8b98302b050a"), "TJAC", "01", "Tribunal de Justiça (AC)", 8, "AC" },
                    { new Guid("709f2f52-bf7d-84ac-9651-ffc097938896"), "TJRS", "21", "Tribunal de Justiça (RS)", 8, "RS" },
                    { new Guid("74d9ba88-d188-838c-a803-78ab95130490"), "TJES", "08", "Tribunal de Justiça (ES)", 8, "ES" },
                    { new Guid("7571eb1b-a810-86e5-8d20-f7a7e0f34109"), "TRT5", "05", "Tribunal Regional do Trabalho da 5ª Região", 5, "BA" },
                    { new Guid("7ad5f9fb-3123-8835-9e26-f65341228eae"), "TRE-AL", "02", "Tribunal Regional Eleitoral (AL)", 6, "AL" },
                    { new Guid("7d65c84a-dc90-8219-9da5-99367e4edc81"), "TJPI", "18", "Tribunal de Justiça (PI)", 8, "PI" },
                    { new Guid("7e3c8c1b-c7f1-8285-86e2-26ab315d71dd"), "CNJ", "00", "Conselho Nacional de Justiça", 2, null },
                    { new Guid("7feed300-de73-8a53-bd3d-e91c08ab5245"), "TRT10", "10", "Tribunal Regional do Trabalho da 10ª Região", 5, "DF" },
                    { new Guid("824d738b-7dd2-82e0-a36f-ca0cb50298b7"), "TRF6", "06", "Tribunal Regional Federal da 6ª Região", 4, "MG" },
                    { new Guid("8c249e4b-3fde-87f3-9297-8210805aa694"), "TJMT", "11", "Tribunal de Justiça (MT)", 8, "MT" },
                    { new Guid("900d7db7-f70c-8982-abba-256118dfe86e"), "2ª CJM", "02", "2ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("91f3c812-ad75-8357-b886-a9591c596e99"), "TJAL", "02", "Tribunal de Justiça (AL)", 8, "AL" },
                    { new Guid("92fd61d5-bd0f-80b3-81b7-f99636e10338"), "5ª CJM", "05", "5ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("93e23a0f-fb13-8490-932e-5a463828740d"), "TRE-GO", "09", "Tribunal Regional Eleitoral (GO)", 6, "GO" },
                    { new Guid("946800e1-45dd-8339-9196-7bbc26ddd34b"), "TJPB", "15", "Tribunal de Justiça (PB)", 8, "PB" },
                    { new Guid("953c940f-b00c-83a1-95af-8bdabb8b98cb"), "TRT16", "16", "Tribunal Regional do Trabalho da 16ª Região", 5, "MA" },
                    { new Guid("97676e52-b59c-8d98-b5e6-bdafce00a08b"), "TJBA", "05", "Tribunal de Justiça (BA)", 8, "BA" },
                    { new Guid("98a7105c-fe64-83c2-8258-5429d4aefa39"), "TJRJ", "19", "Tribunal de Justiça (RJ)", 8, "RJ" },
                    { new Guid("99bea488-347b-889f-8267-c72083237c2f"), "TST", "00", "Tribunal Superior do Trabalho", 5, null },
                    { new Guid("9b708d8d-7ddd-81d4-974d-626d55278caf"), "TRT2", "02", "Tribunal Regional do Trabalho da 2ª Região", 5, "SP" },
                    { new Guid("9ba57dc8-f6a9-806d-bf10-22b9626767a8"), "11ª CJM", "11", "11ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("9beb421a-f625-82d6-a3f7-0307dd5a33e0"), "TJSP", "26", "Tribunal de Justiça (SP)", 8, "SP" },
                    { new Guid("9d06f6d6-380d-8746-ab8e-073c649aee45"), "TRE-RJ", "19", "Tribunal Regional Eleitoral (RJ)", 6, "RJ" },
                    { new Guid("9dea88b2-bc36-8be0-acd0-516ad6eb7d1c"), "TRE-MT", "11", "Tribunal Regional Eleitoral (MT)", 6, "MT" },
                    { new Guid("9f8458f1-f95e-8798-89e7-4f4c80d86c57"), "TRT19", "19", "Tribunal Regional do Trabalho da 19ª Região", 5, "AL" },
                    { new Guid("a25fdfc9-55cb-8d8f-81b7-3d6c2a894f15"), "TJDFT", "07", "Tribunal de Justiça do Distrito Federal e dos Territórios", 8, "DF" },
                    { new Guid("aba40de6-a608-8025-aca6-2711b9247f13"), "TJSC", "24", "Tribunal de Justiça (SC)", 8, "SC" },
                    { new Guid("abbab2d1-b522-840b-aaa6-ed86725a091c"), "6ª CJM", "06", "6ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("ac1ec1d1-35f2-804d-b3a8-38a125223f80"), "TRE-PI", "18", "Tribunal Regional Eleitoral (PI)", 6, "PI" },
                    { new Guid("ac42c0e1-78ec-8766-8c2f-4614eb126846"), "STM", "00", "Superior Tribunal Militar", 7, null },
                    { new Guid("b8e38888-7a2f-864d-bc96-3525421aabab"), "TRT12", "12", "Tribunal Regional do Trabalho da 12ª Região", 5, "SC" },
                    { new Guid("baba4ce3-1dde-8927-9233-e30cfb8328e2"), "TRE-SP", "26", "Tribunal Regional Eleitoral (SP)", 6, "SP" },
                    { new Guid("bcb8eb94-a8f6-8015-b8dc-91b62f606cda"), "TRE-SE", "25", "Tribunal Regional Eleitoral (SE)", 6, "SE" },
                    { new Guid("be5bb014-c3a8-86f6-b46a-6c6d3c2846b7"), "STJ", "00", "Superior Tribunal de Justiça", 3, null },
                    { new Guid("c4b7275b-2a4a-8d32-a4ee-898ac7b8c4c6"), "3ª CJM", "03", "3ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("c53988e9-71f2-87bf-8fed-7b1aea7e4ecd"), "TRE-CE", "06", "Tribunal Regional Eleitoral (CE)", 6, "CE" },
                    { new Guid("c5473d3c-4967-861d-8317-dd816e8d1de8"), "10ª CJM", "10", "10ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("c5f8b6a6-9f20-80e6-a7c1-5114131f6bc7"), "TRE-PR", "16", "Tribunal Regional Eleitoral (PR)", 6, "PR" },
                    { new Guid("c9a67762-0ed2-83b2-b61e-8487892ac169"), "TSE", "00", "Tribunal Superior Eleitoral", 6, null },
                    { new Guid("cc7efcc5-35d0-8ae4-9d4c-4fbf84bffe8e"), "CJF", "90", "Conselho da Justiça Federal", 4, null },
                    { new Guid("cdcd81a4-135f-8fba-9483-841bedf385a8"), "7ª CJM", "07", "7ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("ce59b5d0-2737-89e7-8e34-5a3621c7fce6"), "TRE-SC", "24", "Tribunal Regional Eleitoral (SC)", 6, "SC" },
                    { new Guid("d53f3e75-fd40-8cbc-a064-0b549ae01875"), "TRE-ES", "08", "Tribunal Regional Eleitoral (ES)", 6, "ES" },
                    { new Guid("d5e0e9cd-ee12-8bb1-902c-038b812c1316"), "TRT22", "22", "Tribunal Regional do Trabalho da 22ª Região", 5, "PI" },
                    { new Guid("d6a1032c-4ae0-80e9-8dfb-bb92df172283"), "TRE-BA", "05", "Tribunal Regional Eleitoral (BA)", 6, "BA" },
                    { new Guid("d7128fb7-446e-8b73-8524-3993b80a5c8f"), "TJCE", "06", "Tribunal de Justiça (CE)", 8, "CE" },
                    { new Guid("d85d532a-44d9-803b-b19f-8e54b83db0e2"), "TRF4", "04", "Tribunal Regional Federal da 4ª Região", 4, "RS" },
                    { new Guid("d90be67e-a824-8a2a-aa0c-c633c3ecbef2"), "TJAP", "03", "Tribunal de Justiça (AP)", 8, "AP" },
                    { new Guid("d94f3f5e-4f0f-83b4-a3a4-7ff6c11759cb"), "TRE-RR", "23", "Tribunal Regional Eleitoral (RR)", 6, "RR" },
                    { new Guid("dde9966c-fd56-8337-9cd7-7ef037124503"), "TRE-AM", "04", "Tribunal Regional Eleitoral (AM)", 6, "AM" },
                    { new Guid("df59d8be-a950-8060-bb21-05cad449b3b1"), "TRE-PA", "14", "Tribunal Regional Eleitoral (PA)", 6, "PA" },
                    { new Guid("e14599b0-05f3-813e-81b0-c5d545037755"), "TRT23", "23", "Tribunal Regional do Trabalho da 23ª Região", 5, "MT" },
                    { new Guid("e14c8022-6fbf-80c7-89ef-dc11bb5fee96"), "TRE-MG", "13", "Tribunal Regional Eleitoral (MG)", 6, "MG" },
                    { new Guid("e36e3592-3209-8432-975f-daf135edda7e"), "TRF1", "01", "Tribunal Regional Federal da 1ª Região", 4, "DF" },
                    { new Guid("e37860dc-b74e-8cb4-86dc-8e3fffed459e"), "TRE-RO", "22", "Tribunal Regional Eleitoral (RO)", 6, "RO" },
                    { new Guid("e6829b28-9bc2-8b6b-89ab-4c941979c7bc"), "TJPA", "14", "Tribunal de Justiça (PA)", 8, "PA" },
                    { new Guid("fb038548-9cb9-8558-a534-4b4bfded758f"), "TRE-RN", "20", "Tribunal Regional Eleitoral (RN)", 6, "RN" },
                    { new Guid("fbcae3a0-c080-81a0-9556-311122d6bd17"), "TJMA", "10", "Tribunal de Justiça (MA)", 8, "MA" },
                    { new Guid("fc29b636-82eb-8979-89ba-196ac827228d"), "TJMG", "13", "Tribunal de Justiça (MG)", 8, "MG" },
                    { new Guid("fca2df1f-aa91-80fd-95a1-43454f2efeb7"), "TRF5", "05", "Tribunal Regional Federal da 5ª Região", 4, "PE" },
                    { new Guid("fd0189cf-32ad-8fde-97ba-8bc7a9e89ca5"), "12ª CJM", "12", "12ª Circunscrição Judiciária Militar", 7, null },
                    { new Guid("feaa15f0-bf5a-8cc4-8a26-1386047f1e86"), "TRF2", "02", "Tribunal Regional Federal da 2ª Região", 4, "RJ" },
                    { new Guid("fec23529-0392-8d5b-878d-6d7b60226240"), "TJMS", "12", "Tribunal de Justiça (MS)", 8, "MS" },
                    { new Guid("ff058c93-214a-8494-b2fc-c041e63fec74"), "TRT4", "04", "Tribunal Regional do Trabalho da 4ª Região", 5, "RS" },
                    { new Guid("ffe49046-da23-82d8-a56d-103229c24038"), "TRT3", "03", "Tribunal Regional do Trabalho da 3ª Região", 5, "MG" },
                    { new Guid("fff81f7a-83cc-8407-a4ba-2733dda6ec19"), "TJSE", "25", "Tribunal de Justiça (SE)", 8, "SE" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "holidays",
                columns: new[] { "id", "day", "easter_offset_days", "legal_basis", "month", "name", "rule", "scope", "valid_from_year", "valid_to_year" },
                values: new object[,]
                {
                    { new Guid("150e8013-68ba-8901-8aeb-25847bebe326"), 20, null, "Lei 14.759/2023", 11, "Dia Nacional de Zumbi e da Consciência Negra", "FixedDate", "National", 2024, null },
                    { new Guid("325b2ff0-a806-8040-ac82-9debfd544345"), 12, null, "Lei 6.802/1980", 10, "Nossa Senhora Aparecida", "FixedDate", "National", 1980, null },
                    { new Guid("3ce60634-65f5-81c5-967a-079f63d9e6dc"), 25, null, "Lei 662/1949", 12, "Natal", "FixedDate", "National", 1950, null },
                    { new Guid("56c45ce7-cdb7-8262-bb60-1175d20a9ae7"), 15, null, "Lei 662/1949", 11, "Proclamação da República", "FixedDate", "National", 1950, null },
                    { new Guid("6889e49a-93e0-854b-a012-6e4e1c1d56c6"), 21, null, "Lei 662/1949 (redação da Lei 10.607/2002)", 4, "Tiradentes", "FixedDate", "National", 1950, null },
                    { new Guid("7b4f5cbf-80e7-8adf-9d52-69db35b3decc"), null, -2, "Lei 9.093/1995", null, "Sexta-feira Santa", "EasterRelative", "National", 1950, null },
                    { new Guid("9b4f671b-da73-83fd-9541-16295f4fd8a1"), 7, null, "Lei 662/1949", 9, "Independência do Brasil", "FixedDate", "National", 1950, null },
                    { new Guid("be8709e9-fc92-8d17-ba36-b12e3806840b"), 1, null, "Lei 662/1949", 1, "Confraternização Universal", "FixedDate", "National", 1950, null },
                    { new Guid("c78aeb20-19dd-8238-9dad-fdee8eea890b"), 2, null, "Lei 662/1949 (redação da Lei 10.607/2002)", 11, "Finados", "FixedDate", "National", 1950, null },
                    { new Guid("ee30731b-33e3-88ad-ae27-7195aa241602"), 1, null, "Lei 662/1949", 5, "Dia do Trabalho", "FixedDate", "National", 1950, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_courts_segment_code",
                schema: "platform",
                table: "courts",
                columns: new[] { "segment", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_expires_at",
                schema: "platform",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_organization_id_key",
                schema: "platform",
                table: "idempotency_keys",
                columns: new[] { "organization_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_organization_id_created_at",
                schema: "platform",
                table: "jobs",
                columns: new[] { "organization_id", "created_at" });

            // Trilha de auditoria append-only com encadeamento SHA-256 por organização (seção 6.4, ADR 0009).
            // O hash é calculado no banco, sob advisory lock por organização, para a cadeia não depender da
            // ordem de commit da aplicação. O REVOKE de UPDATE/DELETE por papel do banco entra na Etapa 3 (RLS).
            migrationBuilder.Sql("""
                ALTER TABLE audit.audit_logs
                    ADD COLUMN chain_position bigint NOT NULL,
                    ADD COLUMN previous_hash bytea NULL,
                    ADD COLUMN hash bytea NOT NULL;

                CREATE UNIQUE INDEX ux_audit_logs_chain
                    ON audit.audit_logs (COALESCE(organization_id, '00000000-0000-0000-0000-000000000000'::uuid), chain_position);
                CREATE INDEX ix_audit_logs_org_occurred_at ON audit.audit_logs (organization_id, occurred_at);
                CREATE INDEX ix_audit_logs_org_entity ON audit.audit_logs (organization_id, entity_type, entity_id);

                CREATE FUNCTION audit.audit_log_payload(r audit.audit_logs) RETURNS text
                LANGUAGE sql IMMUTABLE AS $$
                    SELECT concat_ws('|',
                        r.id::text,
                        COALESCE(r.organization_id::text, ''),
                        COALESCE(r.user_id::text, ''),
                        r.action, r.entity_type, r.entity_id, r.changes::text,
                        COALESCE(r.ip, ''), COALESCE(r.user_agent, ''), COALESCE(r.correlation_id, ''),
                        to_char(r.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'))
                $$;

                CREATE FUNCTION audit.audit_logs_chain() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    chain_key uuid := COALESCE(NEW.organization_id, '00000000-0000-0000-0000-000000000000'::uuid);
                    last_position bigint;
                    last_hash bytea;
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended('audit_logs:' || chain_key::text, 0));

                    SELECT a.chain_position, a.hash INTO last_position, last_hash
                    FROM audit.audit_logs a
                    WHERE COALESCE(a.organization_id, '00000000-0000-0000-0000-000000000000'::uuid) = chain_key
                    ORDER BY a.chain_position DESC
                    LIMIT 1;

                    NEW.chain_position := COALESCE(last_position, 0) + 1;
                    NEW.previous_hash := last_hash;
                    NEW.hash := public.digest(COALESCE(encode(last_hash, 'hex'), '') || '|' || audit.audit_log_payload(NEW), 'sha256');
                    RETURN NEW;
                END
                $$;

                CREATE TRIGGER audit_logs_chain BEFORE INSERT ON audit.audit_logs
                    FOR EACH ROW EXECUTE FUNCTION audit.audit_logs_chain();

                CREATE FUNCTION audit.audit_logs_reject_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit.audit_logs é append-only: % não é permitido', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END
                $$;

                CREATE TRIGGER audit_logs_append_only BEFORE UPDATE OR DELETE ON audit.audit_logs
                    FOR EACH ROW EXECUTE FUNCTION audit.audit_logs_reject_change();
                CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON audit.audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.audit_logs_reject_change();

                -- Retorna a primeira posição cuja ligação ou hash não confere (NULL = cadeia íntegra).
                CREATE FUNCTION audit.verify_chain(org uuid) RETURNS bigint
                LANGUAGE plpgsql STABLE AS $$
                DECLARE
                    r audit.audit_logs;
                    expected_previous bytea := NULL;
                    expected_position bigint := 0;
                BEGIN
                    FOR r IN
                        SELECT * FROM audit.audit_logs a
                        WHERE COALESCE(a.organization_id, '00000000-0000-0000-0000-000000000000'::uuid)
                            = COALESCE(org, '00000000-0000-0000-0000-000000000000'::uuid)
                        ORDER BY a.chain_position
                    LOOP
                        expected_position := expected_position + 1;
                        IF r.chain_position <> expected_position
                           OR r.previous_hash IS DISTINCT FROM expected_previous
                           OR r.hash <> public.digest(COALESCE(encode(expected_previous, 'hex'), '') || '|' || audit.audit_log_payload(r), 'sha256') THEN
                            RETURN expected_position;
                        END IF;
                        expected_previous := r.hash;
                    END LOOP;
                    RETURN NULL;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_logs_no_truncate ON audit.audit_logs;
                DROP TRIGGER IF EXISTS audit_logs_append_only ON audit.audit_logs;
                DROP TRIGGER IF EXISTS audit_logs_chain ON audit.audit_logs;
                DROP FUNCTION IF EXISTS audit.verify_chain(uuid);
                DROP FUNCTION IF EXISTS audit.audit_logs_reject_change();
                DROP FUNCTION IF EXISTS audit.audit_logs_chain();
                DROP FUNCTION IF EXISTS audit.audit_log_payload(audit.audit_logs);
                """);
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "courts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "holidays",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "platform");
        }
    }
}
