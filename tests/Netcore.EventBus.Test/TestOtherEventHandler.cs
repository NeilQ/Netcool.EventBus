using System.Threading.Tasks;
using Netcool.EventBus;

namespace Netcore.EventBus.Test
{
    public class TestOtherEventHandler : IEventHandler<TestEvent>
    {
        public bool Handled { get; private set; }
        public TestOtherEventHandler()
        {
            Handled = false;
        }
        public Task Handle(TestEvent @event)
        {
            Handled = true;
            return Task.FromResult(0);
        }
    }
}