using System;
using System.Threading.Tasks;

namespace Netcool.EventBus.Example.Models
{
    public class ExceptionEventHandler : IEventHandler<ExceptionEvent>
    {
        private readonly IEventBus _eventBus;

        public ExceptionEventHandler(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public async Task Handle(ExceptionEvent @event)
        {
            await Task.Delay(2000);
            _eventBus.Unsubscribe<ExceptionEvent, ExceptionEventHandler>();
            throw new Exception("This is an exception event: " + @event.Id);
        }
    }
}
