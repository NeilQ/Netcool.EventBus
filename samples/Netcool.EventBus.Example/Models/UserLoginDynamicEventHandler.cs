using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Netcool.EventBus.Example.Models
{
    public class UserLoginDynamicEventHandler : IDynamicEventHandler
    {
        private readonly ILogger _logger;

        public UserLoginDynamicEventHandler(ILogger<UserLoginDynamicEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(dynamic eventData)
        {
            _logger.LogInformation($"Welcome {eventData.UserName}!");
            return Task.FromResult(0);
        }
    }
}