// TEMPORÁRIO: violação proposital da seção 4.2 para provar que o CI falha. Será revertido.
namespace LexFlow.Demo.Infrastructure
{
    public sealed class DemoRepository;
}

namespace LexFlow.Demo.Domain
{
    public sealed class DemoEntity
    {
        public LexFlow.Demo.Infrastructure.DemoRepository? Repository { get; init; }
    }
}
