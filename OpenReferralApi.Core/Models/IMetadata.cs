using System;

namespace OpenReferralApi.Core.Models;

/// <summary>
/// Common interface for metadata objects
/// </summary>
public interface IMetadata
{
    DateTime Timestamp { get; set; }
}
