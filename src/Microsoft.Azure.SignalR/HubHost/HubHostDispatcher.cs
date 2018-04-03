// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Azure.SignalR.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    // TODO.
    // This class has to be refactored since it has a lot of duplication of DefaultHubDispatcher.
    // For example, the Hub invocation logic is totally the same as SignalR.
    // The difference is to decode HubMessageWrapper before dispatching, and
    // completion or error message sending.
    public class HubHostDispatcher<THub> : HubDispatcher<THub> where THub : Hub
    {
        private readonly Dictionary<string, HubMethodDescriptor> _methods =
            new Dictionary<string, HubMethodDescriptor>(StringComparer.OrdinalIgnoreCase);

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubContext<THub> _hubContext;
        private readonly ILogger<HubHostDispatcher<THub>> _logger;
        private readonly HubLifetimeManager<THub> _lifetimeManager;

        public HubHostDispatcher(HubLifetimeManager<THub> lifetimeManager,
            IServiceScopeFactory serviceScopeFactory, IHubContext<THub> hubContext,
            ILogger<HubHostDispatcher<THub>> logger)
        {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;

            DiscoverHubMethods();
        }

        public override async Task OnConnectedAsync(HubConnectionContext connection)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub>>();
                var hub = hubActivator.Create();
                try
                {
                    InitializeHub(hub, connection);
                    await hub.OnConnectedAsync();
                }
                finally
                {
                    hubActivator.Release(hub);
                }
            }
        }

        public override async Task OnDisconnectedAsync(HubConnectionContext connection, Exception exception)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub>>();
                var hub = hubActivator.Create();
                try
                {
                    InitializeHub(hub, connection);
                    await hub.OnDisconnectedAsync(exception);
                }
                finally
                {
                    hubActivator.Release(hub);
                }
            }
        }

        public override async Task DispatchMessageAsync(HubConnectionContext connection, HubMessage hubMessage)
        {
            // The two parameter types are guaranteed by caller
            var cloudConnection = (CloudHubConnectionContext)connection;
            var messageWrapper = (HubInvocationMessageWrapper)hubMessage;

            //var connectionId = messageWrapper.GetConnectionId();
            var messages = new List<HubMessage>();
            cloudConnection.ClientProtocol.TryParseMessages(messageWrapper.ReadPayload(), this, messages);
            foreach (var message in messages)
            {
                switch (message)
                {
                    case InvocationMessage invocationMessage:
                        _logger.LogDebug($"Received invocation: {invocationMessage}");
                        await ProcessInvocation(messageWrapper, cloudConnection, invocationMessage, isStreamedInvocation: false);
                        break;

                    case StreamInvocationMessage streamInvocationMessage:
                        _logger.LogDebug($"Received stream invocation: {streamInvocationMessage}");
                        await ProcessInvocation(messageWrapper, cloudConnection, streamInvocationMessage, isStreamedInvocation: true);
                        break;

                    case CompletionMessage completionMessage:
                        break;

                    case CancelInvocationMessage cancelInvocationMessage:
                        // Check if there is an associated active stream and cancel it if it exists.
                        // The cts will be removed when the streaming method completes executing
                        if (cloudConnection.ActiveRequestCancellationSources.TryGetValue(cancelInvocationMessage.InvocationId, out var cts))
                        {
                            _logger.LogDebug($"Cancel stream invocation: {cancelInvocationMessage.InvocationId}");
                            cts.Cancel();
                        }
                        else
                        {
                            _logger.LogWarning("Unexpected stream invocation cancel.");
                        }
                        break;

                    case PingMessage _:
                        // We don't care about pings
                        break;

                    // Other kind of message we weren't expecting
                    default:
                        _logger.LogError($"Received unsupported message type: {hubMessage.GetType().FullName}");
                        throw new NotSupportedException($"Received unsupported message: {hubMessage}");
                }
            }
        }

        public override IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            return !_methods.TryGetValue(methodName, out var descriptor) ? Type.EmptyTypes : descriptor.ParameterTypes;
        }

        public override Type GetReturnType(string invocationId)
        {
            return typeof(object);
        }

        private async Task ProcessInvocation(HubInvocationMessageWrapper originalHubMessageWrapper,
            CloudHubConnectionContext connection, HubMethodInvocationMessage hubMethodInvocationMessage,
            bool isStreamedInvocation)
        {
            try
            {
                // If an unexpected exception occurs then we want to kill the entire connection
                // by ending the processing loop
                if (!_methods.TryGetValue(hubMethodInvocationMessage.Target, out var descriptor))
                {
                    // Send an error to the client. Then let the normal completion process occur
                    _logger.LogWarning($"Unknown hub method: {hubMethodInvocationMessage.Target}");

                    await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection,
                        $"Unknown hub method '{hubMethodInvocationMessage.Target}'");
                }
                else
                {
                    await Invoke(descriptor, connection, hubMethodInvocationMessage, originalHubMessageWrapper, isStreamedInvocation);
                }
            }
            catch (Exception)
            {
                // Abort the entire connection if the invocation fails in an unexpected way
                //connection.Abort(ex);
                connection.Abort();
            }
        }

        private async Task Invoke(HubMethodDescriptor descriptor,
            CloudHubConnectionContext connection,
            HubMethodInvocationMessage hubMethodInvocationMessage,
            HubInvocationMessageWrapper originalHubMessageWrapper,
            bool isStreamedInvocation)
        {
            var methodExecutor = descriptor.MethodExecutor;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                if (!await IsHubMethodAuthorized(scope.ServiceProvider, connection.User, descriptor.Policies))
                {
                    _logger.LogError($"Hub method unauthorized: {hubMethodInvocationMessage.Target}");
                    await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection,
                        $"Failed to invoke '{hubMethodInvocationMessage.Target}' because user is unauthorized");
                    return;
                }

                if (!await ValidateInvocationMode(methodExecutor.MethodReturnType, isStreamedInvocation, originalHubMessageWrapper, hubMethodInvocationMessage, connection))
                {
                    return;
                }

                var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub>>();
                var hub = hubActivator.Create();

                try
                {
                    InitializeHub(hub, connection);

                    var result = await ExecuteHubMethod(methodExecutor, hub, hubMethodInvocationMessage.Arguments);

                    if (isStreamedInvocation)
                    {
                        var enumerator = GetStreamingEnumerator(connection, hubMethodInvocationMessage.InvocationId, methodExecutor, result, methodExecutor.MethodReturnType);
                        //Log.StreamingResult(_logger, hubMethodInvocationMessage.InvocationId, methodExecutor);
                        await StreamResultsAsync(hubMethodInvocationMessage.InvocationId, connection, enumerator, originalHubMessageWrapper);
                    }
                    // Non-empty/null InvocationId ==> Blocking invocation that needs a response
                    else if (!string.IsNullOrEmpty(hubMethodInvocationMessage.InvocationId))
                    {
                        //Log.SendingResult(_logger, hubMethodInvocationMessage.InvocationId, methodExecutor);
                        await SendHubMessage(connection, CompletionMessage
                            .WithResult(hubMethodInvocationMessage.InvocationId, result)
                            .AddHeaders(hubMethodInvocationMessage.Headers), // Pass information to CompletionMessage
                            originalHubMessageWrapper);
                    }
                }
                catch (TargetInvocationException ex)
                {
                    //Log.FailedInvokingHubMethod(_logger, hubMethodInvocationMessage.Target, ex);
                    await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection, ex.InnerException.Message);
                }
                catch (Exception ex)
                {
                    //Log.FailedInvokingHubMethod(_logger, hubMethodInvocationMessage.Target, ex);
                    await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection, ex.Message);
                }
                finally
                {
                    hubActivator.Release(hub);
                }
            }
        }

        private async Task StreamResultsAsync(string invocationId, CloudHubConnectionContext connection,
            IAsyncEnumerator<object> enumerator, HubInvocationMessageWrapper originalHubMessageWrapper)
        {
            string error = null;

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    // Send the stream item
                    await SendHubMessage(connection, new StreamItemMessage(invocationId, enumerator.Current), originalHubMessageWrapper);
                }
            }
            catch (ChannelClosedException ex)
            {
                // If the channel closes from an exception in the streaming method, grab the innerException for the error from the streaming method
                error = ex.InnerException == null ? ex.Message : ex.InnerException.Message;
            }
            catch (Exception ex)
            {
                // If the streaming method was canceled we don't want to send a HubException message - this is not an error case
                if (!(ex is OperationCanceledException && connection.ActiveRequestCancellationSources.TryGetValue(invocationId, out var cts)
                    && cts.IsCancellationRequested))
                {
                    error = ex.Message;
                }
            }
            finally
            {
                await SendHubMessage(connection, new CompletionMessage(invocationId, error: error, result: null, hasResult: false),
                    originalHubMessageWrapper);
                if (connection.ActiveRequestCancellationSources.TryRemove(invocationId, out var cts))
                {
                    cts.Dispose();
                }
            }
        }

        private static async Task<object> ExecuteHubMethod(ObjectMethodExecutor methodExecutor, THub hub, object[] arguments)
        {
            // ReadableChannel is awaitable but we don't want to await it.
            if (methodExecutor.IsMethodAsync && !IsChannel(methodExecutor.MethodReturnType, out _))
            {
                if (methodExecutor.MethodReturnType == typeof(Task))
                {
                    await (Task)methodExecutor.Execute(hub, arguments);
                }
                else
                {
                    return await methodExecutor.ExecuteAsync(hub, arguments);
                }
            }
            else
            {
                return methodExecutor.Execute(hub, arguments);
            }

            return null;
        }

        private async Task SendInvocationError(
            HubInvocationMessageWrapper originalHubMessageWrapper,
            HubMethodInvocationMessage hubMethodInvocationMessage,
            CloudHubConnectionContext connection,
            string errorMessage)
        {
            if (string.IsNullOrEmpty(hubMethodInvocationMessage.InvocationId))
            {
                return;
            }
            await SendHubMessage(connection, CompletionMessage
                .WithError(hubMethodInvocationMessage.InvocationId, errorMessage),
                originalHubMessageWrapper);
        }

        private void InitializeHub(THub hub, HubConnectionContext connection)
        {
            hub.Clients = new HubCallerClients(new CloudHubClients<THub>(_lifetimeManager as HubHostLifetimeManager<THub>,
                connection.ConnectionId), connection.ConnectionId);
            hub.Context = new DefaultHubCallerContext(connection);
            hub.Groups = _hubContext.Groups;
        }

        private static bool IsChannel(Type type, out Type payloadType)
        {
            var channelType = type.AllBaseTypes().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ChannelReader<>));
            if (channelType == null)
            {
                payloadType = null;
                return false;
            }
            else
            {
                payloadType = channelType.GetGenericArguments()[0];
                return true;
            }
        }

        private async Task<bool> IsHubMethodAuthorized(IServiceProvider provider, ClaimsPrincipal principal, IList<IAuthorizeData> policies)
        {
            // If there are no policies we don't need to run auth
            if (!policies.Any())
            {
                return true;
            }

            var authService = provider.GetRequiredService<IAuthorizationService>();
            var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

            var authorizePolicy = await AuthorizationPolicy.CombineAsync(policyProvider, policies);
            // AuthorizationPolicy.CombineAsync only returns null if there are no policies and we check that above
            Debug.Assert(authorizePolicy != null);

            var authorizationResult = await authService.AuthorizeAsync(principal, authorizePolicy);
            // Only check authorization success, challenge or forbid wouldn't make sense from a hub method invocation
            return authorizationResult.Succeeded;
        }

        private async Task<bool> ValidateInvocationMode(Type resultType, bool isStreamedInvocation,
            HubInvocationMessageWrapper originalHubMessageWrapper, HubMethodInvocationMessage hubMethodInvocationMessage,
            CloudHubConnectionContext connection)
        {
            var isStreamedResult = IsStreamed(resultType);
            if (isStreamedResult && !isStreamedInvocation)
            {
                // Non-null/empty InvocationId? Blocking
                if (!string.IsNullOrEmpty(hubMethodInvocationMessage.InvocationId))
                {
                    //Log.StreamingMethodCalledWithInvoke(_logger, hubMethodInvocationMessage);
                    await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection,
                        $"The client attempted to invoke the streaming '{hubMethodInvocationMessage.Target}' method in a non-streaming fashion.");
                }

                return false;
            }

            if (!isStreamedResult && isStreamedInvocation)
            {
                //Log.NonStreamingMethodCalledWithStream(_logger, hubMethodInvocationMessage);
                await SendInvocationError(originalHubMessageWrapper, hubMethodInvocationMessage, connection,
                    $"The client attempted to invoke the non-streaming '{hubMethodInvocationMessage.Target}' method in a streaming fashion.");

                return false;
            }

            return true;
        }

        private static Task SendHubMessage(CloudHubConnectionContext connection,
            HubMessage hubMessage, HubInvocationMessageWrapper originalHubMessageWrapper)
        {
            var meta = new MessageMetaDataDictionary();
            meta.AddConnectionId(originalHubMessageWrapper.GetConnectionId());
            return connection.SendHubMessage(hubMessage, meta);
        }

        private static bool IsStreamed(Type resultType)
        {
            var observableInterface = IsIObservable(resultType) ?
                resultType :
                resultType.GetInterfaces().FirstOrDefault(IsIObservable);

            if (observableInterface != null)
            {
                return true;
            }

            if (IsChannel(resultType, out _))
            {
                return true;
            }

            return false;
        }

        private IAsyncEnumerator<object> GetStreamingEnumerator(CloudHubConnectionContext connection,
            string invocationId, ObjectMethodExecutor methodExecutor, object result, Type resultType)
        {
            if (result != null)
            {
                var observableInterface = IsIObservable(resultType) ?
                    resultType :
                    resultType.GetInterfaces().FirstOrDefault(IsIObservable);
                if (observableInterface != null)
                {
                    return AsyncEnumeratorAdapters.FromObservable(result, observableInterface, CreateCancellation());
                }

                if (IsChannel(resultType, out var payloadType))
                {
                    return AsyncEnumeratorAdapters.FromChannel(result, payloadType, CreateCancellation());
                }
            }

            //Log.InvalidReturnValueFromStreamingMethod(_logger, methodExecutor.MethodInfo.Name);
            throw new InvalidOperationException($"The value returned by the streaming method '{methodExecutor.MethodInfo.Name}' is null, does not implement the IObservable<> interface or is not a ReadableChannel<>.");

            CancellationToken CreateCancellation()
            {
                var streamCts = new CancellationTokenSource();
                connection.ActiveRequestCancellationSources.TryAdd(invocationId, streamCts);
                return CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionAborted, streamCts.Token).Token;
            }
        }

        private static bool IsIObservable(Type iface)
        {
            return iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IObservable<>);
        }

        private void DiscoverHubMethods()
        {
            var hubType = typeof(THub);
            var hubTypeInfo = hubType.GetTypeInfo();
            var hubName = hubType.Name;

            foreach (var methodInfo in HubReflectionHelper.GetHubMethods(hubType))
            {
                var methodName =
                    methodInfo.GetCustomAttribute<HubMethodNameAttribute>()?.Name ??
                    methodInfo.Name;

                if (_methods.ContainsKey(methodName))
                {
                    throw new NotSupportedException($"Duplicate definitions of '{methodName}'. Overloading is not supported.");
                }

                var executor = ObjectMethodExecutor.Create(methodInfo, hubTypeInfo);
                var authorizeAttributes = methodInfo.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
                _methods[methodName] = new HubMethodDescriptor(executor, authorizeAttributes);

                _logger.LogDebug($"'{hubName}' hub method '{methodName}' is bound.");
            }
        }

        // REVIEW: We can decide to move this out of here if we want pluggable hub discovery
        private class HubMethodDescriptor
        {
            public HubMethodDescriptor(ObjectMethodExecutor methodExecutor, IEnumerable<IAuthorizeData> policies)
            {
                MethodExecutor = methodExecutor;
                ParameterTypes = methodExecutor.MethodParameters.Select(p => p.ParameterType).ToArray();
                Policies = policies.ToArray();
            }

            public ObjectMethodExecutor MethodExecutor { get; }

            public IReadOnlyList<Type> ParameterTypes { get; }

            public IList<IAuthorizeData> Policies { get; }
        }
    }
}
