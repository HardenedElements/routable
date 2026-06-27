using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Routable.Kestrel;

public static class IEndpointRouteBuilderExtensions
{
	/// <summary>
	/// Map routable as an endpoint that participates in ASP.NET Core endpoint routing.
	/// Routable keeps its own request matching; this simply exposes it as an endpoint so it can
	/// carry endpoint metadata (authorization, CORS, etc.) and be ordered by the routing middleware.
	/// </summary>
	/// <param name="this">The endpoint route builder to map routable onto.</param>
	/// <param name="pattern">The route pattern selecting requests handed to routable (e.g. "/{**slug}" for everything, or "/api/{**slug}" for a prefix).</param>
	/// <param name="optionsSetter">An action that builds routable configuration.</param>
	/// <returns>A convention builder for the mapped endpoint, allowing metadata such as RequireAuthorization() or RequireCors() to be attached.</returns>
	public static IEndpointConventionBuilder MapRoutable(this IEndpointRouteBuilder @this, string pattern, Action<RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>> optionsSetter)
	{
		if(@this == null) {
			throw new ArgumentNullException(nameof(@this));
		}
		if(pattern == null) {
			throw new ArgumentNullException(nameof(pattern));
		}

		// set options.
		var options = new KestrelRoutableOptions(@this.ServiceProvider);
		optionsSetter?.Invoke(options);

		// when the endpoint is selected, run routes.
		return @this.Map(pattern, async context => {
			var routableContext = new KestrelRoutableContext(options, context);
			await options.Invoke(routableContext);
		});
	}

	/// <summary>
	/// Map routable as a fallback endpoint that participates in ASP.NET Core endpoint routing.
	/// The endpoint is selected only when no other endpoint matches the request, after which routable
	/// performs its own request matching. Routable keeps its own request matching; this exposes it as an
	/// endpoint so it can carry endpoint metadata (authorization, CORS, etc.).
	/// </summary>
	/// <param name="this">The endpoint route builder to map routable onto.</param>
	/// <param name="optionsSetter">An action that builds routable configuration.</param>
	/// <returns>A convention builder for the mapped endpoint, allowing metadata such as RequireAuthorization() or RequireCors() to be attached.</returns>
	public static IEndpointConventionBuilder MapRoutable(this IEndpointRouteBuilder @this, Action<RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>> optionsSetter)
	{
		if(@this == null) {
			throw new ArgumentNullException(nameof(@this));
		}

		// set options.
		var options = new KestrelRoutableOptions(@this.ServiceProvider);
		optionsSetter?.Invoke(options);

		// when no other endpoint matches, run routes.
		return @this.MapFallback(async context => {
			var routableContext = new KestrelRoutableContext(options, context);
			await options.Invoke(routableContext);
		});
	}
}

