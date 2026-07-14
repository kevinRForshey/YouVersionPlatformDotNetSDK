using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace PlatformTestApp.Tests.Fakes;

internal sealed class FakeSessionFeature : ISessionFeature
{
    public FakeSessionFeature(ISession session) => Session = session;

    public ISession Session { get; set; }
}
