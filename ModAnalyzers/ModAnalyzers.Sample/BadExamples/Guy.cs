using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace ModAnalyzers.Sample.BadExamples;

public class Guy : PlaceholderCharacterModel, ICustomModel
{
    public override Color NameColor { get; }
    public override CharacterGender Gender { get; }
    public override int StartingHp { get; }
    public override CardPoolModel CardPool { get; }
    public override RelicPoolModel RelicPool { get; }
    public override PotionPoolModel PotionPool { get; }
    public override IEnumerable<CardModel> StartingDeck { get; }
    public override IReadOnlyList<RelicModel> StartingRelics { get; }
}