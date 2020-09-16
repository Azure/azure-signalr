namespace Microsoft.Azure.SignalR.Management.MultiEndpoints
{
    /// <summary>
    /// A builder abstraction for configuring <see cref="IMultiServiceManager"/> instances.
    /// </summary>
    internal interface IMultiServiceManagerBuilder
    {
        /// <summary>
        /// Builds <see cref="IMultiServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IMultiServiceManager"/>.</returns>
        IMultiServiceManager Build();
    }
}
