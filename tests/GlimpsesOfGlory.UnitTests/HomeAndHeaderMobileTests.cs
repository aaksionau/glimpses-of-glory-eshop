using System.Net;
using System.Text.RegularExpressions;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Helpers;

namespace GlimpsesOfGlory.UnitTests;

// Home page and header on mobile (#103): featured products straight under the hero, 44px header
// tap targets with the current section highlighted, lazy images with reserved dimensions, and one
// product grid shared by the home page and All products.
public class HomeAndHeaderMobileTests
{
    private const string GridPath = "~/Pages/Shared/_ProductGrid.cshtml";
    private const string GalleryPath = "~/Pages/Products/_ProductGallery.cshtml";

    [Theory]
    [InlineData("/", StorefrontSection.Home)]
    [InlineData("", StorefrontSection.Home)]
    [InlineData(null, StorefrontSection.Home)]
    [InlineData("/Index", StorefrontSection.Home)]
    [InlineData("/products", StorefrontSection.Shop)]
    [InlineData("/Products/", StorefrontSection.Shop)]
    [InlineData("/products/sunrise", StorefrontSection.Shop)]
    [InlineData("/cart", StorefrontSection.Cart)]
    [InlineData("/checkout", StorefrontSection.None)]
    [InlineData("/privacy", StorefrontSection.None)]
    [InlineData("/index/other", StorefrontSection.None)]
    public void FromPath_MapsRequestPathToHeaderSection(string? path, StorefrontSection expected)
    {
        Assert.Equal(expected, StorefrontSections.FromPath(path));
    }

    [Theory]
    [InlineData(StorefrontSection.Home, "/", "page")]
    [InlineData(StorefrontSection.Shop, "/products", "page")]
    [InlineData(StorefrontSection.Shop, "/Products/Index", "page")]
    [InlineData(StorefrontSection.Shop, "/products/sunrise", "true")]
    [InlineData(StorefrontSection.Cart, "/cart", "page")]
    [InlineData(StorefrontSection.Shop, "/cart", null)]
    [InlineData(StorefrontSection.Home, "/checkout", null)]
    [InlineData(StorefrontSection.None, "/privacy", null)]
    public void AriaCurrent_IsPageOnTheSectionPage_AndTrueInsideTheSection(StorefrontSection link, string path, string? expected)
    {
        Assert.Equal(expected, StorefrontSections.AriaCurrent(link, path));
    }

    [Fact]
    public void AllProducts_LoadsTheFirstDesktopRowEagerly()
    {
        Assert.Contains("ViewData[\"EagerImageCount\"] = 4;", ReadPage("Products/Index.cshtml"));
        Assert.DoesNotContain("EagerImageCount", ReadPage("Index.cshtml"));
    }

    [Fact]
    public void Home_PutsFeaturedPiecesRightAfterTheHero()
    {
        var home = ReadPage("Index.cshtml");
        var hero = home.IndexOf("Shop the Collection", StringComparison.Ordinal);
        var featured = home.IndexOf("data-testid=\"featured\"", StringComparison.Ordinal);
        var about = home.IndexOf("id=\"about\"", StringComparison.Ordinal);

        Assert.True(hero >= 0 && featured >= 0 && about >= 0);
        Assert.True(hero < featured && featured < about, "Featured pieces should sit between the hero and About.");
        // Nothing but the end of the hero sits between it and Featured.
        Assert.Single(Regex.Matches(home[..home.LastIndexOf("<section", featured, StringComparison.Ordinal)], "<section"));
    }

    [Fact]
    public void Home_AboutParagraph_HasNoHardLineBreak()
    {
        Assert.DoesNotMatch("<br\\s*/?>", ReadPage("Index.cshtml"));
    }

    [Theory]
    [InlineData("Index.cshtml", "Model.FeaturedProducts")]
    [InlineData("Products/Index.cshtml", "Model.Products")]
    public void HomeAndAllProducts_UseTheSharedGrid(string page, string model)
    {
        var markup = ReadPage(page);

        Assert.Contains($"<partial name=\"_ProductGrid\" model=\"{model}\" />", markup);
        Assert.DoesNotContain("calc(", markup);
        Assert.DoesNotContain("_ProductCard", markup);
    }

    [Fact]
    public async Task Grid_IsTwoColumnsWithGap4OnPhones()
    {
        var html = await RenderGridAsync();
        var classes = Regex.Match(html, "<div class=\"([^\"]*)\" data-testid=\"product-grid\"").Groups[1].Value.Split(' ');

        Assert.Contains("grid", classes);
        Assert.Contains("grid-cols-2", classes);
        Assert.Contains("gap-4", classes);
        Assert.Contains("sm:gap-6", classes);
        Assert.DoesNotContain("gap-6", classes);
    }

    [Fact]
    public async Task ProductCardImages_AreLazy_WithReservedDimensions()
    {
        var images = Regex.Matches(await RenderGridAsync(), "<img [^>]*>").Select(m => m.Value).ToList();

        Assert.Equal(2, images.Count);
        Assert.All(images, img =>
        {
            Assert.Contains("loading=\"lazy\"", img);
            Assert.Contains("width=\"600\" height=\"800\"", img);
        });
    }

