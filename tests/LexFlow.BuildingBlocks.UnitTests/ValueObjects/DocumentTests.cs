using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.BuildingBlocks.UnitTests.ValueObjects;

/// <summary>CPF e CNPJ. Os válidos foram gerados com o algoritmo oficial fora deste código (sem dados de pessoas reais conhecidas).</summary>
public sealed class DocumentTests
{
    [Theory]
    [InlineData("52601815906", "526.018.159-06")]
    [InlineData("083.016.613-05", "083.016.613-05")]
    [InlineData(" 186.091.390-34 ", "186.091.390-34")]
    [InlineData("99603082430", "996.030.824-30")]
    public void Valid_cpf(string input, string formatted)
    {
        var cpf = Cpf.Create(input);

        cpf.IsSuccess.ShouldBeTrue();
        cpf.Value.Formatted.ShouldBe(formatted);
        cpf.Value.Value.Length.ShouldBe(11);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("52601815907")] // DV errado
    [InlineData("52601815960")] // DVs trocados
    [InlineData("11111111111")] // repetido
    [InlineData("00000000000")]
    [InlineData("5260181590")] // 10 dígitos
    [InlineData("526018159066")] // 12 dígitos
    [InlineData("5260181590A")]
    public void Invalid_cpf(string? input)
    {
        Cpf.Create(input).Error.ShouldBe(Cpf.Invalid);
        Cpf.IsValid(input).ShouldBeFalse();
    }

    [Theory]
    [InlineData("62819482000163", "62.819.482/0001-63")]
    [InlineData("19.935.181/0001-76", "19.935.181/0001-76")]
    [InlineData("90937865000171", "90.937.865/0001-71")]
    [InlineData("12.ABC.345/01DE-35", "12.ABC.345/01DE-35")] // exemplo oficial da Receita (CNPJ alfanumérico)
    [InlineData("12abc34501de35", "12.ABC.345/01DE-35")] // minúsculas normalizadas
    [InlineData("TTNJFBF5JXVL03", "TT.NJF.BF5/JXVL-03")]
    [InlineData("SI47WQAL9VQ245", "SI.47W.QAL/9VQ2-45")]
    public void Valid_cnpj(string input, string formatted)
    {
        var cnpj = Cnpj.Create(input);

        cnpj.IsSuccess.ShouldBeTrue();
        cnpj.Value.Formatted.ShouldBe(formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("62819482000164")] // DV errado
    [InlineData("12ABC34501DE36")] // DV errado no alfanumérico
    [InlineData("12ABC34501DEAB")] // DV com letra
    [InlineData("11111111111111")]
    [InlineData("AAAAAAAAAAAAAA")]
    [InlineData("6281948200016")] // 13 caracteres
    [InlineData("12ABC34501D#35")] // caractere inválido
    public void Invalid_cnpj(string? input)
    {
        Cnpj.Create(input).Error.ShouldBe(Cnpj.Invalid);
        Cnpj.IsValid(input).ShouldBeFalse();
    }
}
