using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class ShopProductTransactionUpdateEntity : BaseEntity<ShopProductTransactionUpdateEntity>
    {
        public ShopProductTransactionUpdateEntity(
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
            long productId = long.Parse(GetPathParameter("product_id"));
            long transactionId = long.Parse(GetPathParameter("transaction_id"));

            Response responseObj = new()
            {
                transaction = new()
                {
                    id = transactionId
                },
                product = new()
                {
                    id = productId,
                    purchased_quantity = 1
                }
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        public class Response
        {
            public Transaction transaction { get; set; } = new();
            public Product product { get; set; } = new();

            public class Transaction
            {
                public long id { get; set; }
            }

            public class Product
            {
                public long id { get; set; }
                public int purchased_quantity { get; set; }
            }
        }
    }
}
