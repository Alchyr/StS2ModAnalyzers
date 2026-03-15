using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace BaseLib.Abstracts;

public abstract class CustomCardModel : CardModel, ICustomModel
{
    protected CustomCardModel(int canonicalEnergyCost, CardType type, CardRarity rarity, TargetType targetType, bool shouldShowInCardLibrary = true) : base(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }
}