namespace Workshoppa;

public enum Stage
{
    TakeItemFromQueue,
    TargetFabricationStation,

    OpenCraftingLog,
    SelectCraftCategory,
    SelectCraft,
    ConfirmCraft,

    SelectCraftBranch,
    ContributeMaterials,
    ConfirmMaterialDelivery,

    ConfirmCollectProduct,

    RequestStop,
    Stopped,
}
