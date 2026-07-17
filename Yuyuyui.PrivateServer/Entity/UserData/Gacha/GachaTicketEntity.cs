using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yuyuyui.PrivateServer.DataModel;

namespace Yuyuyui.PrivateServer
{
    public class GachaTicketEntity : BaseEntity<GachaTicketEntity>
    {
        public GachaTicketEntity(
            Uri requestUri,
            string httpMethod,
            Dictionary<string, string> requestHeaders,
            byte[] requestBody,
            RouteConfig config)
            : base(requestUri, httpMethod, requestHeaders, requestBody, config)
        {
        }

        protected override Task ProcessRequest()
        {
            Response responseObj = new();

            if (Config.Get().InGame.InfiniteItems)
            {
                using var gachasDb = new GachasContext();
                responseObj.gacha_tickets = gachasDb.GachaTickets
                    .ToList()
                    .Select(t => new Ticket
                    {
                        id = t.Id,
                        master_id = t.Id,
                        gacha_id = t.GachaId,
                        gacha_kind = t.GachaKind,
                        consumption_resource_id = t.ConsumptionResourceId,
                        image_id = t.ImageId,
                        name = t.Name,
                        quantity = 999
                    })
                    .ToList();
            }

            responseObj.tickets = responseObj.gacha_tickets;

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        public class Response
        {
            public IList<Ticket> gacha_tickets { get; set; } = new List<Ticket>();
            public IList<Ticket> tickets { get; set; } = new List<Ticket>();
        }

        public class Ticket
        {
            public long id { get; set; }
            public long master_id { get; set; }
            public long gacha_id { get; set; }
            public int gacha_kind { get; set; }
            public int consumption_resource_id { get; set; }
            public long image_id { get; set; }
            public string name { get; set; } = "";
            public int quantity { get; set; }
        }
    }
}
