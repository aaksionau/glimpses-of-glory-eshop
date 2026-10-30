namespace GlimpsesOfGlory.Web.Dtos;

// The −/+ quantity stepper rendered by Pages/Shared/_QuantityStepper.cshtml. Id names the input
// (qty-{Id}) and its buttons (qty-down-{Id}, qty-up-{Id}); ItemName goes into their labels. Max is
// the most the shopper may pick (stock or the preorder cap), or null when the page doesn't know it.
public sealed record QuantityStepperView(
    string Id,
    string ItemName,
    int Value,
    int? Max,
    string? ErrorId = null);
