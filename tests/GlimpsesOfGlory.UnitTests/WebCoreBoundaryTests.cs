using System.Reflection;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Web.Pages;

namespace GlimpsesOfGlory.UnitTests;

/// <summary>
/// Enforces the Web→Core boundary rule: only <c>Program</c> (and the compiler-generated types nested
/// in it for top-level statements, lambdas and local functions) may reference types from the
/// <c>GlimpsesOfGlory.Core</c> assembly. Every other Web type — PageModels, helpers, Razor-compiled
/// views — must go through the interfaces and DTOs in <c>GlimpsesOfGlory.Abstractions</c>.
/// </summary>
public class WebCoreBoundaryTests
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly WebAssembly = typeof(PrivacyModel).Assembly;
    private static readonly Assembly CoreAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void OnlyProgramInWebDependsOnCore()
    {
        var violations = WebAssembly.GetTypes()
            .Where(type => !IsProgramOrNestedInProgram(type))
            .SelectMany(type => FindCoreReferences(type).Select(reference => $"{type.FullName} -> {reference}"))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Web types other than Program must not reference GlimpsesOfGlory.Core types " +
            "(inject an Abstractions interface instead):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ScanDetectsCoreReferences()
    {
        // Guards against the boundary test passing vacuously: a PageModel-shaped type that injects
        // AppDbContext must be reported, naming the Core type and where it is referenced.
        var dbContextReferences = FindCoreReferences(typeof(PageModelInjectingDbContext))
            .Where(r => r.StartsWith(typeof(AppDbContext).FullName!))
            .ToList();

        Assert.Contains(dbContextReferences, r => r.Contains("constructor parameter db"));
        Assert.Contains(dbContextReferences, r => r.Contains("field"));
        Assert.Contains(dbContextReferences, r => r.Contains("parameter dbs of Use"));
    }

    private sealed class PageModelInjectingDbContext(AppDbContext db)
    {
        public AppDbContext Db => db;

        public static void Use(IEnumerable<AppDbContext[]> dbs) => _ = dbs;
    }

    private static bool IsProgramOrNestedInProgram(Type type)
    {
        var outermost = type;
        while (outermost.DeclaringType is not null)
        {
            outermost = outermost.DeclaringType;
        }

        return outermost.Namespace is null && outermost.Name == "Program";
    }

    private static IEnumerable<string> FindCoreReferences(Type type)
    {
        foreach (var referenced in ReferencedTypes(type))
        {
            foreach (var coreType in CoreTypesIn(referenced.Type))
            {
                yield return $"{coreType.FullName} ({referenced.Where})";
            }
        }
    }

    private static IEnumerable<(Type Type, string Where)> ReferencedTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return (type.BaseType, "base type");
        }

        foreach (var iface in type.GetInterfaces())
        {
            yield return (iface, "interface");
        }

        foreach (var field in type.GetFields(DeclaredMembers))
        {
            yield return (field.FieldType, $"field {field.Name}");
        }

        foreach (var property in type.GetProperties(DeclaredMembers))
        {
            yield return (property.PropertyType, $"property {property.Name}");
        }

        foreach (var constructor in type.GetConstructors(DeclaredMembers))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return (parameter.ParameterType, $"constructor parameter {parameter.Name}");
            }
        }

        foreach (var method in type.GetMethods(DeclaredMembers))
        {
            yield return (method.ReturnType, $"return type of {method.Name}");
            foreach (var parameter in method.GetParameters())
            {
                yield return (parameter.ParameterType, $"parameter {parameter.Name} of {method.Name}");
            }
        }
    }

    /// <summary>Unwraps arrays, by-refs, pointers and generic arguments to find any Core types.</summary>
    private static IEnumerable<Type> CoreTypesIn(Type type)
    {
        if (type.HasElementType)
        {
            foreach (var inner in CoreTypesIn(type.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        if (type.IsGenericParameter)
        {
            yield break;
        }

        if (type.Assembly == CoreAssembly)
        {
            yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in CoreTypesIn(argument))
                {
                    yield return inner;
                }
            }
        }
    }
}