    [Fact]
    public async Task Gallery_LoadsOnlyTheFirstPhotoUpFront()
    {
        var html = await RenderGalleryAsync("a.jpg", "b.jpg", "c.jpg");
        var main = Regex.Match(html, "<button[^>]*x-ref=\"trigger\".*?</button>", RegexOptions.Singleline).Value;
        var mainImages = Regex.Matches(main, "<img [^>]*>").Select(m => m.Value).ToList();

        Assert.Equal(3, mainImages.Count);
        Assert.All(mainImages, img => Assert.Contains("width=\"600\" height=\"800\"", img));
        Assert.DoesNotContain("loading=", mainImages[0]);
        Assert.Contains("fetchpriority=\"high\"", mainImages[0]);
        Assert.DoesNotContain("display: none", mainImages[0]);
        Assert.All(mainImages.Skip(1), img =>
        {
            Assert.Contains("loading=\"lazy\"", img);
            Assert.Contains("style=\"display: none\"", img);
            Assert.DoesNotContain("fetchpriority", img);
        });
    }

    [Fact]
    public async Task Gallery_ThumbnailsAndLightboxPhotos_AreLazy()
    {
        var html = await RenderGalleryAsync("a.jpg", "b.jpg");
        var thumbnails = Regex.Matches(html, "<img src=\"[^\"]*\" alt=\"\"[^>]*>").Select(m => m.Value).ToList();
        var lightbox = html[html.IndexOf("data-lightbox", StringComparison.Ordinal)..];

        Assert.Equal(2, thumbnails.Count);
        Assert.All(thumbnails, img =>
        {
            Assert.Contains("loading=\"lazy\"", img);
            Assert.Contains("width=\"64\" height=\"64\"", img);
        });
        Assert.All(Regex.Matches(lightbox, "<img [^>]*>"), m => Assert.Contains("loading=\"lazy\"", m.Value));
    }

    [Fact]
    public void Header_HidesHomeOnPhones_AndHasACartIcon()
    {
        var header = Header();

        Assert.Matches("<a asp-area=\"\" asp-page=\"/Index\" class=\"@NavLinkClass\\(StorefrontSection.Home\\) hidden sm:inline-flex\"", header);
        var cart = Regex.Match(header, "<a [^>]*data-testid=\"header-cart\">.*?</a>", RegexOptions.Singleline).Value;
        Assert.Matches("<svg [^>]*aria-hidden=\"true\"", cart);
        // The label stays for screen readers on phones, where only the icon shows.
        Assert.Contains("<span class=\"sr-only sm:not-sr-only\">Cart</span>", cart);
        Assert.Contains("<span id=\"cart-count\">", cart);
    }

    [Fact]
    public void Header_NavLinks_AreTallTapTargets_AndMarkTheCurrentSection()
    {
        var layout = ReadPage("Shared/_Layout.cshtml");
        var navClass = Regex.Match(layout, "NavLinkClass\\(StorefrontSection section\\) =>\\s*\"([^\"]*)\"").Groups[1].Value.Split(' ');

        Assert.Contains("min-h-11", navClass);
        Assert.Contains("px-3", navClass);
        Assert.Contains("StorefrontSections.AriaCurrent(section, path)", layout);

        var links = Regex.Matches(Header(), "<a [^>]*>").Select(m => m.Value).Skip(1).ToList();
        Assert.Equal(4, links.Count);
        Assert.All(links, link => Assert.Contains("class=\"@NavLinkClass(", link));
        Assert.Equal(3, links.Count(link => link.Contains("aria-current=\"@AriaCurrent(StorefrontSection.", StringComparison.Ordinal)));
    }

    private static readonly Lazy<RazorViewToStringRenderer> Renderer =
        new(RazorViewToStringRendererTests.CreateRenderer);

    private static string ReadPage(string page) => File.ReadAllText(PrimaryButtonTests.PagePath(page));

    private static string Header()
    {
        var layout = ReadPage("Shared/_Layout.cshtml");
        var start = layout.IndexOf("<header", StringComparison.Ordinal);
        return layout[start..layout.IndexOf("</header>", start, StringComparison.Ordinal)];
    }

    private static Task<string> RenderGridAsync() =>
        RenderAsync(GridPath, (IReadOnlyList<ProductSummary>)
        [
            new ProductSummary("sunrise", "Sunrise", 20m, "sunrise.jpg", 3, false, null),
            new ProductSummary("harbour", "Harbour", 15m, "harbour.jpg", 0, true, null),
        ]);

    private static Task<string> RenderGalleryAsync(params string[] photos) =>
        RenderAsync(GalleryPath, new ProductDetail("sunrise", "Sunrise", "A print.", 20m, 4, false, null, photos));

    private static async Task<string> RenderAsync(string path, object model) =>
        WebUtility.HtmlDecode(await Renderer.Value.RenderAsync(path, model));
}
