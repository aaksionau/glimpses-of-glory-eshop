namespace GlimpsesOfGlory.Web.Pages.Admin;

// One dashboard number. Href is the filtered list it summarises, or null when there's no such list.
public sealed record DashboardTile(string Label, string Value, string Description, string? Href);
