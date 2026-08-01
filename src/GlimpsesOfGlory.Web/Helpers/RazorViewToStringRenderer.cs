using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace GlimpsesOfGlory.Web.Helpers;

// Renders a .cshtml view (not a routable Razor Page) to an HTML string outside of an
// HTTP request, using ASP.NET Core's built-in Razor view engine - no third-party
// templating library. Used for email bodies.
public sealed class RazorViewToStringRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider)
{
    public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException(
                $"Could not find Razor view '{viewPath}'. Searched: {string.Join(", ", viewResult.SearchedLocations)}");
        }

        await using var output = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(
            new EmptyModelMetadataProvider(),
            new ModelStateDictionary())
        {
            Model = model,
        };

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            output,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return output.ToString();
    }
}
