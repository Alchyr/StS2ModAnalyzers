using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;

namespace ModAnalyzers.Sample;

public class TestEvent : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Overgrowth>()]; 
    
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return [
            Option(Explode),
            LockedOption("asdf")
        ];
    }

    private async Task Explode()
    {
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner!).ToMutable(), Owner!);
        await CardPileCmd.AddCurseToDeck<Clumsy>(Owner!);
        SetEventFinished(PageDescription("ORNATE"));
    }
}