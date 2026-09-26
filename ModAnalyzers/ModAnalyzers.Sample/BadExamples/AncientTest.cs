using BaseLib.Abstracts;
using BaseLib.Utils;

namespace ModAnalyzers.Sample.BadExamples;

public class AncientTest : CustomAncientModel
{
    protected override OptionPools MakeOptionPools => new(MakePool((AncientOption[])[]));
}