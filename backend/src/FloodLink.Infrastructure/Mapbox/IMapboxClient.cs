namespace FloodLink.Infrastructure.Mapbox;

/// <summary>
/// Abstracts the Mapbox Directions API call so the Route/ETA Agent can be
/// tested without a live network connection. Implemented by <see cref="MapboxClient"/>.
/// </summary>
public interface IMapboxClient
{
    /// <summary>
    /// Calls the Mapbox Directions API and returns distance, ETA, and encoded polyline
    /// for the route from <paramref name="originLng"/>,<paramref name="originLat"/>
    /// to <paramref name="destLng"/>,<paramref name="destLat"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="MapboxRouteResult"/> on success, or <c>null</c> if the API
    /// returned no usable route (e.g. invalid coordinates, no road route available).
    /// </returns>
    /// <exception cref="HttpRequestException">
    /// Thrown on network-level failures or non-transient HTTP errors — callers
    /// must catch and convert to <c>AgentResult.Fail</c>.
    /// </exception>
    /// <exception cref="TaskCanceledException">
    /// Thrown when the request exceeds the configured timeout — callers must
    /// catch and convert to <c>AgentResult.Fail</c>.
    /// </exception>
    Task<MapboxRouteResult?> GetRouteAsync(
        double originLng, double originLat,
        double destLng, double destLat,
        CancellationToken cancellationToken = default);
}
