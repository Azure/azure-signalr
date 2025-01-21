// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using Azure.Core;

namespace Microsoft.Azure.SignalR;

#nullable enable

public class ServiceEndpoint
{
    private readonly Uri _serviceEndpoint;

    private readonly Uri? _serverEndpoint;

    private readonly Uri? _clientEndpoint;

    private readonly TokenCredential? _tokenCredential;

    private readonly object _lock = new object();

    private volatile IAccessKey? _accessKey;

    public string? ConnectionString { get; }

    public EndpointType EndpointType { get; } = EndpointType.Primary;

    public virtual string Name { get; internal set; } = "";

    /// <summary>
    /// Gets or initializes the custom endpoint for SignalR server to connect to SignalR service.
    /// </summary>
    public Uri ServerEndpoint
    {
        get => _serverEndpoint ?? _serviceEndpoint; init => _serverEndpoint = CheckScheme(value);
    }

    /// <summary>
    /// Gets or initializes the custom endpoint for SignalR clients to connect to SignalR service.
    /// </summary>
    public Uri ClientEndpoint
    {
        get => _clientEndpoint ?? _serviceEndpoint; init => _clientEndpoint = CheckScheme(value);
    }

    /// <summary>
    /// When current app server instance has server connections connected to the target endpoint for current hub, it can deliver messages to that endpoint.
    /// The endpoint is then considered as *Online*; otherwise, *Offline*.
    /// Messages are not able to be delivered to an *Offline* endpoint.
    /// </summary>
    public bool Online { get; internal set; } = true;

    /// <summary>
    /// When the target endpoint has hub clients connected, the endpoint is considered as an *Active* endpoint.
    /// When the target endpoint has no hub clients connected for 10 minutes, the endpoint is considered as an *Inactive* one.
    /// User can choose to not send messages to an *Inactive* endpoint to save network traffic.
    /// But please note that as the *Active* status is reported to the server from remote service, there can be some delay when status changes.
    /// Don't rely on this status if you don't expect any message lose once a client is connected.
    /// </summary>
    public bool IsActive { get; internal set; } = true;

    /// <summary>
    /// Enriched endpoint metrics for customized routing.
    /// </summary>
    public EndpointMetrics EndpointMetrics { get; internal set; } = new EndpointMetrics();

    public string Endpoint { get; }

    internal string AudienceBaseUrl { get; }

    internal IAccessKey AccessKey
    {
        get
        {
            if (_accessKey is null)
            {
                if (_tokenCredential is null)
                {
                    throw new ArgumentNullException(nameof(_tokenCredential));
                }
                lock (_lock)
                {
                    _accessKey ??= new MicrosoftEntraAccessKey(ServerEndpoint, _tokenCredential);
                }
            }
            return _accessKey;
        }
    }

    // Flag to indicate an updaing endpoint needs staging
    internal virtual bool PendingReload { get; set; }

    /// <summary>
    /// Connection string constructor with nameWithEndpointType
    /// </summary>
    /// <param name="nameWithEndpointType"></param>
    /// <param name="connectionString"></param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ServiceEndpoint(string nameWithEndpointType, string connectionString) : this(connectionString)
    {
        (Name, EndpointType) = Parse(nameWithEndpointType);
    }

    private static IAccessKey BuildAccessKey(ParsedConnectionString parsed)
    {
        return string.IsNullOrEmpty(parsed.AccessKey)
            ? new MicrosoftEntraAccessKey(parsed.ServerEndpoint ?? parsed.Endpoint, parsed.TokenCredential)
            : new AccessKey(parsed.AccessKey);
    }

