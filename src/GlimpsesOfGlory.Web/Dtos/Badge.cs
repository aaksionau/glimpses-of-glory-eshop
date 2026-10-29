namespace GlimpsesOfGlory.Web.Dtos;

// A small status/tag pill rendered by Pages/Shared/_Badge.cshtml; CssClass carries the colour scheme.
public sealed record Badge(string Text, string CssClass);
