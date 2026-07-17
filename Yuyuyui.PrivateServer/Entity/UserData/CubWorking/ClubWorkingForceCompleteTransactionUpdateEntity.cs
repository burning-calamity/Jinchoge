using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class ClubWorkingForceCompleteTransactionUpdateEntity : BaseEntity<ClubWorkingForceCompleteTransactionUpdateEntity>
    {
        public ClubWorkingForceCompleteTransactionUpdateEntity(
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
            var player = GetPlayerFromCookies();
            long clubWorkingId = long.Parse(GetPathParameter("club_working_id"));
            long transactionId = long.Parse(GetPathParameter("transaction_id"));

            ClubWorkingSlot slot = player.clubWorkingSlots
                .Select(ClubWorkingSlot.Load)
                .First(s => s.club_working_id == clubWorkingId);

            slot.finishment_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.Save();

            Response responseObj = new()
            {
                transaction = new()
                {
                    id = transactionId
                },
                club_working = slot
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        public class Response
        {
            public Transaction transaction { get; set; } = new();
            public ClubWorkingSlot club_working { get; set; } = new();

            public class Transaction
            {
                public long id { get; set; }
            }
        }
    }
}