    /// <summary>
    /// Connection string constructor
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="type"></param>
    /// <param name="name"></param>
    public ServiceEndpoint(string connectionString, EndpointType type = EndpointType.Primary, string name = "")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace.", nameof(connectionString));
        }
        ConnectionString = connectionString;
        EndpointType = type;
        Name = name;

        var result = ConnectionStringParser.Parse(connectionString);

        _accessKey = BuildAccessKey(result);
        _serviceEndpoint = result.Endpoint;
        _clientEndpoint = result.ClientEndpoint;
        _serverEndpoint = result.ServerEndpoint;

        Endpoint = BuildEndpointString(_serviceEndpoint);
        AudienceBaseUrl = BuildAudienceBaseUrlEndWithSlash(_serviceEndpoint);
    }

    /// <summary>
    /// Azure active directory constructor with nameWithEndpointType
    /// </summary>
    /// <param name="nameWithEndpointType"></param>
    /// <param name="endpoint"></param>
    /// <param name="credential"></param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ServiceEndpoint(string nameWithEndpointType, Uri endpoint, TokenCredential credential) : this(endpoint, credential)
    {
        (Name, EndpointType) = Parse(nameWithEndpointType);
    }

    /// <summary>
    /// Azure active directory constructor
    /// </summary>
    /// <param name="endpoint">SignalR Service endpoint.</param>
    /// <param name="credential">The Azure Active Directory credential.</param>
    /// <param name="endpointType">The endpoint type.</param>
    /// <param name="name">The endpoint name.</param>
    /// <param name="serverEndpoint">The endpoint for servers to connect to Azure SignalR.</param>
    /// <param name="clientEndpoint">The endpoint for clients to connect to Azure SignalR.</param>
    public ServiceEndpoint(Uri endpoint,
                           TokenCredential credential,
                           EndpointType endpointType = EndpointType.Primary,
                           string name = "",
                           Uri? serverEndpoint = null,
                           Uri? clientEndpoint = null)
    {
        _tokenCredential = credential ?? throw new ArgumentNullException(nameof(credential));

        EndpointType = endpointType;
        Name = name;

        _serviceEndpoint = CheckScheme(endpoint);
        _serverEndpoint = serverEndpoint == null ? serverEndpoint : CheckScheme(serverEndpoint);
        _clientEndpoint = clientEndpoint == null ? clientEndpoint : CheckScheme(clientEndpoint);

        AudienceBaseUrl = BuildAudienceBaseUrlEndWithSlash(_serviceEndpoint);
        Endpoint = BuildEndpointString(_serviceEndpoint);
    }

    /// <summary>
    /// Copy constructor with no exception
    /// </summary>
    /// <param name="other"></param>
    public ServiceEndpoint(ServiceEndpoint other)
    {
        ConnectionString = other.ConnectionString;
        EndpointType = other.EndpointType;
        Name = other.Name;
        Endpoint = other.Endpoint;
        AudienceBaseUrl = other.AudienceBaseUrl;

        _accessKey = other._accessKey;
        _tokenCredential = other._tokenCredential;
        _serviceEndpoint = other._serviceEndpoint;
        _clientEndpoint = other._clientEndpoint;
        _serverEndpoint = other._serverEndpoint;
    }

    public override string ToString()
    {
        var prefix = string.IsNullOrEmpty(Name) ? string.Empty : $"[{Name}]";
        var suffix = ClientEndpoint == _serviceEndpoint ? string.Empty : $";ClientEndpoint={ClientEndpoint}";
        suffix += ServerEndpoint == _serviceEndpoint ? string.Empty : $";ServerEndpoint={ServerEndpoint}";
        return $"{prefix}({EndpointType}){Endpoint}{suffix}";
    }

    public override int GetHashCode()
    {
        // We consider ServiceEndpoint with the same Endpoint (https://{signalr.endpoint}) as the unique identity
        return (Endpoint, EndpointType, Name, ClientEndpoint, ServerEndpoint).GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (!(obj is ServiceEndpoint that))
        {
            return false;
        }

        return (Name, Endpoint, EndpointType, ClientEndpoint, ServerEndpoint) == (that.Name, that.Endpoint, that.EndpointType, that.ClientEndpoint, that.ServerEndpoint);
    }

    internal static string BuildEndpointString(Uri uri)
    {
        return new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}").AbsoluteUri.TrimEnd('/');
    }

    private static string BuildAudienceBaseUrlEndWithSlash(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Host}/";
    }

    private static (string, EndpointType) Parse(string nameWithEndpointType)
    {
        if (string.IsNullOrEmpty(nameWithEndpointType))
        {
            return (string.Empty, EndpointType.Primary);
        }

        var parts = nameWithEndpointType.Split(':');
        if (parts.Length == 1)
        {
            return (parts[0], EndpointType.Primary);
        }
        else if (Enum.TryParse<EndpointType>(parts[1], true, out var endpointStatus))
        {
            return (parts[0], endpointStatus);
        }
        else
        {
            return (nameWithEndpointType, EndpointType.Primary);
        }
    }

    private static Uri CheckScheme(Uri uri)
    {
        return uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps
            ? throw new ArgumentException("Endpoint scheme must be 'http://' or 'https://'")
            : uri;
    }
}
