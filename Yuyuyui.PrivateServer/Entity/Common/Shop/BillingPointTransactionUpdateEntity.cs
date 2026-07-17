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
            long transactionId = long.Parse(GetPathParameter("transaction_id"));
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
