using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace ModAnalyzers.Sample;

public class SomePower : InBetweenPower, ICustomModel
{
    public override PowerType Type { get; }
    public override PowerStackType StackType { get; }
}