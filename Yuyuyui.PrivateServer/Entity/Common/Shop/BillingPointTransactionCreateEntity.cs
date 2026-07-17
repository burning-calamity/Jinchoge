using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class BillingPointTransactionCreateEntity : BaseEntity<BillingPointTransactionCreateEntity>
    {
        public BillingPointTransactionCreateEntity(Uri requestUri, string httpMethod, Dictionary<string, string> requestHeaders, byte[] requestBody, RouteConfig config)
            : base(requestUri, httpMethod, requestHeaders, requestBody, config) { }

        protected override Task ProcessRequest()
        {
            responseBody = Serialize(new Response { transaction = new Transaction { id = long.Parse(Utils.GenerateRandomDigit(8)) } });
            SetBasicResponseHeaders();
            return Task.CompletedTask;
        }

        public class Response
        {
            public Transaction transaction { get; set; } = new();
        }

        public class Transaction
        {
            public long id { get; set; }
        }
    }
}
