namespace GlimpsesOfGlory.Web.Dtos;

// One step of the admin order timeline. At is null for a pending step, and for a completed step
// that happened before step timestamps were recorded.
public sealed record OrderTimelineStep(string Label, bool IsComplete, DateTimeOffset? At);
