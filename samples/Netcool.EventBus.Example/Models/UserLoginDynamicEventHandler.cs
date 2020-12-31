using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Netcool.EventBus.Example.Models
{
    public class UserLoginDynamicEventHandler : IDynamicEventHandler
    {
        public Task Handle(dynamic eventData)
        {
            Console.WriteLine($"Welcome {eventData.UserName}!");
            return Task.FromResult(0);
        }
    }
}