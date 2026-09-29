using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    private const string ModelLocInterface = "BaseLib.Abstracts.ILocalizationProvider";
    private const string CustomIdAttribute = "BaseLib.Utils.Attributes.CustomIDAttribute";
    

    //Required localization data
    private static readonly Dictionary<string, RequiredLocalization[]> NamedTypeLocData = new()
    {
        {
            "MegaCrit.Sts2.Core.Models.CardModel", //modeltype
            [new RequiredLocalization("cards") //file
                .Add("SYMBOLID.title", "SYMBOLNAME")  //required entries
                .Add("SYMBOLID.description")
            ]
        },
        {
            "MegaCrit.Sts2.Core.Models.CharacterModel",
            [new RequiredLocalization("characters")
                .Add("SYMBOLID.title", "The SYMBOLNAME")
                .Add("SYMBOLID.titleObject", "The SYMBOLNAME")
                .Add("SYMBOLID.description", "Character Selection\\nScreen Description")
                .Add("SYMBOLID.pronounObject", "him/her/it")
                .Add("SYMBOLID.possessiveAdjective", "his/her/its")
                .Add("SYMBOLID.pronounPossessive", "his/hers/its")
                .Add("SYMBOLID.pronounSubject", "he/she/it")
                .Add("SYMBOLID.goldMonologue", "Line spoken when obtaining a large amount of gold")
                .Add("SYMBOLID.eventDeathPrevention", "Co-op survival line")
                .Add("SYMBOLID.aromaPrinciple", "Lore")
                .Add("SYMBOLID.cardsModifierTitle", "__ Cards")
                .Add("SYMBOLID.cardsModifierDescription", "__ cards will now appear in rewards and shops.")
                .Add("SYMBOLID.banter.alive.endTurnPing", "Co-op hurry up end turn ping message")
                .Add("SYMBOLID.banter.dead.endTurnPing", "..."),
            new RequiredLocalization("ancients")
                .Add(new LocInfo(key =>
                [
                    new("THE_ARCHITECT.talk.SYMBOLID.0-0r.char", "I am angry at the architect"),
                    new("THE_ARCHITECT.talk.SYMBOLID.0-0r.next", "Continue"),
                    new("THE_ARCHITECT.talk.SYMBOLID.0-1r.ancient", "You die"),
                    new("THE_ARCHITECT.talk.SYMBOLID.0-attack", "Both")
                ], 
                    "THE_ARCHITECT.talk.SYMBOLID.0-0r.char",
                    "THE_ARCHITECT.talk.SYMBOLID.0-0r.ancient",
                    "THE_ARCHITECT.talk.SYMBOLID.0-0.char",
                    "THE_ARCHITECT.talk.SYMBOLID.0-0.ancient"
                ))
            ]
        },
        {
            "MegaCrit.Sts2.Core.Models.PotionModel",
            [new RequiredLocalization("potions")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")]
        },
        {
            "MegaCrit.Sts2.Core.Models.PowerModel",
            [new RequiredLocalization("powers")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")
                .Add("SYMBOLID.smartDescription")]
        },
        {
            "MegaCrit.Sts2.Core.Models.RelicModel",
            [new RequiredLocalization("relics")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")
                .Add("SYMBOLID.flavor")]
        },
        {
            "MegaCrit.Sts2.Core.Models.AncientEventModel",
            [new RequiredLocalization("ancients")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.epithet")
                .Add("SYMBOLID.talk.firstVisitEver.0-0.ancient", "First time greeting.")
                .Add(["SYMBOLID.talk.ANY.0-0r.ancient",
                    "SYMBOLID.talk.ANY.0-0r.char",
                    "SYMBOLID.talk.ANY.0-0.ancient",
                    "SYMBOLID.talk.ANY.0-0.char"
                ], "Reusable generic greeting.")]
        },
        {
            "MegaCrit.Sts2.Core.Models.ActModel",
            [new RequiredLocalization("acts")
                .Add("SYMBOLID.title", "SYMBOLNAME")
            ]
        },
        {
            "MegaCrit.Sts2.Core.Models.MonsterModel",
            [new RequiredLocalization("monsters")
                .Add("SYMBOLID.name", "SYMBOLNAME")
            ]
        },
        {
            "MegaCrit.Sts2.Core.Models.EncounterModel",
            [new RequiredLocalization("encounters")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.loss", "How did {character} die to [gold]{encounter}[/gold]?")
            ]
        }
    };

    private static readonly Dictionary<string, RequiredLocalization[]> EnumLocData = new()
    {
        {
            "CardKeyword", //enum type name
            [
                new RequiredLocalization("card_keywords") //file
                    .Add("SYMBOLID.title", "NAME") //required entries
                    .Add("SYMBOLID.description", "Tooltip")
            ]
        }
    };

    private static readonly Dictionary<string, string[]> CodeLocalizationData = new()
    {
        { "ActLoc", [ "title" ] },
        { "CardModifierLoc", [ "title", "description" ] },
        { "CardLoc", [ "title", "description" ] },
        { "CharacterLoc", [] },
        { "EncounterLoc", [ "title", "loss" ] },
        { "ModifierLoc", [ "title", "description" ] },
        { "MonsterLoc", [ "name" ] },
        { "OrbLoc", [ "title", "description", "smartDescription" ] },
        { "PotionLoc", [ "title", "description" ] },
        { "PowerLoc", [ "title", "description", "smartDescription" ] },
        { "RelicLoc", [ "title", "description", "flavor" ] }
    };

    /// <summary>
    /// Method overrides that disable entries for specific models.
    /// </summary>
    private static readonly Dictionary<string, KeyValuePair<string, string>[]> OverrideIgnores = new()
    {
        {
            "MegaCrit.Sts2.Core.Models.PowerModel",
            [
                new("Title", "SYMBOLID.title"),
                new("Description", "SYMBOLID.description"),
                new("SmartDescriptionLocKey", "SYMBOLID.smartDescription")
            ]
        }
    };

    class RequiredLocalization(string filename)
    {
        public readonly string Filename = filename;
        public readonly List<LocInfo> RequiredKeys = [];

        public RequiredLocalization Add(string key, string defaultValue = "")
        {
            RequiredKeys.Add(new(defaultValue, key));
            return this;
        }
        public RequiredLocalization Add(string[] keys, string defaultValue = "")
        {
            RequiredKeys.Add(new(defaultValue, keys));
            return this;
        }

        public RequiredLocalization Add(LocInfo loc)
        {
            RequiredKeys.Add(loc);
            return this;
        }
    }

    class LocInfo
    {
        public LocInfo(Func<string, IEnumerable<Tuple<string, string>>> genLoc, params string[] locKeys)
        {
            LocFunc = genLoc;
            LocKeys = locKeys;
        }
        public LocInfo(string defaultLoc, params string[] locKeys)
        {
            LocFunc = (key) => [new Tuple<string, string>(key, defaultLoc)];
            LocKeys = locKeys;
        }

        /// <summary>
        /// Func that receives generated localization key and returns arbitrary number of generated loc entries.
        /// The generated loc entries still need text replacement.
        /// </summary>
        public Func<string, IEnumerable<Tuple<string, string>>> LocFunc { get; }
        
        public string[] LocKeys { get; }
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
        DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: "CompilationEnd");
    private static readonly DiagnosticDescriptor CustomModelRule = new(CustomModelRuleId, CustomModelTitle, CustomModelFormat, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CustomModelDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Rule, NoLoc, CustomModelRule, LoggingDiagnostic.Fake];

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

        var customModelInterface = 
            context.Compilation.GetTypeByMetadataName(CustomModelInterface);
        var customLocInterface = 
            context.Compilation.GetTypeByMetadataName(ModelLocInterface);
        var idAttribute =
            context.Compilation.GetTypeByMetadataName(CustomIdAttribute);
        
        context.RegisterSymbolAction(
            analysisContext => CheckSymbol(analysisContext, customModelInterface, customLocInterface, idAttribute), 
            SymbolKind.NamedType);
        context.RegisterSymbolAction(CheckField, SymbolKind.Field);
        /*context.RegisterSymbolAction(analysisContext => CheckMethod(analysisContext, customLocInterface, idAttribute),
            SymbolKind.Method);
        
        context.RegisterOperationAction(null, OperationKind.MethodReference);
        context.RegisterSyntaxNodeAction(syntaxContext => CheckMethod(syntaxContext, customLocInterface, idAttribute), SyntaxKind.InvocationExpression);*/

        context.RegisterCompilationEndAction(endContext =>
        {
            if (receivedJson) return;
            var diagnostic = Diagnostic.Create(NoLoc, null);
            endContext.ReportDiagnostic(diagnostic);
        });
    }

    private void CheckSymbol(SymbolAnalysisContext context, INamedTypeSymbol? customModel, INamedTypeSymbol? locProvider, INamedTypeSymbol? idAttribute)
    {
        if (_currentLocKeys == null) return;
        if (context.Symbol is not INamedTypeSymbol namedTypeSymbol) return;
        if (namedTypeSymbol.IsAbstract || namedTypeSymbol.IsStatic) return;
        
        Dictionary<string, string> missingKeys = [];
        
        foreach (var entry in NamedTypeLocData)
        {
            if (!namedTypeSymbol.ImplementsInterfaceOrBaseClass(entry.Key)) continue;
            var isCustomModel = namedTypeSymbol.ImplementsInterface(customModel);
            
            //Check for localization provided through alternative means
            List<string> ignoreKeys = [];
            if (OverrideIgnores.TryGetValue(entry.Key, out var overrideIgnores))
            {
                foreach (var overrideIgnore in overrideIgnores)
                {
                    if (namedTypeSymbol.OverridesMethodOrProperty(entry.Key, overrideIgnore.Key))
                    {
                        ignoreKeys.Add(overrideIgnore.Value);
                    }
                }
            }

            ISet<string>? ignoreOnce = null; //Only ignored in first required loc;
                                          //secondary required loc is in a different file and so is not ignored.
            
            if (namedTypeSymbol.ImplementsInterface(locProvider))
            {
                ignoreOnce = FindAndGetLocalizationDeclaration(namedTypeSymbol, "SYMBOLID", context);
                context.Log("ProvidedLoc: " + (ignoreOnce == null ? "null" : string.Join(",", ignoreOnce)), namedTypeSymbol.Locations[0]);
            }
            
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

            var customIdAttribute = namedTypeSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(idAttribute, attr.AttributeClass));
            
            var fullName = namedTypeSymbol.FullName();
            var prefix = fullName.GetPrefix();
            var id = customIdAttribute?.AttributeArgumentString(0) ?? (isCustomModel ? prefix : "") + namedTypeSymbol.Name.Slugify();
            
            foreach (var requiredLoc in entry.Value)
            {
                missingKeys.Clear();
                
                var once = ignoreOnce;
                FindMissingKeys(missingKeys, requiredLoc, id, namedTypeSymbol.Name, 
                    (locKey) => ignoreKeys.Contains(locKey) ||
                                (once != null && (once.Count == 0 || once.Contains(locKey)))
                    );

                ignoreOnce = null;

                if (missingKeys.Count == 0) continue;

                var builder = ImmutableDictionary.CreateBuilder<string, string?>();
                //For future, list all necessary languages. eg "eng/cards.json, zhs/cards.json"
                builder.Add("LOCFILES", requiredLoc.Filename + ".json");
                foreach (var missingKey in missingKeys)
                {
                    builder.Add(missingKey.Key, missingKey.Value);
                }
                
                var diagnostic = Diagnostic.Create(Rule,
                    namedTypeSymbol.Locations[0],
                    builder.ToImmutable(),
                    JoinKeys(missingKeys), fullName);
                context.ReportDiagnostic(diagnostic);
            }
            return;
        }
    }

    //TODO - detect used methods and add required loc
    //option generation for events, selection prompt for cards
    private void CheckMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol? locProvider, INamedTypeSymbol? idAttribute)
    {
        if (_currentLocKeys == null) return;
        if (context.Node is not InvocationExpressionSyntax invocation) return;

        var identifierName = invocation.FindChild<IdentifierNameSyntax>();
        if (identifierName == null) return;
        
        switch (identifierName.Identifier.Text)
        {
            case "LockedOption":
                break;
            case "Option":
                break;
        }
    }

    private ISet<string>? FindAndGetLocalizationDeclaration(INamedTypeSymbol? symbol, string symbolId, SymbolAnalysisContext context)
    {
        while (symbol != null)
        {
            foreach (var member in symbol.GetMembers())
            {
                if (member is not IPropertySymbol || !member.IsOverride ||
                    !member.Name.Equals("Localization")) continue;
                
                var syntaxReferences = member.DeclaringSyntaxReferences;
                if (syntaxReferences.Length == 0) return null;

                var syntax = syntaxReferences[0].GetSyntax();
                syntax = syntax.FindPropertyGetter(context);
                if (syntax == null) return null;

                return GetLocalizationKeys(syntax, symbolId, context);
            }

            symbol = symbol.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Returns localization keys defined by a syntax node specifically for a property
    /// of type List(string, string). Returns an empty list if it could not be analyzed.
    /// </summary>
    /// <param name="syntax"></param>
    /// <param name="symbolId"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    private ISet<string>? GetLocalizationKeys(SyntaxNode syntax, string symbolId, SymbolAnalysisContext context)
    {
        var nullReturn =
            syntax.FindChild<LiteralExpressionSyntax>(test => test.IsKind(SyntaxKind.NullLiteralExpression));
        if (nullReturn != null) return null;

        SyntaxNode? objectCreation = syntax.FindChild<ObjectCreationExpressionSyntax>();

        IEnumerable<SyntaxNode> collectionItems;
        if (objectCreation is ObjectCreationExpressionSyntax objectCreationSyntax)
        {
            var typeName = objectCreationSyntax.CreationTypeName();
            context.Log(typeName, syntax.GetLocation());
            //Special localization types provided by BaseLib
            if (CodeLocalizationData.TryGetValue(typeName, out var locNames))
            {
                return locNames.Select(name => $"{symbolId}.{name}").ToImmutableHashSet();
            }
            
            //Check for collection initializer
            objectCreation = objectCreation.FindChild<ExpressionSyntax>(test => 
                    test.IsKind(SyntaxKind.CollectionInitializerExpression));
            collectionItems = objectCreation?.ChildNodes()
                .OfType<TupleExpressionSyntax>() ?? [];
        }
        else
        {
            var collectionExpression = syntax.FindChild<CollectionExpressionSyntax>();
            if (collectionExpression == null) return ImmutableHashSet<string>.Empty;

            collectionItems = collectionExpression.ChildNodes().OfType<CollectionElementSyntax>()
                .Select(element => element.FindChild<TupleExpressionSyntax>()).OfType<TupleExpressionSyntax>();
        }

        HashSet<string> results = [];
        foreach (var item in collectionItems)
        {
            var firstValue = item.FindChild<ArgumentSyntax>()
                ?.FindChild<LiteralExpressionSyntax>(test => test.IsKind(SyntaxKind.StringLiteralExpression));
            if (firstValue == null)
                return ImmutableHashSet<string>.Empty;

            results.Add($"{symbolId}.{firstValue.Token.ValueText}");
        }
        
        return results; 
    }

    private void CheckField(SymbolAnalysisContext context)
    {
        if (_currentLocKeys == null) return;
        if (context.Symbol is not IFieldSymbol fieldSymbol) return;
        if (!fieldSymbol.IsStatic || fieldSymbol.IsReadOnly) return;
        
        var attributes = fieldSymbol.GetAttributes();
        AttributeData? enumAttr = null;
        AttributeData? keywordProperties = null;
        foreach (var attr in attributes)
        {
            if ("CustomEnumAttribute".Equals(attr.AttributeClass?.Name))
            {
                enumAttr = attr;
            }
            else if ("KeywordPropertiesAttribute".Equals(attr.AttributeClass?.Name))
            {
                keywordProperties = attr;
            }
        }

        if (enumAttr != null)
        {
            var name = fieldSymbol.Name;
            var containingType = fieldSymbol.ContainingType;
            
            if (containingType == null) return;
            
            Dictionary<string, string> missingKeys = [];
            
            foreach (var entry in EnumLocData)
            {
                if (!fieldSymbol.Type.Name.Contains(entry.Key)) continue;
                
                if (enumAttr.ConstructorArguments.Length > 0)
                {
                    var nameArg = enumAttr.ConstructorArguments[0].Value;
                    if (nameArg != null) name = nameArg.ToString();
                }
                var prefix = containingType.FullName().GetPrefix();
                var id = prefix + name.ToUpperInvariant();
        
                foreach (var requiredLoc in entry.Value)
                {
                    missingKeys.Clear();
                    
                    FindMissingKeys(missingKeys, requiredLoc, id, name);

                    if (missingKeys.Count == 0) continue;

                    var builder = ImmutableDictionary.CreateBuilder<string, string?>();
                    //For future, list all necessary languages. eg "eng/cards.json, zhs/cards.json"
                    builder.Add("LOCFILES", requiredLoc.Filename + ".json");
                    foreach (var missingKey in missingKeys)
                    {
                        builder.Add(missingKey.Key, missingKey.Value);
                    }
            
                    var diagnostic = Diagnostic.Create(Rule,
                        fieldSymbol.Locations[0],
                        builder.ToImmutable(),
                        JoinKeys(missingKeys), name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    //Finds all localization keys that don't exist in current loc table and add them to passed missingKeys dictionary
    private void FindMissingKeys(Dictionary<string, string> missingKeys, RequiredLocalization loc,
        string id, string name,
        Predicate<string>? skipCheck = null)
    {
        if (_currentLocKeys == null) return;
        foreach (var locEntry in loc.RequiredKeys)
        {
            bool locFine = false;
            string? defaultKey = null;
                    
            foreach (var locDef in locEntry.LocKeys)
            {
                if (skipCheck != null && skipCheck(locDef))
                {
                    locFine = true;
                    break;
                }
                
                var key = ReplaceSpecial(locDef, id, name);
                defaultKey ??= key;
                
                if (_currentLocKeys.Contains($"{loc.Filename}.{key}"))
                {
                    locFine = true;
                    break;
                }
            }

            if (locFine || defaultKey == null) continue;

            foreach (var defaultLoc in locEntry.LocFunc(defaultKey))
            {
                missingKeys.Add(ReplaceSpecial(defaultLoc.Item1, id, name), ReplaceSpecial(defaultLoc.Item2, id, name));
            }
        }
    }

    private static string ReplaceSpecial(string orig, string id, string name)
    {
        string result = orig.Replace("SYMBOLID", id);
        result = result.Replace("SYMBOLNAME", name);
        return result;
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