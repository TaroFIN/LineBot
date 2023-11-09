using Line.Messaging;
using Line.Messaging.Webhooks;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace LineBot
{
    internal class LineBotApp : WebhookApplication
    {
        private LineMessagingClient lineMessagingClient;
        private LineBotConfig airBoxConfig;
        private readonly ILogger<LineBotController> _logger;
        private static int disconnect = 0;
        private static List<string> dcAirBox = new List<string>();
        private CosmosClient cosmosClient;
        private Container container;
        private Database database;
        private string databaseId = "ToDoList";
        private string containerId = "Items";
        private DateTime dateTime = DateTime.MaxValue;
        private string userID = "";


        public LineBotApp(LineMessagingClient lineMessagingClient, LineBotConfig airBoxConfig, ILogger<LineBotController> logger)
        {
            this.lineMessagingClient = lineMessagingClient;
            this.airBoxConfig = airBoxConfig;
            this.cosmosClient = new CosmosClient(airBoxConfig.configurationCosmos.GetValue<string>("EndpointUri"),
                                                 airBoxConfig.configurationCosmos.GetValue<string>("PrimaryKey"),
                                                 new CosmosClientOptions() { ApplicationName = "CosmosDBDotnetQuickstart" });
            this._logger = logger;
        }

        protected override async Task OnMessageAsync(MessageEvent ev)
        {
            var result = null as List<ISendMessage>;

            switch (ev.Message)
            {
                //文字訊息
                case TextEventMessage textMessage:
                    {
                        //頻道Id
                        var channelId = ev.Source.Id;
                        //使用者Id
                        var userId = ev.Source.UserId;

                        userID = userId;

                        result = await LineBotQuote(textMessage.Text);
                    }
                    break;
            }

            if (result != null)
            {
                await lineMessagingClient.ReplyMessageAsync(ev.ReplyToken, result);
            }
        }

        public async Task<List<ISendMessage>> LineBotQuote(string quote)
        {
            List<ISendMessage> result = new List<ISendMessage>();
            var airbox = airBoxConfig.configuration.Get<string[]>();
            await this.CreateDatabaseAsync();
            await this.CreateContainerAsync();

            string airBoxInfo = "";
            if (quote[0] == '!')
            {
                string[] quote_handler = quote.Trim().Split(' ');
                int index = 0;
                switch (quote_handler[0])
                {
                    case "!說話":
                        result.Add(new TextMessage(quote));
                        return result;
                    case "!空氣盒子":
                        disconnect = 0;
                        dcAirBox.Clear();
                        var tasks = new List<Task<(string index, string abf)>>();

                        ItemResponse<AirBoxes> airBoxesResponse = await this.container.ReadItemAsync<AirBoxes>("AirBoxes", new PartitionKey("AirBoxes"));
                        var itemBody = airBoxesResponse.Resource;
                        var airBoxSite = itemBody.airBoxSite.GetType().GetProperties().ToDictionary(x => x.Name, x => x.GetValue(itemBody.airBoxSite, null));
                        foreach (var i in airBoxSite.Values)
                        {
                            tasks.Add(GetAirBoxInfoAsync(i.ToString()));
                        }

                        foreach (var task in await Task.WhenAll(tasks))
                        {
                            if (task.abf.Length > 0)
                            {
                                airBoxInfo += task.abf;
                                if (index == 2 || index == 5 || index == airBoxSite.Count - 1)
                                {
                                    result.Add(new TextMessage(airBoxInfo));
                                    airBoxInfo = "";
                                }
                                if (airBoxInfo.Length > 0) airBoxInfo += "\n\n";
                                index++;
                            }
                        }
                        if (result.Count > 0) result.Insert(0, new TextMessage(GetAirBoxList(dateTime)));
                        if (disconnect > 8) result.Insert(result.Count, new ImageMessage("https://imgur.com/mVUmaZ9.jpg", "https://imgur.com/mVUmaZ9.jpg"));

                        return result;
                    case "!command":
                        result.Add(new TextMessage("!空氣盒子:抓取空氣盒子資訊\n!Edit:更改其站點之MAC 格式:!Edit <站點> <MAC>"));
                        return result;
                    case "!Site":
                        result.Add(new TextMessage(GetAirBoxSite()));
                        return result;
                    case "!Edit":
                        string editResult = await EditAirBox(quote_handler);
                        result.Add(new TextMessage(editResult));
                        return result;
                    case "!Wake":
                        result.Add(new ImageMessage("https://i.imgur.com/3Phg83h.png", "https://i.imgur.com/h5kB9lW.png"));
                        return result;
                    default:
                        result.Add(new TextMessage("不能辨識其指令，請使用!command取得所有指令"));
                        return result;
                }
            }
            else
            {
                _logger.LogInformation(userID + ":" + quote);
                return null;
            }
        }

        private async Task<(string index, string abf)> GetAirBoxInfoAsync(string MAC)
        {
            ItemResponse<AirBoxItem> wakefieldFamilyResponse = await this.container.ReadItemAsync<AirBoxItem>(MAC, new PartitionKey(MAC));
            var itemBody = wakefieldFamilyResponse.Resource;

            _logger.LogInformation(itemBody.jsonIsBroken.ToString());

            AirBoxFeed airBoxFeed = itemBody.airbox;
            string result;
            if(itemBody.jsonIsBroken)
            {
                result = $"站點：{itemBody.SiteName} 回傳json格式異常";
                dcAirBox.Add(itemBody.SiteName);
                disconnect++;
            }
            else if (airBoxFeed.feeds.Count == 0)
            {
                result = $"站點：{itemBody.SiteName} 並無資料";
                dcAirBox.Add(itemBody.SiteName);
                disconnect++;
            }
            else
            {
                result = $"站點：{airBoxFeed.feeds[0].AirBox.name} \n" +
                    $"時間：{airBoxFeed.feeds[0].AirBox.timestamp.AddHours(8).ToString("yyyy/MM/dd HH:mm:ss")} \n" +
                    $"PM2.5：{airBoxFeed.feeds[0].AirBox.s_d1}μg/m3 \n" +
                    $"溫度：{airBoxFeed.feeds[0].AirBox.s_t0.ToString("#00")}°C";
                if (dateTime > airBoxFeed.feeds[0].AirBox.timestamp) dateTime = airBoxFeed.feeds[0].AirBox.timestamp;
            }
            return (MAC, result);
        }

        private string GetAirBoxList(DateTime date)
        {
            if (date == DateTime.MaxValue) date = DateTime.UtcNow;
            string result = $"監控測站數量：10 站\n上線測站數量：{10 - disconnect} 站\n離線測站數量：{disconnect} 站\n(測站名稱)\n\n最後更新時間:{date.AddHours(8).ToString("yyyy/MM/dd HH:mm:ss")}";
            string dc = "";
            foreach (string _dc in dcAirBox)
            {
                dc += dc.Length > 0 ? "," + _dc : _dc;
            }
            result = result.Replace("(測站名稱)", dc);
            return result;
        }

        private string GetAirBoxSite()
        {
            string result = "WaterTower_Left:水塔測站(左)\n"
                + "WaterTower_Center:水塔測站(中)\n"
                + "WaterTower_Right:水塔測站(右)\n"
                + "TCDorm_Left:中科宿舍(左)\n"
                + "TCDorm_Center:中科宿舍(中)\n"
                + "TCDorm_Right:中科宿舍(右)\n"
                + "XinKeRoad:新科路\n"
                + "Golf_Left:高爾夫球場(左)\n"
                + "Golf_Right:高爾夫球場(右)\n"
                + "Test:測試\n";

            return result;
        }

        private async Task<string> EditAirBox(string[] quote_handler)
        {
            try
            {
                if (quote_handler.Length < 3) return "指令輸入錯誤 格式:!Edit {站點} {MAC}";

                Type type = typeof(AirBoxSite);
                ItemResponse<AirBoxes> airBoxResponse = await this.container.ReadItemAsync<AirBoxes>("AirBoxes", new PartitionKey("AirBoxes"));

                AirBoxSite airBoxSite = airBoxResponse.Resource.airBoxSite;
                var airBoxSiteDic = airBoxSite.GetType().GetProperties().ToDictionary(x => x.Name, x => x.GetValue(airBoxSite, null));
                airBoxSiteDic[quote_handler[1]] = quote_handler[2];
                airBoxResponse.Resource.airBoxSite = (AirBoxSite)DicToObject(airBoxSiteDic, type);
                await this.container.ReplaceItemAsync<AirBoxes>(airBoxResponse.Resource, "AirBoxes", new PartitionKey("AirBoxes"));

                return $"站點{quote_handler[1]} 修改成功，請確認";
            }
            catch(Exception exp)
            {
                _logger.LogError(JsonConvert.SerializeObject(exp));
                return "例外狀況產生，請至PowerShell查詢相關錯誤";
            }
        }

        public static object DicToObject(Dictionary<string, object> dict, Type type)
        {
            var obj = Activator.CreateInstance(type);

            foreach (var kv in dict)
            {
                var prop = type.GetProperty(kv.Key);
                if (prop == null) continue;

                object value = kv.Value;
                if (value is Dictionary<string, object>)
                {
                    value = DicToObject((Dictionary<string, object>)value, prop.PropertyType); // <= This line
                }

                prop.SetValue(obj, value, null);
            }
            return obj;
        }

        private async Task CreateContainerAsync()
        {
            // Create a new container
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/partitionKey");
        }

        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        }
    }
}