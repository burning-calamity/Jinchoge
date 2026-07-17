using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yuyuyui.PrivateServer.DataModel;

namespace Yuyuyui.PrivateServer
{
    public class GachaTransactionUpdateEntity : BaseEntity<GachaTransactionUpdateEntity>
    {
        public GachaTransactionUpdateEntity(
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

            long gachaId = long.Parse(GetPathParameter("gacha_id"));
            long lineupId = long.Parse(GetPathParameter("lineup_id"));
            long transactionId = long.Parse(GetPathParameter("transaction_id"));

            using var gachasDb = new GachasContext();
            using var cardsDb = new CardsContext();
            using var itemsDb = new ItemsContext();

            GachaLineup? lineup = gachasDb.GachaLineups
                .FirstOrDefault(l => l.Id == lineupId && l.GachaId == gachaId)
                ?? gachasDb.GachaLineups.FirstOrDefault(l => l.GachaId == gachaId)
                ?? gachasDb.GachaLineups.FirstOrDefault(l => l.Id == lineupId)
                ?? gachasDb.GachaLineups.FirstOrDefault();

            IList<GachaContent> rolledContents = RollCards(gachasDb, cardsDb, lineup);
            IList<ResultContent> resultContents = new List<ResultContent>();

            foreach (GachaContent rolledContent in rolledContents)
            {
                bool isNew = !player.cards.ContainsKey(rolledContent.ContentId);
                player.GrantCard(rolledContent.ContentId, 1, cardsDb, itemsDb);

                long userCardId = player.cards[rolledContent.ContentId];
                resultContents.Add(new ResultContent
                {
                    item_category_id = 1,
                    master_id = rolledContent.ContentId,
                    content_id = rolledContent.ContentId,
                    card = CardsEntity.Card.FromPlayerCardData(cardsDb, userCardId),
                    is_new = isNew
                });
            }

            Response responseObj = new()
            {
                transaction = new()
                {
                    id = transactionId,
                    results = resultContents
                }
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        private static IList<GachaContent> RollCards(GachasContext gachasDb, CardsContext cardsDb, GachaLineup? lineup)
        {
            List<GachaContent> cardPool = lineup == null
                ? new List<GachaContent>()
                : gachasDb.GachaContents
                    .Where(c => c.GachaBoxId == lineup.GachaBoxId)
                    .Where(c => c.ContentType == "Card" || c.Category == 1)
                    .Where(c => c.Weight > 0)
                    .ToList();

            if (cardPool.Count == 0)
            {
                long fallbackGachaBoxId = lineup == null ? 0 : lineup.GachaBoxId;
                cardPool = cardsDb.Cards
                    .Select(c => new GachaContent
                    {
                        GachaBoxId = fallbackGachaBoxId,
                        Category = 1,
                        ContentId = c.Id,
                        ContentType = "Card",
                        Weight = 1
                    })
                    .ToList();
            }

            if (cardPool.Count == 0)
                return new List<GachaContent>();

            int lotCount = Math.Max(lineup?.LotCount ?? 1, 1);
            List<GachaContent> results = new();
            for (int i = 0; i < lotCount; i++)
                results.Add(RollOne(cardPool));

            return results;
        }

        private static GachaContent RollOne(IList<GachaContent> pool)
        {
            int totalWeight = pool.Sum(c => c.Weight);
            int roll = Random.Shared.Next(totalWeight);

            foreach (GachaContent content in pool)
            {
                if (roll < content.Weight)
                    return content;

                roll -= content.Weight;
            }

            return pool[^1];
        }

        public class Response
        {
            public Transaction transaction { get; set; } = new();

            public class Transaction
            {
                public long id { get; set; }
                public IList<ResultContent> results { get; set; } = new List<ResultContent>();
            }
        }

        public class ResultContent
        {
            public int item_category_id { get; set; }
            public long master_id { get; set; }
            public long content_id { get; set; }
            public CardsEntity.Card card { get; set; } = new();
            public bool is_new { get; set; }
        }
    }
}
