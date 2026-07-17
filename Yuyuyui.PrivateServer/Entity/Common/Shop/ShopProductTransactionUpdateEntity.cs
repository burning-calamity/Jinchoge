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

            if (productId == 0 && player.transactions.shopProductTransactions.TryGetValue(transactionId, out var storedTransaction))
            {
                shopId = storedTransaction.shop_id;
                productId = storedTransaction.product_id;
            }

            ShopProduct product = FindProduct(shopId, productId);

            DeductCurrency(player, product);
            GrantProduct(player, product);
            player.transactions.shopProductTransactions.Remove(transactionId);
            player.Save();

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
                    purchased_quantity = 1
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
                ("C3365", 10093) => new ShopProduct(100040, (int)ItemCategory.Card, 4, 50000),
                ("C3365", 10094) => new ShopProduct(100140, (int)ItemCategory.Card, 4, 50000),
                ("C3365", 10095) => new ShopProduct(100140, (int)ItemCategory.Card, 8, 5),
                ("A3365", 10093) => new ShopProduct(500101, (int)ItemCategory.Accessory, 4, 6000),
                ("A3365", 10094) => new ShopProduct(500109, (int)ItemCategory.Accessory, 4, 6000),
                ("A3365", 10095) => new ShopProduct(500091, (int)ItemCategory.Accessory, 4, 8000),
                ("I3365", 10093) => new ShopProduct(2, (int)ItemCategory.EnhancementItem, 4, 1000),
                ("I3365", 10094) => new ShopProduct(10310, (int)ItemCategory.EvolutionItem, 4, 1000),
                ("I3365", 10095) => new ShopProduct(10410, (int)ItemCategory.EvolutionItem, 4, 1000),
                _ => throw new APIErrorException("A1321", $"Unknown shop product {shopId}/{productId}")
            };
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

        private static void DeductCurrency(PlayerProfile player, ShopProduct product)
        {
            if (Config.Get().InGame.InfiniteItems)
                return;

            switch (product.ConsumptionResourceId)
            {
                case 4:
                    player.data.money = Math.Max(0, player.data.money - product.Price);
                    break;
                case 8:
                    player.data.braveCoin = Math.Max(0, player.data.braveCoin - product.Price);
                    break;
                default:
                    Utils.LogWarning($"Shop product uses unsupported consumption resource {product.ConsumptionResourceId}; no currency was deducted.");
                    break;
            }
        }

        private sealed class ShopProduct
        {
            public ShopProduct(long itemMasterId, int itemCategoryId, int consumptionResourceId, int price)
            {
                ItemMasterId = itemMasterId;
                ItemCategoryId = itemCategoryId;
                ConsumptionResourceId = consumptionResourceId;
                Price = price;
            }

            public long ItemMasterId { get; }
            public int ItemCategoryId { get; }
            public int ConsumptionResourceId { get; }
            public int Price { get; }
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
