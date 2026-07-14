using Microsoft.AspNetCore.Http;

namespace PlatformTestApp.Tests.Fakes;

internal sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
