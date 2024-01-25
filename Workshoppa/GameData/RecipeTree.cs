using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Workshoppa.GameData;

public sealed class RecipeTree
{
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _pluginLog;
    private readonly IReadOnlyList<uint> _shopItemsOnly;

    public RecipeTree(IDataManager dataManager, IPluginLog pluginLog)
    {
        _dataManager = dataManager;
        _pluginLog = pluginLog;

        // probably incomplete, e.g. different housing districts have different shop types
        var shopVendorIds = new uint[]
        {
            262461, // Purchase Items (Lumber, Metal, Stone, Bone, Leather)
            262462, // Purchase Items (Cloth, Reagents)
            262463, // Purchase Items (Gardening, Dyes)
            262471, // Purchase Items (Catalysts)
            262472, // Purchase (Cooking Ingredients)

            262692, // Amalj'aa
            262422, // Housing District Merchant
            262211, // Z'ranmaia, upper decks
        };

        _shopItemsOnly = _dataManager.GetExcelSheet<GilShopItem>()!
            .Where(x => shopVendorIds.Contains(x.RowId))
            .Select(x => x.Item.Row)
            .Where(x => x > 0)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    public List<Ingredient> ResolveRecipes(List<Ingredient> materials)
    {
        // look up recipes recursively
        int limit = 10;
        List<RecipeInfo> nextStep = ExtendWithAmountCrafted(materials);
        List<RecipeInfo> completeList = new(nextStep);
        while (--limit > 0 && nextStep.Any(x => x.Type == Ingredient.EType.Craftable))
        {
            nextStep = GetIngredients(nextStep);
            completeList.AddRange(nextStep);
        }

        // sum up all recipes
        completeList = completeList.GroupBy(x => x.ItemId)
            .Select(x => new RecipeInfo
            {
                ItemId = x.Key,
                Name = x.First().Name,
                TotalQuantity = x.Sum(y => y.TotalQuantity),
                Type = x.First().Type,
                DependsOn = x.First().DependsOn,
                AmountCrafted = x.First().AmountCrafted,
            })
            .ToList();

        // if a recipe has a specific amount crafted, divide the gathered amount by it
        foreach (var ingredient in completeList.Where(x => x is { AmountCrafted: > 1 }))
        {
            _pluginLog.Information($"Fudging {ingredient.Name}");
            foreach (var part in completeList.Where(x => ingredient.DependsOn.Contains(x.ItemId)))
            {
                _pluginLog.Information($"   → {part.Name}");

                int unmodifiedQuantity = part.TotalQuantity;
                int roundedQuantity = (int)((unmodifiedQuantity + ingredient.AmountCrafted - 1) / ingredient.AmountCrafted);
                part.TotalQuantity = part.TotalQuantity - unmodifiedQuantity + roundedQuantity;
            }
        }

        // figure out the correct order for items to be crafted
        foreach (var item in completeList.Where(x => x.Type == Ingredient.EType.ShopItem))
            item.DependsOn.Clear();
        List<RecipeInfo> sortedList = new List<RecipeInfo>();
        while (sortedList.Count < completeList.Count)
        {
            var craftable = completeList.Where(x =>
                !sortedList.Contains(x) && x.DependsOn.All(y => sortedList.Any(z => y == z.ItemId)))
                .ToList();
            if (craftable.Count == 0)
                throw new Exception("Unable to sort items");

            sortedList.AddRange(craftable.OrderBy(x => x.Name));
        }

        return sortedList.Cast<Ingredient>().ToList();
    }

    private List<RecipeInfo> GetIngredients(List<RecipeInfo> materials)
    {
        List<RecipeInfo> ingredients = new();
        foreach (var material in materials.Where(x => x.Type == Ingredient.EType.Craftable))
        {
            _pluginLog.Information($"Looking up recipe for {material.Name}");

            var recipe = GetFirstRecipeForItem(material.ItemId);
            if (recipe == null)
                continue;

            foreach (var ingredient in recipe.UnkData5.Take(8))
            {
                if (ingredient == null || ingredient.ItemIngredient == 0)
                    continue;

                Item? item = _dataManager.GetExcelSheet<Item>()!.GetRow((uint)ingredient.ItemIngredient);
                if (item == null)
                    continue;

                Recipe? ingredientRecipe = GetFirstRecipeForItem((uint)ingredient.ItemIngredient);

                _pluginLog.Information($"Adding {item.Name}");
                ingredients.Add(new RecipeInfo
                {
                    ItemId = (uint)ingredient.ItemIngredient,
                    Name = item.Name,
                    TotalQuantity = material.TotalQuantity * ingredient.AmountIngredient,
                    Type =
                        _shopItemsOnly.Contains((uint)ingredient.ItemIngredient) ? Ingredient.EType.ShopItem :
                        ingredientRecipe != null ? Ingredient.EType.Craftable :
                        GetGatheringItem((uint)ingredient.ItemIngredient) != null ? Ingredient.EType.Gatherable :
                        GetVentureItem((uint)ingredient.ItemIngredient) != null ? Ingredient.EType.Gatherable :
                        Ingredient.EType.Other,

                    AmountCrafted = ingredientRecipe?.AmountResult ?? 1,
                    DependsOn = ingredientRecipe?.UnkData5.Take(8).Where(x => x != null && x.ItemIngredient != 0)
                                    .Select(x => (uint)x.ItemIngredient)
                                    .ToList()
                                ?? new(),
                });
            }
        }

        return ingredients;
    }

    private List<RecipeInfo> ExtendWithAmountCrafted(List<Ingredient> materials)
    {
        return materials.Select(x => new
            {
                Ingredient = x,
                Recipe = GetFirstRecipeForItem(x.ItemId)
            })
            .Where(x => x.Recipe != null)
            .Select(x => new RecipeInfo
            {
                ItemId = x.Ingredient.ItemId,
                Name = x.Ingredient.Name,
                TotalQuantity = x.Ingredient.TotalQuantity,
                Type = _shopItemsOnly.Contains(x.Ingredient.ItemId) ? Ingredient.EType.ShopItem : x.Ingredient.Type,
                AmountCrafted = x.Recipe!.AmountResult,
                DependsOn = x.Recipe.UnkData5.Take(8).Where(y => y != null && y.ItemIngredient != 0)
                    .Select(y => (uint)y.ItemIngredient)
                    .ToList(),
            })
            .ToList();
    }

    public Recipe? GetFirstRecipeForItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<Recipe>()!.FirstOrDefault(x => x.RowId > 0 && x.ItemResult.Row == itemId);
    }

    public GatheringItem? GetGatheringItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<GatheringItem>()!.FirstOrDefault(x => x.RowId > 0 && (uint)x.Item == itemId);
    }

    public RetainerTaskNormal? GetVentureItem(uint itemId)
    {
        return _dataManager.GetExcelSheet<RetainerTaskNormal>()!
            .FirstOrDefault(x => x.RowId > 0 && x.Item.Row == itemId);
    }

    private sealed class RecipeInfo : Ingredient
    {
        public required uint AmountCrafted { get; init; }
        public required List<uint> DependsOn { get; init; }
    }
}
