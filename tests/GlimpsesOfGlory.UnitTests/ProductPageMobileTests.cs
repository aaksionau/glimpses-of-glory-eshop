using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Dtos;

namespace GlimpsesOfGlory.UnitTests;

// Product page on mobile (#99): the call to action above the description, the cart's −/+ stepper
// bounded by stock / the preorder cap, and a keyboard-accessible lightbox.
public class ProductPageMobileTests
{
    private const string GalleryPath = "~/Pages/Products/_ProductGallery.cshtml";
    private const string StepperPath = "~/Pages/Shared/_QuantityStepper.cshtml";

    [Fact]
    public void DetailsPage_PutsTheOrderFormAboveTheDescription()
    {
        var page = DetailsPage();
        var form = page.IndexOf("data-testid=\"add-to-cart\"", StringComparison.Ordinal);
        var description = page.IndexOf("data-testid=\"product-description\"", StringComparison.Ordinal);

        Assert.True(form >= 0 && description >= 0);
        Assert.True(form < description, "The add-to-cart form should come before the description.");
        Assert.True(page.IndexOf("data-testid=\"delivery-note\"", StringComparison.Ordinal) is var note && note > form && note < description);
        Assert.Contains("Free local pickup", page);
    }

    [Fact]
    public void DetailsPage_UsesTheSharedStepper_BoundedByStockOrThePreorderCap()
    {
        var page = DetailsPage();

        Assert.Contains("var maxQuantity = Model.Product.IsPreorder ? PreorderPolicy.MaxQuantityPerLine : Model.Product.StockQuantity;", page);
        Assert.Contains("<partial name=\"_QuantityStepper\" model=\"new QuantityStepperView(Model.Product.Slug, Model.Product.Name, 1, maxQuantity)\" />", page);
        Assert.DoesNotContain("type=\"number\"", page);
    }

    [Fact]
    public void CartLines_UseTheSameStepper()
    {
        var cartLines = ReadPage("Cart/_CartLines.cshtml");

        Assert.Contains("<partial name=\"_QuantityStepper\"", cartLines);
        Assert.DoesNotContain("type=\"number\"", cartLines);
    }

    [Fact]
    public void DetailsPage_BackLink_IsATallTapTarget()
    {
        var link = Regex.Match(DetailsPage(), "<a href=\"/products\" class=\"([^\"]*)\">").Groups[1].Value;

        Assert.Contains("min-h-11", link.Split(' '));
        Assert.DoesNotContain("text-sm", link.Split(' '));
    }

    [Fact]
    public async Task Stepper_IsBoundedByItsMax()
    {
        var html = await RenderAsync(StepperPath, new QuantityStepperView("sunrise", "Sunrise", 1, 3));

        Assert.Matches("<input type=\"number\" id=\"qty-sunrise\" name=\"quantity\" value=\"1\" min=\"1\" max=\"3\"", html);
        Assert.Matches("<button type=\"button\" id=\"qty-down-sunrise\" aria-label=\"Decrease quantity of Sunrise\"[^>]*x-on:click=\"step\\(false\\)\"", html);
        Assert.Matches("<button type=\"button\" id=\"qty-up-sunrise\" aria-label=\"Increase quantity of Sunrise\"[^>]*x-on:click=\"step\\(true\\)\"", html);
        Assert.Contains("<label for=\"qty-sunrise\" class=\"sr-only\">", html);
        Assert.DoesNotContain("aria-invalid", html);
    }

    [Fact]
    public async Task Stepper_WithoutAMax_LeavesItOff_AndPointsAtItsError()
    {
        var html = await RenderAsync(StepperPath, new QuantityStepperView("sunrise", "Sunrise", 2, null, "error-sunrise"));

        Assert.DoesNotContain("max=", html);
        Assert.Contains("aria-describedby=\"error-sunrise\" aria-invalid=\"true\"", html);
    }

    [Fact]
    public async Task Gallery_OpensFromAKeyboardButton()
    {
        var html = await RenderGalleryAsync("a.jpg", "b.jpg", "c.jpg");

        var trigger = Regex.Match(html, "<button[^>]*x-ref=\"trigger\"[^>]*>", RegexOptions.Singleline).Value;
        Assert.Contains("type=\"button\"", trigger);
        Assert.Contains("aria-haspopup=\"dialog\"", trigger);
        Assert.Contains("x-on:click=\"openLightbox()\"", trigger);
        Assert.DoesNotMatch("<div[^>]*@click", html);
    }

