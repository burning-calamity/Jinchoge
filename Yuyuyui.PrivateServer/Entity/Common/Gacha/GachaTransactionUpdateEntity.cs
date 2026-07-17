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

            GachaLineup? lineup = IsSyntheticFallbackLineup(gachaId, lineupId)
                ? CreateFallbackLineup(gachaId, lineupId)
                : gachasDb.GachaLineups.FirstOrDefault(l => l.Id == lineupId && l.GachaId == gachaId && l.Sp == 1)
                  ?? gachasDb.GachaLineups.FirstOrDefault(l => l.GachaId == gachaId && l.Sp == 1)
                  ?? CreateFallbackLineup(gachaId, lineupId);

            IList<GachaContent> rolledContents = RollCards(gachasDb, cardsDb, lineup, player);
            if (rolledContents.Count > 0)
                DeductConsumption(player, gachasDb, lineup);

            IList<ResultContent> resultContents = new List<ResultContent>();

            foreach (GachaContent rolledContent in rolledContents)
            {
                long cardProfileKey = player.GetCardProfileKey(rolledContent.ContentId, cardsDb);
                bool isNew = !player.cards.ContainsKey(cardProfileKey);
                player.GrantCard(rolledContent.ContentId, 1, cardsDb, itemsDb);

                long userCardId = player.cards[cardProfileKey];
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
                    free_rare_gacha = 1,
                    play_animation = true,
                    results = resultContents
                }
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        private static IList<GachaContent> RollCards(
            GachasContext gachasDb,
            CardsContext cardsDb,
            GachaLineup? lineup,
            PlayerProfile player)
        {
            List<GachaContent> cardPool = lineup == null
                ? new List<GachaContent>()
                : gachasDb.GachaContents
                    .Where(c => c.GachaBoxId == lineup.GachaBoxId)
                    .Where(c => c.ContentType == "Card" || c.Category == 1)
                    .Where(c => c.Weight > 0)
                    .ToList();

            cardPool = ApplySelectGachaFilter(gachasDb, lineup, player, cardPool, out bool selectionLimitedPool);

            if (selectionLimitedPool && cardPool.Count == 0)
                return new List<GachaContent>();

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


        private static List<GachaContent> ApplySelectGachaFilter(
            GachasContext gachasDb,
            GachaLineup? lineup,
            PlayerProfile player,
            List<GachaContent> cardPool,
            out bool selectionLimitedPool)
        {
            selectionLimitedPool = false;

            if (lineup == null || cardPool.Count == 0)
                return cardPool;

            IList<int>? selectedIds = GetPlayerSelection(player, lineup);
            if (selectedIds == null || selectedIds.Count == 0)
                return cardPool;

            selectionLimitedPool = true;

            HashSet<int> selectedSelectIds = selectedIds.ToHashSet();
            HashSet<long> selectedContentIds = gachasDb.GachaContents
                .Where(c => c.GachaBoxId == lineup.GachaBoxId)
                .Where(c => c.SelectId != null && selectedSelectIds.Contains(c.SelectId.Value))
                .Select(c => c.ContentId)
                .ToHashSet();

            if (selectedContentIds.Count == 0)
            {
                HashSet<long> storedContentIds = selectedIds.Select(id => (long)id).ToHashSet();
                selectedContentIds = cardPool
                    .Where(c => storedContentIds.Contains(c.ContentId))
                    .Select(c => c.ContentId)
                    .ToHashSet();
            }

            if (selectedContentIds.Count == 0)
            {
                Utils.LogWarning($"Select gacha selection for gacha {lineup.GachaId}/box {lineup.GachaBoxId} did not match any contents; returning no results instead of rolling unselected cards.");
                return new List<GachaContent>();
            }

            return cardPool
                .Where(c => selectedContentIds.Contains(c.ContentId))
                .ToList();
        }

        private static IList<int>? GetPlayerSelection(PlayerProfile player, GachaLineup lineup)
        {
            long[] selectionKeys =
            {
                lineup.GachaBoxId,
                lineup.GachaId,
                lineup.Id
            };

            foreach (long key in selectionKeys)
            {
                if (player.gachaSelections.TryGetValue(key, out IList<int>? selected) && selected.Count > 0)
                    return selected;
            }

            return null;
        }

        private static void DeductConsumption(PlayerProfile player, GachasContext gachasDb, GachaLineup? lineup)
        {
            if (lineup == null || lineup.ConsumptionAmount <= 0 || Config.Get().InGame.InfiniteItems)
                return;

            int amount = lineup.ConsumptionAmount;
            switch (lineup.ConsumptionResourceId)
            {
                case 1:
                    DeductBillingPoint(player, amount);
                    break;
                case 2:
                    player.data.freeBlessing = Math.Max(0, player.data.freeBlessing - amount);
                    break;
                case 3:
                    player.data.friendPoint = Math.Max(0, player.data.friendPoint - amount);
                    break;
                case 6:
                case 7:
                    DeductGachaTicket(player, gachasDb, lineup, amount);
                    break;
                case 8:
                    player.data.braveCoin = Math.Max(0, player.data.braveCoin - amount);
                    break;
                case 21:
                    player.data.exchangePoint = Math.Max(0, player.data.exchangePoint - amount);
                    break;
                default:
                    Utils.LogWarning($"Gacha lineup {lineup.Id} uses unsupported consumption resource {lineup.ConsumptionResourceId}; no balance was deducted.");
                    break;
            }

            player.Save();
        }

        private static void DeductBillingPoint(PlayerProfile player, int amount)
        {
            int paidDeduction = Math.Min(player.data.paidBlessing, amount);
            player.data.paidBlessing -= paidDeduction;

            int remaining = amount - paidDeduction;
            if (remaining > 0)
                player.data.freeBlessing = Math.Max(0, player.data.freeBlessing - remaining);
        }

        private static void DeductGachaTicket(
            PlayerProfile player,
            GachasContext gachasDb,
            GachaLineup lineup,
            int amount)
        {
            GachaTicket? ticket = gachasDb.GachaTickets
                .FirstOrDefault(t => t.GachaId == lineup.GachaId && t.ConsumptionResourceId == lineup.ConsumptionResourceId)
                ?? gachasDb.GachaTickets.FirstOrDefault(t => t.ConsumptionResourceId == lineup.ConsumptionResourceId);

            if (ticket == null || !player.items.gachaTickets.TryGetValue(ticket.Id, out long ticketItemId) || !Item.Exists(ticketItemId))
            {
                Utils.LogWarning($"No persisted gacha ticket was found for lineup {lineup.Id}; ticket balance was not deducted.");
                return;
            }

            Item ticketItem = Item.Load(ticketItemId);
            ticketItem.quantity = Math.Max(0, ticketItem.quantity - amount);
            ticketItem.Save();
        }

        private static GachaLineup CreateFallbackLineup(long gachaId, long lineupId)
        {
            return new GachaLineup
            {
                Id = lineupId,
                GachaId = gachaId,
                GachaBoxId = gachaId,
                LotCount = IsTenRollLineup(lineupId) ? 10 : 1,
                ConsumptionResourceId = 1,
                ConsumptionAmount = IsTenRollLineup(lineupId) ? 2500 : 250,
                Sp = 1,
                FreeRareGacha = 1
            };
        }

        private static bool IsSyntheticFallbackLineup(long gachaId, long lineupId)
        {
            return lineupId == gachaId * 100 + 1 || lineupId == gachaId * 100 + 10;
        }

        private static bool IsTenRollLineup(long lineupId)
        {
            string idText = lineupId.ToString();
            return idText.EndsWith("10", StringComparison.Ordinal);
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
                public int free_rare_gacha { get; set; }
                public bool play_animation { get; set; }
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
