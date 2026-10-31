using System.Globalization;
using System.Runtime.CompilerServices;

namespace GlimpsesOfGlory.TestSupport;

/// <summary>
/// Pins the test run to en-US, mirroring Program.cs, so rendered money ("$25.00") doesn't
/// depend on the machine's culture (Linux CI runners default to the invariant culture).
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void UseAppCulture()
    {
        var culture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
