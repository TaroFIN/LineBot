using System;
using System.Threading.Tasks;
using Line.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LineBot
{
    [Route("api/linebot")]
    public class LineBotController : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpContext _httpContext;
        private readonly LineBotConfig _lineBotConfig;
        private readonly ILogger<LineBotController> _logger;

        public LineBotController(IServiceProvider serviceProvider,
            LineBotConfig lineBotConfig,
            ILogger<LineBotController> logger)
        {
            _httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            _httpContext = _httpContextAccessor.HttpContext;
            _lineBotConfig = lineBotConfig;
            _logger = logger;
        }

        //完整的路由網址就是 https://xxx/api/linebot/run
        [HttpPost("run")]
        public async Task<IActionResult> Post()
        {
            try
            {
                var events = await _httpContext.Request.GetWebhookEventsAsync(_lineBotConfig.channelSecret);
                var lineMessagingClient = new LineMessagingClient(_lineBotConfig.accessToken);
                var lineBotApp = new LineBotApp(lineMessagingClient, _lineBotConfig, _logger);
                await lineBotApp.RunAsync(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(JsonConvert.SerializeObject(ex));
            }
            return Ok();
        }

        [HttpGet("push")]
        public async Task<IActionResult> Get()
        {
            try
            {
                var lineMessagingClient = new LineMessagingClient(_lineBotConfig.accessToken);
                var lineBotApp = new LineBotApp(lineMessagingClient, _lineBotConfig, _logger);
                var result = await lineBotApp.LineBotQuote("!空氣盒子");
                //await lineMessagingClient.PushMessageAsync("C4ce80517ba95bb0bdbd6f4632284e9b5", result);  //測試群組
                await lineMessagingClient.PushMessageAsync("C7978a308207ceba021ae405806844e03", result);  //正式群組
            }
            catch (Exception ex)
            {
                _logger.LogError(JsonConvert.SerializeObject(ex));
            }
            return Ok();
        }
    }
}
