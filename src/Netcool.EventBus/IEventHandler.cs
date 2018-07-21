using System.Threading.Tasks;

namespace Netcool.EventBus
{


    public interface IEventHandler<in TEvent> where TEvent : Event
    {
        Task Handle(TEvent @event);
    }
}