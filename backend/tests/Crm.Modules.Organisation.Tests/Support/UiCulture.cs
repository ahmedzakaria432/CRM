using System.Globalization;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>Switches <see cref="CultureInfo.CurrentUICulture"/> for one test and restores it on dispose. The
/// culture is async-local, so parallel tests do not see each other's choice.</summary>
public sealed class UiCulture : IDisposable
{
    private readonly CultureInfo _previousUi = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    private UiCulture(string name)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
    }

    public static UiCulture Use(string name) => new(name);

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previousUi;
        CultureInfo.CurrentCulture = _previous;
    }
}
