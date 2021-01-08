using System.Text.Encodings.Web;
using System.Text.Json;

namespace Netcool.EventBus
{
    public class EventBusOptions
    {
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}