using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Yuyuyui.PrivateServer
{
    public class ClubWorkingRewardEntity : BaseEntity<ClubWorkingRewardEntity>
    {
        public ClubWorkingRewardEntity(
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

            ClubWorkingSlot slot = player.clubWorkingSlots
                .Select(ClubWorkingSlot.Load)
                .First(s => s.club_working_id == clubWorkingId);

            ClubWorkingSlot completedSlot = new()
            {
                id = slot.id,
                available = slot.available,
                club_working_id = slot.club_working_id,
                primary_user_card_id = slot.primary_user_card_id,
                secondary_user_card_id = slot.secondary_user_card_id,
                club_order_master_id = slot.club_order_master_id,
                finishment_time = slot.finishment_time
            };

            slot.available = true;
            slot.club_working_id = null;
            slot.primary_user_card_id = null;
            slot.secondary_user_card_id = null;
            slot.club_order_master_id = null;
            slot.finishment_time = null;
            slot.Save();

            Response responseObj = new()
            {
                club_working = completedSlot,
                club_working_slot = slot
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        public class Response
        {
            public ClubWorkingSlot club_working { get; set; } = new();
            public ClubWorkingSlot club_working_slot { get; set; } = new();
            public IList<object> rewards { get; set; } = new List<object>();
        }
    }
}
