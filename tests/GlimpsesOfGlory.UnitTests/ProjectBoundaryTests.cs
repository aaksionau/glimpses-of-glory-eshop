using System.Reflection;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Infrastructure.Services;
using GlimpsesOfGlory.Web.Pages;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetArchTest.Rules;

namespace GlimpsesOfGlory.UnitTests;

/// <summary>
/// Enforces the project boundary rules (see the project-structure skill and docs/adr/0001):
/// <list type="bullet">
/// <item>Web→Core and Web→Infrastructure: only <c>Program</c> (the top-level statements in Program.cs) may
/// depend on the <c>GlimpsesOfGlory.Core</c> or <c>GlimpsesOfGlory.Infrastructure</c> namespaces. Every other
/// Web type — PageModels, helpers, Razor-compiled pages/views — must go through the interfaces and DTOs in
/// <c>GlimpsesOfGlory.Abstractions</c>.</item>
/// <item>Core stays integration-free: it must not depend on Infrastructure, nor on an integration SDK
/// (Stripe) directly.</item>
/// <item>Infrastructure implements Abstractions interfaces and must not depend on Core.</item>
/// </list>
/// NetArchTest reads the IL, so a type used only inside a method body (including async state machines and
/// lambdas, which it folds into the declaring type) is caught too.
/// </summary>
public class ProjectBoundaryTests
{
    private const string CoreNamespace = "GlimpsesOfGlory.Core";
    private const string InfrastructureNamespace = "GlimpsesOfGlory.Infrastructure";
    private const string StripeNamespace = "Stripe";

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly WebAssembly = typeof(PrivacyModel).Assembly;
    private static readonly Assembly CoreAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(StripePaymentGateway).Assembly;

    [Fact]
    public void WebTypes_OtherThanProgram_DoNotDependOnCore() =>
        AssertNoWebTypeOtherThanProgramDependsOn(CoreNamespace, CoreAssembly);

    [Fact]
    public void WebTypes_OtherThanProgram_DoNotDependOnInfrastructure() =>
        AssertNoWebTypeOtherThanProgramDependsOn(InfrastructureNamespace, InfrastructureAssembly);

    [Theory]
    [InlineData(InfrastructureNamespace)]
    [InlineData(StripeNamespace)]
    public void CoreTypes_DoNotDependOn(string forbiddenNamespace)
    {
        AssertNoViolations(
            DependentTypes(Types.InAssembly(CoreAssembly), forbiddenNamespace).Select(type => type.FullName),
            $"Core types must not depend on {forbiddenNamespace} (put the integration in " +
            $"{InfrastructureNamespace} behind an Abstractions interface)");
    }

    [Fact]
    public void InfrastructureTypes_DoNotDependOnCore()
    {
        AssertNoViolations(
            DependentTypes(Types.InAssembly(InfrastructureAssembly), CoreNamespace).Select(type => type.FullName),
            $"Infrastructure types must not depend on {CoreNamespace} (implement an Abstractions interface instead)");
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
    public void CoreTypes_ScannedByStripeRule_WouldCatchAStripeDependency()
    {
        // Guards against the Core→Stripe rule passing vacuously: the same check flags the Stripe adapter.
        Assert.Contains(typeof(StripePaymentGateway), DependentTypes(Types.InAssembly(InfrastructureAssembly), StripeNamespace));
    }

    [Theory]
    [InlineData(CoreNamespace)]
    [InlineData(InfrastructureNamespace)]
    public void AssemblyTypes_AllLiveUnderTheirProjectNamespace(string projectNamespace)
    {
        // The boundary rules match a *namespace*; this keeps that equivalent to the project's *assembly*
        // (each project's assembly is named after its root namespace).
        // Compiler-generated and file-local types (e.g. source-generated regexes) have mangled '<' names
        // and can't be referenced from Web, so they're skipped.
        var outsiders = Assembly.Load(projectNamespace).GetTypes()
            .Where(type => type.Namespace is not null && !OutermostType(type).Name.StartsWith('<'))
            .Where(type => type.Namespace != projectNamespace && !type.Namespace!.StartsWith(projectNamespace + ".", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(outsiders);
    }

    [Fact]
    public void CoreDependentTypes_TypeUsingDbContextOnlyInMethodBody_IsReported()
    {
        // Proves the rule reads method bodies: this type mentions AppDbContext nowhere in its signatures.
        var violators = DependentTypesOtherThanProgram(Types.InAssembly(typeof(ProjectBoundaryTests).Assembly), CoreNamespace);

        Assert.Contains(typeof(UsesDbContextInBody), violators);
    }

    [Fact]
    public void DescribeReferences_TypeInjectingDbContext_NamesTheCoreType()
    {
        Assert.Equal(typeof(AppDbContext).FullName, DescribeReferences(typeof(InjectsDbContext), CoreAssembly));
    }

    [Fact]
    public void DescribeReferences_TypeInjectingAnInfrastructureType_NamesIt()
    {
        Assert.Equal(typeof(StripePaymentGateway).FullName, DescribeReferences(typeof(InjectsPaymentGateway), InfrastructureAssembly));
    }

    private static void AssertNoWebTypeOtherThanProgramDependsOn(string forbiddenNamespace, Assembly forbiddenAssembly)
    {
        AssertNoViolations(
            DependentTypesOtherThanProgram(Types.InAssembly(WebAssembly), forbiddenNamespace)
                .Select(type => $"{type.FullName} -> {DescribeReferences(type, forbiddenAssembly)}"),
            $"Web types other than Program must not depend on {forbiddenNamespace} (inject an Abstractions interface instead)");
    }

    private static void AssertNoViolations(IEnumerable<string?> violations, string rule)
    {
        var list = violations.ToList();

        Assert.True(list.Count == 0, rule + ":" + Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    /// <summary>
    /// Returns the given types, other than <c>Program</c>, that depend on the namespace anywhere —
    /// signatures or method bodies. NetArchTest folds compiler-generated nested types into their
    /// declaring type, so Program's lambdas and local functions are excluded with it.
    /// </summary>
    private static List<Type> DependentTypesOtherThanProgram(Types types, string forbiddenNamespace) =>
        DependentTypes(types, forbiddenNamespace)
            .Where(type => type.FullName != "Program")
            .ToList();

    private static List<Type> DependentTypes(Types types, string forbiddenNamespace)
    {
        var result = types.ShouldNot().HaveDependencyOn(forbiddenNamespace).GetResult();

        return (result.FailingTypes ?? [])
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Names the types from <paramref name="forbiddenAssembly"/> a violating type references through its
    /// base type, interfaces, fields, properties or method/constructor signatures (its own or its
    /// compiler-generated nested types'). NetArchTest reports only the violating type, so this recovers
    /// the "what" for the failure message.
    /// </summary>
    private static string DescribeReferences(Type type, Assembly forbiddenAssembly)
    {
        var referencedTypes = SelfAndNested(type)
            .SelectMany(SignatureTypes)
            .SelectMany(Unwrap)
            .Where(t => t.Assembly == forbiddenAssembly)
            .Select(t => t.FullName ?? t.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        return referencedTypes.Count > 0 ? string.Join(", ", referencedTypes) : "(a type used only inside a method body)";
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

    private sealed class InjectsPaymentGateway(StripePaymentGateway gateway)
    {
        public StripePaymentGateway Gateway { get; } = gateway;
    }
}
