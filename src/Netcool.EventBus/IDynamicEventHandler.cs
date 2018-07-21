using System.Threading.Tasks;

namespace Netcool.EventBus
{
    public interface IDynamicEventHandler
    {
        Task Handle(dynamic eventData);
    }
}