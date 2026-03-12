using System;
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
    public const string CustomModelRuleId = "STS003";

    private const string BaseLibAbstracts = "BaseLib.Abstracts.Custom";
    private const string CustomModelInterface = "BaseLib.Abstracts.ICustomModel";
    

    //Required localization data
    private static readonly Dictionary<string, RequiredLocalization> LocData = new()
    {
        {
            "MegaCrit.Sts2.Core.Models.CardModel", //modeltype
            new RequiredLocalization("cards") //file
                .Add("title", "CLASSNAME")  //required entries
                .Add("description")
        },
        {
            "MegaCrit.Sts2.Core.Models.CharacterModel",
            new RequiredLocalization("characters")
                .Add("title", "The CLASSNAME")
                .Add("titleObject", "The CLASSNAME")
                .Add("description", "Character Selection\\nScreen Description")
                .Add("pronounObject", "him/her/it")
                .Add("possessiveAdjective", "his/her/its")
                .Add("pronounPossessive", "his/hers/its")
                .Add("pronounSubject", "he/she/it")
                .Add("goldMonologue", "Line spoken when obtaining a large amount of gold")
                .Add("eventDeathPrevention", "Co-op survival line")
                .Add("aromaPrinciple", "Lore")
                .Add("cardsModifierTitle", "__ Cards")
                .Add("cardsModifierDescription", "__ cards will now appear in rewards and shops.")
                .Add("banter.alive.endTurnPing", "Co-op hurry up end turn ping message")
                .Add("banter.dead.endTurnPing", "...")
        },
        {
            "MegaCrit.Sts2.Core.Models.PotionModel",
            new RequiredLocalization("potions")
                .Add("title", "CLASSNAME")
                .Add("description")
        },
        {
            "MegaCrit.Sts2.Core.Models.PowerModel",
            new RequiredLocalization("powers")
                .Add("title", "CLASSNAME")
                .Add("description")
                .Add("smartDescription")
        },
        {
            "MegaCrit.Sts2.Core.Models.RelicModel",
            new RequiredLocalization("relics")
                .Add("title", "CLASSNAME")
                .Add("description")
                .Add("flavor")
        },
        {
            "MegaCrit.Sts2.Core.Models.AncientEventModel",
            new RequiredLocalization("ancients")
                .Add("title", "CLASSNAME")
                .Add("epithet")
                .Add("talk.firstVisitEver.0-0.ancient", "First time greeting.")
                .Add("talk.ANY.0-0r.ancient", "Reusable generic greeting.")
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
    private static readonly LocalizableString CustomModelTitle = new LocalizableResourceString(nameof(Resources.STS003Title),
        Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.STS001MessageFormat), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString CustomModelFormat =
        new LocalizableResourceString(nameof(Resources.STS003MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.STS001Description), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString NoLocDescription =
        new LocalizableResourceString(nameof(Resources.STS002Description), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString CustomModelDescription =
        new LocalizableResourceString(nameof(Resources.STS003Description), Resources.ResourceManager,
            typeof(Resources));

    private const string Category = "Localization";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
    private static readonly DiagnosticDescriptor NoLoc = new(NoLocId, NoLocTitle, NoLocDescription, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor CustomModelRule = new(CustomModelRuleId, CustomModelTitle, CustomModelFormat, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CustomModelDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule, NoLoc, CustomModelRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterCompilationStartAction(LoadLocOnce);
    }

    private HashSet<string>? _currentLocKeys;

    private void LoadLocOnce(CompilationStartAnalysisContext context)
    {
        var additionalFiles = context.Options.AdditionalFiles;
        _currentLocKeys = [];
        bool receivedJson = false;
        
        foreach (var file in additionalFiles)
        {
            if (file == null) continue;
            
            var path = file.Path;
            if (!path.EndsWith(".json")) continue;
            if (!path.Contains("localization")) continue;

            receivedJson = true;

            var jsonText = file.GetText()?.ToString();
            if (jsonText == null) continue;

            try
            {
                var fileKey = Path.GetFileNameWithoutExtension(path);
                var loc = JsonValue.Parse(jsonText);
                if (loc is not JsonObject locObj) continue;
                foreach (var s in locObj.Keys)
                {
                    _currentLocKeys.Add($"{fileKey}.{s}");
                }
            }
            catch (Exception) { }
        }
        
        context.RegisterSymbolAction(CheckSymbol, SymbolKind.NamedType);
        context.RegisterCompilationEndAction(endContext =>
        {
            if (receivedJson) return;
            var diagnostic = Diagnostic.Create(NoLoc, null);
            endContext.ReportDiagnostic(diagnostic);
        });
    }

    private void CheckSymbol(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedTypeSymbol) return;
        if (namedTypeSymbol.IsAbstract || namedTypeSymbol.IsStatic) return;
        if (_currentLocKeys == null) return;
        
        Dictionary<string, string> missingKeys = [];
        
        foreach (var entry in LocData)
        {
            if (!namedTypeSymbol.ImplementsInterfaceOrBaseClass(entry.Key)) continue;
            var isCustomModel = namedTypeSymbol.ImplementsInterfaceOrBaseClass(CustomModelInterface);

            if (!isCustomModel)
            {
                var customModelName = entry.Key;
                var index = customModelName.LastIndexOf('.');
                customModelName = BaseLibAbstracts + customModelName.Substring(index + 1);
                var modelTypeDiagnostic = Diagnostic.Create(CustomModelRule,
                    namedTypeSymbol.Locations[0],
                    customModelName);
                context.ReportDiagnostic(modelTypeDiagnostic);
            }
            
            var fullName = namedTypeSymbol.FullName();
            var id = namedTypeSymbol.Name.Slugify();
            if (isCustomModel) id = id.AddPrefix(fullName);
            
            foreach (var locEntry in entry.Value.RequiredKeys)
            {
                var key = $"{entry.Value.Filename}.{id}.{locEntry.Key}";
                if (_currentLocKeys.Contains(key)) continue;

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