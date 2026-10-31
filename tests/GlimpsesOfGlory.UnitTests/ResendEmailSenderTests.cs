using System.Net;
using System.Text.Json;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Email;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.UnitTests;

public class ResendEmailSenderTests
{
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private static ResendEmailSender CreateSender(CapturingHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            RazorViewToStringRendererTests.CreateRenderer(),
            Options.Create(new ResendOptions { ApiKey = "re_test", FromAddress = "shop@example.com" }));

    private static AbandonedCheckoutReminderView Reminder() =>
        new("shopper@example.com", [new AbandonedCheckoutLine("Ceramic Mug", 2)], "https://shop.example/cart/restore/abc");

    [Fact]
    public async Task SendAbandonedCheckoutReminderAsync_PostsRenderedEmailToResend()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);

        await CreateSender(handler).SendAbandonedCheckoutReminderAsync(Reminder(), CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal(new Uri("https://api.resend.com/emails"), handler.Request.RequestUri);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("re_test", handler.Request.Headers.Authorization?.Parameter);

        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("shop@example.com", json.RootElement.GetProperty("from").GetString());
        Assert.Equal("shopper@example.com", json.RootElement.GetProperty("to")[0].GetString());
        Assert.Contains("Ceramic Mug", json.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAbandonedCheckoutReminderAsync_Throws_WhenResendRejectsTheEmail()
    {
        var handler = new CapturingHandler(HttpStatusCode.UnprocessableEntity);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateSender(handler).SendAbandonedCheckoutReminderAsync(Reminder(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
    }
}
