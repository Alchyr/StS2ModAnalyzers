using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ModAnalyzers.Sample.BadExamples;

public class WrongTypeCard() : CardModel(1, CardType.Attack, CardRarity.Ancient, TargetType.AllAllies)
{
    
}