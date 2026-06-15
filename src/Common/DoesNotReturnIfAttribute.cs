// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Polyfill for System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute, which is
// referenced by the vendored MessagePack source but is not available on netstandard2.0.
#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP3_0_OR_GREATER && !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class DoesNotReturnIfAttribute : Attribute
    {
        public DoesNotReturnIfAttribute(bool parameterValue) => this.ParameterValue = parameterValue;

        public bool ParameterValue { get; }
    }
}
#endif
