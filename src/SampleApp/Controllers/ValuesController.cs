using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace SampleApp.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ILogger<ValuesController> logger;
        private readonly HttpClient httpClient;

        public ValuesController(ILogger<ValuesController> logger, HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
        }

        [HttpGet("{id}")]
        public async Task<string> Get(string id)
        {
            logger.LogInformation("Got Value {id}", id);

            if (id == "1")
            {
                logger.LogInformation("Doing another nested call");
                await httpClient.GetAsync($"http://127.0.0.1:5000/api/values/100");
            }
            return id;
        }
    }
}
