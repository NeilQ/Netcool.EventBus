using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Netcool.EventBus.Example.Models
{
    public class UserLoginEventHandler : IEventHandler<UserLoginEvent>
    {
        private readonly ILogger<UserLoginEventHandler> _logger;

        public UserLoginEventHandler(ILogger<UserLoginEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(UserLoginEvent @event)
        {
            _logger.LogInformation($"Welcome {@event.UserName}!");
            return Task.FromResult(0);
        }
    }
}
