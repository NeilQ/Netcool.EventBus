using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Netcool.EventBus.Example.Models;

namespace Netcool.EventBus.Example.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly IEventBus _eventBus;

        public ValuesController(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        // GET api/values
        [HttpGet]
        public ActionResult Get()
        {
            _eventBus.Publish(new UserLoginEvent() { UserName = "Peppa" });
            _eventBus.Publish(new UserLoginEvent() { UserName = "佩奇" });
            _eventBus.Publish(new UserLoginDynamicEvent() { UserName = "Dad" });
            _eventBus.Publish(new ExceptionEvent());

            return Ok();
        }

        [HttpGet("exception")]
        public async Task<IActionResult> PublishException()
        {
            _eventBus.Subscribe<ExceptionEvent, ExceptionEventHandler>();
            
            // trigger rabbitmq basic.nack
            await Task.Delay(1000);
            _eventBus.Publish(new ExceptionEvent());
            
            await Task.Delay(5000);
            
            // rabbitmq requeue
            _eventBus.Subscribe<ExceptionEvent, ExceptionEventHandler>();
            return Ok();
        }
    }
}
