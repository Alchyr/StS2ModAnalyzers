using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ModAnalyzers.Json;

namespace ModAnalyzers;


//TODO - check localizations by language (separate keys into a map by language, report all languages missing keys)
//Probably keys map to a list of languages, and then compare that to list of all languages that exist
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalizationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STS001";    
    public const string NoLocId = "STS002";

    //Required localization data
    private static readonly Dictionary<string, RequiredLocalization> LocData = new()
    {
        {
            "BaseLib.Abstracts.CustomCardModel", //modeltype
            new RequiredLocalization("cards") //file
                .Add("title", "CLASSNAME")  //required entries
                .Add("description")
        },
        {
            "BaseLib.Abstracts.CustomCharacterModel",
            new RequiredLocalization("characters")
                .Add("title", "The CLASSNAME")
                .Add("titleObject", "The CLASSNAME")
                .Add("description", "Character Selection\nScreen Description")
                .Add("pronounObject", "him/her/it")
                .Add("possessiveAdjective", "his/her/its")
                .Add("pronounPossessive", "his/hers/its")
                .Add("pronounSubject", "he/she/it")
                .Add("goldMonologue", "Line spoken when obtaining a large amount of gold")
                .Add("eventDeathPrevention", "Co-op survival line")
                .Add("aromaPrinciple", "Lore")
                .Add("cardsModifierTitle", "__ Cards")
                .Add("cardsModifierDescription", "__ cards will now appear in rewards and shops.")
                .Add("banter.alive.endTurnPing", "Some variation of Hurry Up")
                .Add("banter.dead.endTurnPing", "...")
        },
        {
            "BaseLib.Abstracts.CustomPotionModel",
            new RequiredLocalization("potions")
                .Add("title", "CLASSNAME")
                .Add("description")
        },
        {
            "BaseLib.Abstracts.CustomPowerModel",
            new RequiredLocalization("powers")
                .Add("title", "CLASSNAME")
                .Add("description")
                .Add("smartDescription")
        },
        {
            "BaseLib.Abstracts.CustomRelicModel",
            new RequiredLocalization("relics")
                .Add("title", "CLASSNAME")
                .Add("description")
                .Add("flavor")
        }
    };

    class RequiredLocalization(string filename)
    {
        public readonly string Filename = filename;
        public readonly Dictionary<string, string> RequiredKeys = [];

        public RequiredLocalization Add(string key, string defaultValue = "")
        {
            RequiredKeys.Add(key, defaultValue);
            return this;
        }
    }
    
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.STS001Title),
        Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString NoLocTitle = new LocalizableResourceString(nameof(Resources.STS002Title),
        Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.STS001MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.STS001Description), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString NoLocDescription =
        new LocalizableResourceString(nameof(Resources.STS002Description), Resources.ResourceManager,
            typeof(Resources));

    private const string Category = "Localization";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
    private static readonly DiagnosticDescriptor NoLoc = new(NoLocId, NoLocTitle, NoLocDescription, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule, NoLoc);

    public override void Initialize(AnalysisContext context)
    {
        // You must call this method to avoid analyzing generated code.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // You must call this method to enable the Concurrent Execution.
        //context.EnableConcurrentExecution();

        context.RegisterSymbolAction(CheckSymbol, SymbolKind.NamedType);
    }

    private void CheckSymbol(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedTypeSymbol) return;
        if (namedTypeSymbol.IsAbstract || namedTypeSymbol.IsStatic) return;
        if (!LoadLocalization(context, out var localizationKeys)) return;
        
        Dictionary<string, string> missingKeys = [];
        
        foreach (var entry in LocData)
        {
            if (!namedTypeSymbol.ImplementsInterfaceOrBaseClass(entry.Key)) continue;

            
            var fullName = namedTypeSymbol.FullName();
            var id = namedTypeSymbol.Name.Slugify().AddPrefix(fullName);
            
            foreach (var locEntry in entry.Value.RequiredKeys)
            {
                var key = $"{entry.Value.Filename}.{id}.{locEntry.Key}";
                if (localizationKeys.Contains(key)) continue;

                var result = locEntry.Value.Replace("CLASSNAME", namedTypeSymbol.Name);

                missingKeys.Add($"{id}.{locEntry.Key}", result);
            }

            if (missingKeys.Count == 0) return;

            var builder = ImmutableDictionary.CreateBuilder<string, string?>();
            //For future, list all necessary languages. eg "eng/cards.json, zhs/cards.json"
            builder.Add("LOCFILES", entry.Value.Filename + ".json");
            foreach (var missingKey in missingKeys)
            {
                builder.Add(missingKey.Key, missingKey.Value);
            }
            
            var diagnostic = Diagnostic.Create(Rule,
                namedTypeSymbol.Locations[0],
                builder.ToImmutable(),
                JoinKeys(missingKeys), fullName);
            context.ReportDiagnostic(diagnostic);

            return;
        }
    }

    private bool LoadLocalization(SymbolAnalysisContext context, out HashSet<string> localizationKeys)
    {
        var additionalFiles = context.Options.AdditionalFiles;
        localizationKeys = [];

        var jsonCount = 0;
        
        foreach (var file in additionalFiles)
        {
            if (file == null) continue;
            
            var path = file.Path;
            if (!path.EndsWith(".json")) continue;
            if (!path.Contains("localization")) continue;

            var jsonText = file.GetText()?.ToString();
            if (jsonText == null) continue;

            var loc = JsonValue.Parse(jsonText);

            if (loc is not JsonObject locObj) continue;

            ++jsonCount;

            foreach (var s in locObj.Keys)
            {
                localizationKeys.Add($"{Path.GetFileNameWithoutExtension(path)}.{s}");
            }
        }

        if (jsonCount > 0) return true;
        
        var diagnostic = Diagnostic.Create(NoLoc, null);
        context.ReportDiagnostic(diagnostic);
        return false;

    }

    private static string JoinKeys<T, U>(IDictionary<T, U> dict)
    {
        StringBuilder sb = new();
        bool first = true;
        foreach (var entry in dict)
        {
            if (first) first = false;
            else sb.Append(", ");

            sb.Append(entry.Key);
        }

        return sb.ToString();
    }
}