using System.Threading.Tasks;
using Netcool.EventBus;

namespace Netcore.EventBus.Test
{
    public class NamedTestEventHandler : IEventHandler<NamedTestEvent>
    {
        public bool Handled { get; private set; }
        public NamedTestEventHandler()
        {
            Handled = false;
        }
        public Task Handle(NamedTestEvent @event)
        {
            Handled = true;
            return Task.FromResult(0);
        }
    }
}