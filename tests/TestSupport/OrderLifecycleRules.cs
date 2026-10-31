using GlimpsesOfGlory.Core.Services;

namespace GlimpsesOfGlory.TestSupport;

// Every rule OrderLifecycle declares, by field name (a transition contributes its guard). Read off
// OrderLifecycle itself, so a newly declared rule or transition can't skip the tests that walk them.
internal static class OrderLifecycleRules
{
    public static readonly IReadOnlyDictionary<string, OrderRule> ByName = typeof(OrderLifecycle)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .ToDictionary(f => f.Name, f => f.GetValue(null) switch
        {
            OrderRule rule => rule,
            OrderTransition transition => transition.Guard,
            var other => throw new InvalidOperationException($"Unexpected OrderLifecycle member {f.Name}: {other}"),
        });
}
