using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.BuildingBlocks.UnitTests.ValueObjects;

public sealed class ContactTests
{
    [Theory]
    [InlineData("123456/SP", "123456", "SP")]
    [InlineData("123.456/sp", "123456", "SP")]
    [InlineData("SP 123456", "123456", "SP")]
    [InlineData("OAB/RJ 98765", "98765", "RJ")]
    [InlineData("0012345-MG", "12345", "MG")]
    public void Valid_oab(string input, string number, string state)
    {
        var oab = OabNumber.Parse(input).Value;

        oab.Number.ShouldBe(number);
        oab.State.ShouldBe(state);
        oab.Formatted.ShouldBe($"{number}/{state}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456/XX")] // UF inexistente
    [InlineData("123456")] // sem UF
    [InlineData("12345678/SP")] // 8 dígitos
    [InlineData("0000/SP")] // só zeros
    [InlineData("12A456/SP")]
    public void Invalid_oab(string? input) => OabNumber.Parse(input).Error.ShouldBe(OabNumber.Invalid);

    [Theory]
    [InlineData("Advogada@Exemplo.com.BR", "advogada@exemplo.com.br")]
    [InlineData("  joao.silva+lexflow@escritorio.adv.br ", "joao.silva+lexflow@escritorio.adv.br")]
    public void Valid_email(string input, string normalized) => Email.Create(input).Value.Value.ShouldBe(normalized);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem-arroba.com.br")]
    [InlineData("a@b")]
    [InlineData("a@@b.com")]
    [InlineData("com espaco@b.com")]
    [InlineData("a@-dominio.com")]
    public void Invalid_email(string? input) => Email.IsValid(input).ShouldBeFalse();

    [Theory]
    [InlineData("(11) 98765-4321", "+5511987654321", true)]
    [InlineData("+55 21 3456-7890", "+552134567890", false)]
    [InlineData("5561999998888", "+5561999998888", true)]
    [InlineData("4733334444", "+554733334444", false)]
    public void Valid_phone(string input, string e164, bool mobile)
    {
        var phone = Phone.Create(input).Value;

        phone.E164.ShouldBe(e164);
        phone.IsMobile.ShouldBe(mobile);
    }

    [Fact]
    public void Phone_is_formatted() => Phone.Create("11987654321").Value.Formatted.ShouldBe("(11) 98765-4321");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(01) 98765-4321")] // DDD inválido
    [InlineData("(10) 98765-4321")] // DDD com zero
    [InlineData("(11) 88765-4321")] // celular não começa com 9
    [InlineData("(11) 9876-5432")] // fixo começando com 9
    [InlineData("987654321")] // sem DDD
    [InlineData("(11) 98765-4321 ramal 2")]
    public void Invalid_phone(string? input) => Phone.IsValid(input).ShouldBeFalse();

    [Theory]
    [InlineData("01310-100", "01310-100")]
    [InlineData("01310100", "01310-100")]
    [InlineData("70.040-010", "70040-010")]
    public void Valid_cep(string input, string formatted) => Cep.Create(input).Value.Formatted.ShouldBe(formatted);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000-000")]
    [InlineData("0131010")]
    [InlineData("01310-10A")]
    public void Invalid_cep(string? input) => Cep.IsValid(input).ShouldBeFalse();
}
