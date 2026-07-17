using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class ClubWorkingStartEntity : BaseEntity<ClubWorkingStartEntity>
    {
        public ClubWorkingStartEntity(
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
            Request request = Deserialize<Request>(requestBody)!;

            ClubWorkingSlot slot = player.clubWorkingSlots
                .Select(ClubWorkingSlot.Load)
                .First(s => s.id == request.club_working_slot_id);

            slot.available = false;
            slot.club_working_id = GenerateWorkingId(player);
            slot.primary_user_card_id = request.primary_user_card_id;
            slot.secondary_user_card_id = request.secondary_user_card_id;
            slot.club_order_master_id = request.club_order_master_id;
            slot.finishment_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.Save();

            Response responseObj = new()
            {
                club_working = slot
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        private static long GenerateWorkingId(PlayerProfile player)
        {
            long newId = long.Parse(Utils.GenerateRandomDigit(9));
            while (player.clubWorkingSlots
                       .Select(ClubWorkingSlot.Load)
                       .Any(s => s.club_working_id == newId))
                newId = long.Parse(Utils.GenerateRandomDigit(9));

            return newId;
        }

        public class Request
        {
            public long club_working_slot_id { get; set; }
            public int club_order_master_id { get; set; }
            public long? primary_user_card_id { get; set; }
            public long? secondary_user_card_id { get; set; }
        }

        public class Response
        {
            public ClubWorkingSlot club_working { get; set; } = new();
        }
    }
}
