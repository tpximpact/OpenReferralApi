using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Endpoints;

/// <summary>
/// Detailed performance breakdown for HTTP request/response timing analysis
/// </summary>
public class EndpointPerformanceMetrics
{
    /// <summary>
    /// Time spent resolving the domain name to an IP address
    /// High values may indicate DNS issues or slow DNS servers
    /// </summary>
    [JsonPropertyName("dnsLookup")]
    public TimeSpan DnsLookup { get; set; }

    /// <summary>
    /// Time spent establishing the TCP connection to the server
    /// High values may indicate network latency or connectivity issues
    /// </summary>
    [JsonPropertyName("tcpConnection")]
    public TimeSpan TcpConnection { get; set; }

    /// <summary>
    /// Time spent on TLS/SSL handshake for HTTPS connections
    /// High values may indicate SSL configuration issues or certificate problems
    /// </summary>
    [JsonPropertyName("tlsHandshake")]
    public TimeSpan TlsHandshake { get; set; }

    /// <summary>
    /// Time spent by the server processing the request and generating the response
    /// High values indicate server-side performance bottlenecks
    /// </summary>
    [JsonPropertyName("serverProcessing")]
    public TimeSpan ServerProcessing { get; set; }

    /// <summary>
    /// Time spent transferring the response content from server to client
    /// High values may indicate large response sizes or bandwidth limitations
    /// </summary>
    [JsonPropertyName("contentTransfer")]
    public TimeSpan ContentTransfer { get; set; }

}
