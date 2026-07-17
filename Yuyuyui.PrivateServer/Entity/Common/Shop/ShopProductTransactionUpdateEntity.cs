using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yuyuyui.PrivateServer.DataModel;

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
            string shopId = GetPathParameter("shop_id");
            long productId = long.Parse(GetPathParameter("product_id"));
            long transactionId = long.Parse(GetPathParameter("transaction_id"));
            PlayerProfile player = GetPlayerFromCookies();

            if (!player.transactions.shopProductTransactions.TryGetValue(transactionId, out var storedTransaction))
                throw new APIErrorException("A1321", $"Unknown shop transaction {transactionId}.");

            if (productId == 0)
            {
                shopId = storedTransaction.shop_id;
                productId = storedTransaction.product_id;
            }
            else if (storedTransaction.shop_id != shopId || storedTransaction.product_id != productId)
            {
                throw new APIErrorException("A1321", $"Shop transaction {transactionId} does not match product {shopId}/{productId}.");
            }

            ShopProduct product = FindProduct(shopId, productId);

            string purchaseKey = GetPurchaseKey(shopId, productId);
            if (!storedTransaction.completed)
            {
                int purchasedQuantity = GetPurchasedQuantity(player, purchaseKey);
                if (purchasedQuantity >= product.PurchaseLimitQuantity)
                    throw new APIErrorException("A1321", $"Purchase limit reached for shop product {shopId}/{productId}.");

                if (!DeductCurrency(player, product))
                    throw new APIErrorException("A1321", $"Not enough currency to purchase shop product {shopId}/{productId}.");

                GrantProduct(player, product);
                player.transactions.shopProductPurchaseCounts[purchaseKey] = purchasedQuantity + 1;
                storedTransaction.completed = true;
                player.Save();
            }

            Response responseObj = new()
            {
                transaction = new()
                {
                    id = transactionId
                },
                product = new()
                {
                    id = productId,
                    item_master_id = product.ItemMasterId,
                    item_category_id = product.ItemCategoryId,
                    purchased_quantity = GetPurchasedQuantity(player, purchaseKey)
                }
            };

            responseBody = Serialize(responseObj);
            SetBasicResponseHeaders();

            return Task.CompletedTask;
        }

        private static ShopProduct FindProduct(string shopId, long productId)
        {
            return (shopId, productId) switch
            {
                ("C3365", 10093) => new ShopProduct(100040, (int)ItemCategory.Card, 4, 50000, 5),
                ("C3365", 10094) => new ShopProduct(100140, (int)ItemCategory.Card, 4, 50000, 5),
                ("C3365", 10095) => new ShopProduct(100140, (int)ItemCategory.Card, 8, 5, 5),
                ("A3365", 10093) => new ShopProduct(500101, (int)ItemCategory.Accessory, 4, 6000, 20),
                ("A3365", 10094) => new ShopProduct(500109, (int)ItemCategory.Accessory, 4, 6000, 20),
                ("A3365", 10095) => new ShopProduct(500091, (int)ItemCategory.Accessory, 4, 8000, 20),
                ("I3365", 10093) => new ShopProduct(2, (int)ItemCategory.EnhancementItem, 4, 1000, 20),
                ("I3365", 10094) => new ShopProduct(10310, (int)ItemCategory.EvolutionItem, 4, 1000, 10),
                ("I3365", 10095) => new ShopProduct(10410, (int)ItemCategory.EvolutionItem, 4, 1000, 10),
                _ => throw new APIErrorException("A1321", $"Unknown shop product {shopId}/{productId}")
            };
        }

        private static string GetPurchaseKey(string shopId, long productId)
        {
            return $"{shopId}/{productId}";
        }

        private static int GetPurchasedQuantity(PlayerProfile player, string purchaseKey)
        {
            return player.transactions.shopProductPurchaseCounts.TryGetValue(purchaseKey, out int purchasedQuantity)
                ? purchasedQuantity
                : 0;
        }

        private static void GrantProduct(PlayerProfile player, ShopProduct product)
        {
            switch ((ItemCategory)product.ItemCategoryId)
            {
                case ItemCategory.Card:
                    using (var cardsDb = new CardsContext())
                    using (var itemsDb = new ItemsContext())
                    {
                        player.GrantCard(product.ItemMasterId, 1, cardsDb, itemsDb);
                    }
                    break;
                case ItemCategory.Accessory:
                    player.GrantAccessory(product.ItemMasterId, 1);
                    break;
                case ItemCategory.EnhancementItem:
                    GrantStackableItem(player.items.enhancement, product.ItemMasterId, 1);
                    break;
                case ItemCategory.EvolutionItem:
                    GrantStackableItem(player.items.evolution, product.ItemMasterId, 1);
                    break;
                case ItemCategory.StaminaItem:
                    GrantStackableItem(player.items.stamina, product.ItemMasterId, 1);
                    break;
                default:
                    Utils.LogWarning($"Shop product category {product.ItemCategoryId} is not grantable yet; product {product.ItemMasterId} was accepted without inventory mutation.");
                    break;
            }
        }

        private static void GrantStackableItem(IDictionary<long, long> playerItems, long masterId, int quantity)
        {
            if (!playerItems.TryGetValue(masterId, out long itemId) || !Item.Exists(itemId))
            {
                Item newItem = new()
                {
                    id = Item.GetID(),
                    master_id = masterId,
                    quantity = quantity
                };
                playerItems[masterId] = newItem.id;
                newItem.Save();
                return;
            }

            Item existingItem = Item.Load(itemId);
            existingItem.quantity += quantity;
            existingItem.Save();
        }

        private static bool DeductCurrency(PlayerProfile player, ShopProduct product)
        {
            if (Config.Get().InGame.InfiniteItems)
                return true;

            switch (product.ConsumptionResourceId)
            {
                case 4:
                    if (player.data.money < product.Price)
                        return false;

                    player.data.money -= product.Price;
                    return true;
                case 8:
                    if (player.data.braveCoin < product.Price)
                        return false;

                    player.data.braveCoin -= product.Price;
                    return true;
                default:
                    Utils.LogWarning($"Shop product uses unsupported consumption resource {product.ConsumptionResourceId}; no currency was deducted.");
                    return false;
            }
        }

        private sealed class ShopProduct
        {
            public ShopProduct(long itemMasterId, int itemCategoryId, int consumptionResourceId, int price, int purchaseLimitQuantity)
            {
                ItemMasterId = itemMasterId;
                ItemCategoryId = itemCategoryId;
                ConsumptionResourceId = consumptionResourceId;
                Price = price;
                PurchaseLimitQuantity = purchaseLimitQuantity;
            }

            public long ItemMasterId { get; }
            public int ItemCategoryId { get; }
            public int ConsumptionResourceId { get; }
            public int Price { get; }
            public int PurchaseLimitQuantity { get; }
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
                public long item_master_id { get; set; }
                public int item_category_id { get; set; }
                public int purchased_quantity { get; set; }
            }
        }
    }
}
