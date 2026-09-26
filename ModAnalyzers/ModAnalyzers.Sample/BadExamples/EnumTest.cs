using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace ModAnalyzers.Sample;

public class EnumTest
{
    [CustomEnum]
    public static CardKeyword MyKeyword;
    
    [CustomEnum]
    public CardKeyword Wrong; //not defined as a CustomEnum properly

    [CustomEnum] 
    public static PileType CustomPileType; //Doesn't need loc
}