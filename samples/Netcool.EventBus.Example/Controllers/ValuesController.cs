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
            _eventBus.Publish(new UserLoginDynamicEvent(){UserName = "Dad"});

            return Ok();
        }


    }
}