    [Fact]
    public async Task Lightbox_IsAModalThatTrapsFocus_AndClosesOnEscapeOrItsCloseButton()
    {
        var html = await RenderGalleryAsync("a.jpg", "b.jpg");
        var lightbox = Lightbox(html);

        Assert.Contains("role=\"dialog\"", lightbox);
        Assert.Contains("aria-modal=\"true\"", lightbox);
        Assert.Contains("x-trap.inert.noscroll=\"open\"", lightbox);
        Assert.Contains("x-on:keydown.escape.prevent.stop=\"closeLightbox()\"", lightbox);
        Assert.Matches("<button type=\"button\" data-lightbox-close aria-label=\"Close photos\" x-on:click=\"closeLightbox\\(\\)\"", lightbox);
        // Closing hands focus back to the photo that opened it.
        Assert.Contains("closeLightbox() { this.open = false; this.$nextTick(() => this.$refs.trigger.focus()); }", html);
    }

    [Fact]
    public async Task Lightbox_WithSeveralPhotos_HasPrevNext_ArrowKeys_AndSwipe()
    {
        var lightbox = Lightbox(await RenderGalleryAsync("a.jpg", "b.jpg", "c.jpg"));

        Assert.Matches("data-lightbox-prev aria-label=\"Previous photo\" x-on:click=\"prev\\(\\)\"", lightbox);
        Assert.Matches("data-lightbox-next aria-label=\"Next photo\" x-on:click=\"next\\(\\)\"", lightbox);
        Assert.Contains("x-on:keydown.arrow-left=\"prev()\"", lightbox);
        Assert.Contains("x-on:keydown.arrow-right=\"next()\"", lightbox);
        Assert.Contains("x-on:touchend=\"swipeEnd($event)\"", lightbox);
        Assert.Contains("of 3", lightbox);
    }

    [Fact]
    public async Task Lightbox_WithOnePhoto_HasNoPrevNext()
    {
        var html = await RenderGalleryAsync("a.jpg");

        Assert.Contains("data-lightbox-close", html);
        Assert.DoesNotContain("data-lightbox-prev", html);
        Assert.DoesNotContain("data-lightbox-next", html);
    }

    [Fact]
    public async Task Gallery_WithoutPhotos_SaysSo()
    {
        var html = await RenderGalleryAsync();

        Assert.Contains("No photos", html);
        Assert.DoesNotContain("data-lightbox", html);
    }

    [Fact]
    public void DetailsPage_LoadsTheAlpineFocusPlugin_InTheScriptsSection()
    {
        // The layout renders the Scripts section before Alpine, so the plugin registers first.
        var page = DetailsPage();
        var section = page.IndexOf("@section Scripts", StringComparison.Ordinal);

        Assert.True(section >= 0);
        Assert.True(page.IndexOf("~/js/alpine-focus.min.js", section, StringComparison.Ordinal) > section);
        var layout = ReadPage("Shared/_Layout.cshtml");
        Assert.True(layout.IndexOf("RenderSectionAsync(\"Scripts\"", StringComparison.Ordinal) < layout.IndexOf("~/js/alpine.min.js", StringComparison.Ordinal));
    }

    private static readonly Lazy<string> DetailsSource = new(() => ReadPage("Products/Details.cshtml"));

    private static readonly Lazy<GlimpsesOfGlory.Web.Helpers.RazorViewToStringRenderer> Renderer =
        new(RazorViewToStringRendererTests.CreateRenderer);

    private static string DetailsPage() => DetailsSource.Value;

    private static string ReadPage(string page) => File.ReadAllText(PrimaryButtonTests.PagePath(page));

    private static string Lightbox(string html)
    {
        var start = html.IndexOf("data-lightbox", StringComparison.Ordinal);
        Assert.True(start >= 0, "No lightbox rendered");
        return html[html.LastIndexOf("<div", start, StringComparison.Ordinal)..];
    }

    private static Task<string> RenderGalleryAsync(params string[] photos) =>
        RenderAsync(GalleryPath, new ProductDetail("sunrise", "Sunrise", "A print.", 20m, 4, false, null, photos));

    private static async Task<string> RenderAsync(string path, object model) =>
        WebUtility.HtmlDecode(await Renderer.Value.RenderAsync(path, model));
}
