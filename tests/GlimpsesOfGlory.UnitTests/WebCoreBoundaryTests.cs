using System.Reflection;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Web.Pages;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetArchTest.Rules;

namespace GlimpsesOfGlory.UnitTests;

/// <summary>
/// Enforces the Web→Core boundary rule: only <c>Program</c> (the top-level statements in Program.cs)
/// may depend on the <c>GlimpsesOfGlory.Core</c> namespace. Every other Web type — PageModels, helpers,
/// Razor-compiled pages/views — must go through the interfaces and DTOs in
/// <c>GlimpsesOfGlory.Abstractions</c>. NetArchTest reads the IL, so a Core type used only inside a
/// method body (including async state machines and lambdas, which it folds into the declaring type)
/// is caught too.
/// </summary>
public class WebCoreBoundaryTests
{
    private const string CoreNamespace = "GlimpsesOfGlory.Core";

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly WebAssembly = typeof(PrivacyModel).Assembly;

    [Fact]
    public void WebTypes_OtherThanProgram_DoNotDependOnCore()
    {
        var violations = CoreDependentTypes(Types.InAssembly(WebAssembly))
            .Select(type => $"{type.FullName} -> {DescribeCoreReferences(type)}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Web types other than Program must not depend on {CoreNamespace} " +
            "(inject an Abstractions interface instead):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void WebTypes_ScannedByBoundaryRule_IncludePageModelsAndRazorViews()
    {
        // Guards against the boundary test passing vacuously because it saw no (or the wrong) types.
        var webTypes = Types.InAssembly(WebAssembly).GetTypes().ToList();

        Assert.Contains(typeof(PrivacyModel), webTypes);
        Assert.Contains(webTypes, type => type.IsSubclassOf(typeof(Page)));
    }

    [Fact]
    public void CoreAssemblyTypes_AllLiveUnderCoreNamespace()
    {
        // The boundary rule matches the Core *namespace*; this keeps that equivalent to the Core *assembly*.
        // Compiler-generated and file-local types (e.g. source-generated regexes) have mangled '<' names
        // and can't be referenced from Web, so they're skipped.
        var outsiders = typeof(AppDbContext).Assembly.GetTypes()
            .Where(type => type.Namespace is not null && !OutermostType(type).Name.StartsWith('<'))
            .Where(type => type.Namespace != CoreNamespace && !type.Namespace!.StartsWith(CoreNamespace + ".", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(outsiders);
    }

    [Fact]
    public void CoreDependentTypes_TypeUsingDbContextOnlyInMethodBody_IsReported()
    {
        // Proves the rule reads method bodies: this type mentions AppDbContext nowhere in its signatures.
        var violators = CoreDependentTypes(Types.InAssembly(typeof(WebCoreBoundaryTests).Assembly));

        Assert.Contains(typeof(UsesDbContextInBody), violators);
    }

    [Fact]
    public void DescribeCoreReferences_TypeInjectingDbContext_NamesTheCoreType()
    {
        Assert.Equal(typeof(AppDbContext).FullName, DescribeCoreReferences(typeof(InjectsDbContext)));
    }

    /// <summary>
    /// Returns the given types, other than <c>Program</c>, that depend on the Core namespace anywhere —
    /// signatures or method bodies. NetArchTest folds compiler-generated nested types into their
    /// declaring type, so Program's lambdas and local functions are excluded with it.
    /// </summary>
    private static List<Type> CoreDependentTypes(Types types)
    {
        var result = types.ShouldNot().HaveDependencyOn(CoreNamespace).GetResult();

        return (result.FailingTypes ?? [])
            .Where(type => type.FullName != "Program")
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Names the Core types a violating type references through its base type, interfaces, fields,
    /// properties or method/constructor signatures (its own or its compiler-generated nested types').
    /// NetArchTest reports only the violating type, so this recovers the "what" for the failure message.
    /// </summary>
    private static string DescribeCoreReferences(Type type)
    {
        var coreTypes = SelfAndNested(type)
            .SelectMany(SignatureTypes)
            .SelectMany(Unwrap)
            .Where(t => t.Assembly == typeof(AppDbContext).Assembly)
            .Select(t => t.FullName ?? t.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        return coreTypes.Count > 0 ? string.Join(", ", coreTypes) : "(a Core type used only inside a method body)";
    }

    private static Type OutermostType(Type type) =>
        type.DeclaringType is null ? type : OutermostType(type.DeclaringType);

    private static IEnumerable<Type> SelfAndNested(Type type) =>
        type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(SelfAndNested).Prepend(type);

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var iface in type.GetInterfaces())
        {
            yield return iface;
        }

        foreach (var field in type.GetFields(AllMembers))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(AllMembers))
        {
            yield return property.PropertyType;
        }

        foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
        {
            if (method is MethodInfo info)
            {
                yield return info.ReturnType;
            }

            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType)
        {
            return Unwrap(type.GetElementType()!);
        }

        return type.IsGenericType
            ? type.GetGenericArguments().SelectMany(Unwrap).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }

    private sealed class UsesDbContextInBody
    {
        public static string Describe() => typeof(AppDbContext).Name;
    }

    private sealed class InjectsDbContext(AppDbContext db)
    {
        public AppDbContext Db { get; } = db;
    }
}
