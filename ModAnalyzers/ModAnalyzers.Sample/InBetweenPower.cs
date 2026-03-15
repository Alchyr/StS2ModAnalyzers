using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ModAnalyzers.Sample;

public abstract class InBetweenPower : PowerModel
{
    public override LocString Title => null;
    public override LocString Description { get; }
}