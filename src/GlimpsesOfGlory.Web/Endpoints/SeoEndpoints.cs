using System.Xml.Linq;
using GlimpsesOfGlory.Abstractions.Services;

namespace GlimpsesOfGlory.Web.Endpoints;

// /robots.txt and /sitemap.xml, built from the request's own scheme and host so they are
// correct behind any domain the app is served on.
internal static class SeoEndpoints
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", GetRobotsTxt);
        endpoints.MapGet("/sitemap.xml", GetSitemapAsync);
        return endpoints;
    }

    private static IResult GetRobotsTxt(HttpRequest request)
    {
        var baseUrl = BaseUrl(request);
        var content = $"""
            User-agent: *
            Allow: /
            Disallow: /admin
            Disallow: /cart
            Disallow: /checkout

            Sitemap: {baseUrl}/sitemap.xml
            """;
        return Results.Text(content, "text/plain");
    }

    private static async Task<IResult> GetSitemapAsync(
        HttpRequest request,
        IProductCatalogService productCatalogService,
        CancellationToken cancellationToken)
    {
        var baseUrl = BaseUrl(request);
        var products = await productCatalogService.GetProductsAsync(cancellationToken);

        var urls = new List<string> { $"{baseUrl}/", $"{baseUrl}/products" };
        urls.AddRange(products.Select(p => $"{baseUrl}/products/{p.Slug}"));

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNamespace + "urlset", urls.Select(url => new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", url)))));

        return Results.Text(document.ToString(), "application/xml");
    }

    private static string BaseUrl(HttpRequest request) => $"{request.Scheme}://{request.Host}";
}
