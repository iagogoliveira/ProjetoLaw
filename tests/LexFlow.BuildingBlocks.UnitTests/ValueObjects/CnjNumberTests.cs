using System.Globalization;
using System.Numerics;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.BuildingBlocks.UnitTests.ValueObjects;

/// <summary>
/// Critério de aceite da Etapa 1: ao menos 20 números CNJ de formato válido e 20 inválidos.
/// Os válidos cobrem todos os segmentos (J = 1 a 9) e vários tribunais; os dígitos foram calculados
/// fora deste código e são reconferidos aqui por uma implementação independente com <see cref="BigInteger"/>.
/// </summary>
public sealed class CnjNumberTests
{
    public static TheoryData<string> ValidNumbers =>
    [
        "2238058-98.2025.8.26.8434", // TJSP
        "6882179-53.2025.8.19.4857", // TJRJ
        "7449860-97.2013.8.13.1614", // TJMG
        "9839979-93.2024.8.21.3778", // TJRS
        "8894673-45.2016.8.16.0869", // TJPR
        "2789416-15.2009.8.05.0052", // TJBA
        "7920460-80.2019.8.07.9230", // TJDFT
        "6875885-84.2019.8.24.9697", // TJSC
        "6616920-53.2022.4.01.1974", // TRF1
        "7232307-49.2021.4.03.5689", // TRF3
        "1012904-46.2024.4.04.9894", // TRF4
        "3883512-41.2008.4.06.7781", // TRF6
        "3745690-86.2025.5.02.8348", // TRT2
        "5262693-23.2012.5.15.2253", // TRT15
        "4227945-88.2024.5.01.8836", // TRT1
        "2504184-24.2010.6.26.6597", // TRE-SP
        "7489586-33.2023.1.00.1272", // STF
        "4777190-76.2012.3.00.1997", // STJ
        "2669221-52.2017.7.01.5601", // Justiça Militar da União
        "1854470-27.2022.9.13.5663", // TJM-MG
        "0901226-07.2010.2.00.5299", // CNJ
        "1534157-47.2019.5.00.1031", // TST
    ];

    public static TheoryData<string> InvalidNumbers =>
    [
        "",
        "   ",
        "2238058-99.2025.8.26.8434", // DV alterado
        "2238058-89.2025.8.26.8434", // DV com dígitos trocados
        "2238059-98.2025.8.26.8434", // sequencial alterado
        "2238058-98.2024.8.26.8434", // ano alterado
        "2238058-98.2025.8.25.8434", // tribunal alterado
        "2238058-98.2025.8.26.8435", // origem alterada
        "2238058-98.2025.5.26.8434", // segmento alterado
        "0000000-00.2025.0.00.0000", // segmento 0 não existe
        "223805-98.2025.8.26.8434", // sequencial com 6 dígitos
        "2238058-98.25.8.26.8434", // ano com 2 dígitos
        "2238058-98.2025.8.26.843", // origem com 3 dígitos
        "2238058-98.2025.8.268434", // separador faltando
        "2238058/98.2025.8.26.8434", // separador errado
        "2238058-98.2025.8.26.843A", // letra
        "223805898202582684341", // 21 dígitos
        "2238058982025826843", // 19 dígitos
        "22380589820258268435", // 20 dígitos com DV inválido
        "CNJ 2238058-98.2025.8.26.8434", // texto extra
    ];

    [Theory]
    [MemberData(nameof(ValidNumbers))]
    public void Valid_numbers_are_accepted(string value)
    {
        var result = CnjNumber.Create(value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Formatted.ShouldBe(value);
    }

    [Theory]
    [MemberData(nameof(ValidNumbers))]
    public void Valid_numbers_pass_independent_mod97_check(string value)
    {
        var digits = value.Replace("-", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal);
        var reordered = digits[..7] + digits[9..] + digits[7..9]; // NNNNNNN AAAA J TR OOOO DD

        (BigInteger.Parse(reordered, CultureInfo.InvariantCulture) % 97).ShouldBe(BigInteger.One);
    }

    [Theory]
    [MemberData(nameof(InvalidNumbers))]
    public void Invalid_numbers_are_rejected(string value)
    {
        var result = CnjNumber.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CnjNumber.Invalid);
        CnjNumber.IsValid(value).ShouldBeFalse();
    }

    [Fact]
    public void Null_is_rejected() => CnjNumber.Create(null).Error.ShouldBe(CnjNumber.Invalid);

    [Fact]
    public void Exposes_parts_of_the_number()
    {
        var cnj = CnjNumber.Create("2238058-98.2025.8.26.8434").Value;

        cnj.Sequential.ShouldBe("2238058");
        cnj.CheckDigits.ShouldBe("98");
        cnj.Year.ShouldBe(2025);
        cnj.Segment.ShouldBe(8);
        cnj.Court.ShouldBe("26");
        cnj.Origin.ShouldBe("8434");
        cnj.ToString().ShouldBe("2238058-98.2025.8.26.8434");
    }

    [Fact]
    public void Accepts_unformatted_digits_and_formats_them()
    {
        CnjNumber.Create("22380589820258268434").Value.Formatted.ShouldBe("2238058-98.2025.8.26.8434");
    }

    [Fact]
    public void Equal_numbers_are_equal_value_objects()
    {
        CnjNumber.Create("22380589820258268434").Value.ShouldBe(CnjNumber.Create("2238058-98.2025.8.26.8434").Value);
    }
}
