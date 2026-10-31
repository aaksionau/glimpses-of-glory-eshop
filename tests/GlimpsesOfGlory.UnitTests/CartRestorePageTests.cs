using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Pages.Cart;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlimpsesOfGlory.UnitTests;

public class CartRestorePageTests
{
    [Fact]
    public async Task Restore_SavesThePaymentIntentId_AndRedirectsToTheCart()
    {
        var (page, sessionStore) = CreatePage(CartRestoreResult.Restored("pi_restored", skippedLineCount: 0));

        var result = await page.OnGetAsync("token", CancellationToken.None);

        Assert.Equal("/Cart/Index", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal("pi_restored", sessionStore.GetPaymentIntentId());
        Assert.Null(page.Error);
    }

    [Fact]
    public async Task Restore_WithSkippedLines_SaysSomeItemsAreGone()
    {
        var (page, _) = CreatePage(CartRestoreResult.Restored("pi_restored", skippedLineCount: 1));

        await page.OnGetAsync("token", CancellationToken.None);

        Assert.Equal("Some items from your previous cart are no longer available.", page.Error);
    }

    [Fact]
    public async Task Restore_ExpiredLink_SaysSo_AndLeavesTheCheckoutSessionAlone()
    {
        var (page, sessionStore) = CreatePage(CartRestoreResult.Expired());
        sessionStore.SavePaymentIntentId("pi_current");

        var result = await page.OnGetAsync("token", CancellationToken.None);

        Assert.Equal("/Cart/Index", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal("That cart link has expired; your current cart is below.", page.Error);
        Assert.Equal("pi_current", sessionStore.GetPaymentIntentId());
    }

    [Fact]
    public async Task Restore_NeverTouchesTheSavedAddress()
    {
        var (page, sessionStore) = CreatePage(CartRestoreResult.Restored("pi_restored", skippedLineCount: 0));

        await page.OnGetAsync("token", CancellationToken.None);

        Assert.Null(sessionStore.GetAddress());
    }

    private static (RestoreModel Page, CheckoutSessionStore SessionStore) CreatePage(CartRestoreResult result)
    {
        var httpContext = new DefaultHttpContext { Session = new InMemorySession() };
        var sessionStore = new CheckoutSessionStore(new HttpContextAccessor { HttpContext = httpContext });
        var page = new RestoreModel(new StubCartRestoreService(result), sessionStore)
        {
            PageContext = new PageContext { HttpContext = httpContext },
        };
        return (page, sessionStore);
    }

    private sealed class StubCartRestoreService(CartRestoreResult result) : ICartRestoreService
    {
        public Task<CartRestoreResult> RestoreAsync(string token, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
