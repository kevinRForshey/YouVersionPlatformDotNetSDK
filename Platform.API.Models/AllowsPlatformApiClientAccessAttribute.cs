namespace Platform.API.Models;

/// <summary>
/// Assembly-level marker that exempts the carrying assembly from the YVP0001 analyzer rule
/// forbidding direct references to <c>Platform.API.Clients</c>/<c>Platform.API.OAuth</c>/
/// <c>Platform.API.Exceptions</c> types.
/// </summary>
/// <remarks>
/// Only <c>Platform.API</c> and <c>Platform.SDK.Services</c> should carry this attribute — they
/// are the layers the architecture allows to see those types directly. See
/// <c>docs/adr/0001-layered-architecture-with-enforced-boundaries.md</c> for the boundary this
/// enforces and <c>docs/adr/0006-roslyn-analyzer-for-api-client-boundary.md</c> for why this
/// exemption mechanism exists.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class AllowsPlatformApiClientAccessAttribute : Attribute;
