using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class BillingPointTransactionUpdateEntity : BaseEntity<BillingPointTransactionUpdateEntity>
    {
        public BillingPointTransactionUpdateEntity(Uri requestUri, string httpMethod, Dictionary<string, string> requestHeaders, byte[] requestBody, RouteConfig config)
            : base(requestUri, httpMethod, requestHeaders, requestBody, config) { }

        protected override Task ProcessRequest()
        {
            var player = GetPlayerFromCookies();
            long transactionId = long.Parse(GetPathParameter("transaction_id"));

            if (RequestUri.AbsolutePath.Contains("weekday_stamina_recovery"))
                player.data.weekdayStamina = Math.Max(player.data.weekdayStamina, 6);
            else if (RequestUri.AbsolutePath.Contains("stamina_recovery"))
                player.data.stamina = Math.Max(player.data.stamina, 140);

            player.Save();

            responseBody = Serialize(new Response { transaction = new Transaction { id = transactionId }, completed = true });
            SetBasicResponseHeaders();
            return Task.CompletedTask;
        }

        public class Response
        {
            public Transaction transaction { get; set; } = new();
            public bool completed { get; set; }
        }

        public class Transaction
        {
            public long id { get; set; }
        }
    }
}
