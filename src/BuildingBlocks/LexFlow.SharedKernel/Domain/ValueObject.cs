namespace LexFlow.SharedKernel.Domain;

/// <summary>
/// Base dos value objects. Por ser um <c>record</c>, a igualdade é estrutural (por valor);
/// cada value object valida a si mesmo na criação e é imutável.
/// </summary>
public abstract record ValueObject;
