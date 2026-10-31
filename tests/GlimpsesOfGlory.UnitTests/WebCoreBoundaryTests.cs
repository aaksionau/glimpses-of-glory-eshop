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

    private static readonly Assembly WebAssembly = typeof(PrivacyModel).Assembly;

    [Fact]
    public void WebTypes_OtherThanProgram_DoNotDependOnCore()
    {
        var violators = CoreDependentTypes(Types.InAssembly(WebAssembly));

        Assert.True(
            violators.Count == 0,
            "Web types other than Program must not depend on GlimpsesOfGlory.Core " +
            "(inject an Abstractions interface instead):" + Environment.NewLine +
            string.Join(Environment.NewLine, violators));
    }

    [Fact]
    public void WebTypes_ScannedByBoundaryRule_IncludePageModelsAndRazorViews()
    {
        // Guards against the boundary test passing vacuously because it saw no (or the wrong) types.
        var webTypes = Types.InAssembly(WebAssembly).GetTypes().ToList();

        Assert.Contains(webTypes, type => type == typeof(PrivacyModel));
        Assert.Contains(webTypes, type => type.IsSubclassOf(typeof(Page)));
    }

    [Fact]
    public void CoreDependentTypes_TypeUsingDbContextOnlyInMethodBody_IsReported()
    {
        // Proves the rule reads method bodies: this type mentions AppDbContext nowhere in its signatures.
        var violators = CoreDependentTypes(Types.InAssembly(typeof(WebCoreBoundaryTests).Assembly));

        Assert.Contains(typeof(UsesDbContextInBody).FullName!.Replace('+', '/'), violators);
    }

    /// <summary>
    /// Returns the full names of the given types, other than <c>Program</c>, that depend on the Core
    /// namespace anywhere — signatures or method bodies. NetArchTest folds compiler-generated nested types
    /// into their declaring type, so Program's lambdas and local functions are excluded with it.
    /// </summary>
    private static List<string> CoreDependentTypes(Types types)
    {
        var result = types.ShouldNot().HaveDependencyOn(CoreNamespace).GetResult();

        return (result.FailingTypeNames ?? [])
            .Where(name => name != "Program")
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private sealed class UsesDbContextInBody
    {
        public static string Describe() => typeof(AppDbContext).Name;
    }
}
