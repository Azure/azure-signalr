// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR.Controllers.Common
{
    internal static class JsonObjectConverter
    {
        public static object[] ConvertToObjectArray(object[] array)
        {
            if (array?.Length > 0)
            {
                var ret = new object[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    ret[i] = ConvertToObject(array[i]);
                }

                return ret;
            }

            return array;
        }

        public static object ConvertToObject(object value)
        {
            // Recursion guard to prevent stackoverflow
            RuntimeHelpers.EnsureSufficientExecutionStack();

            switch (value)
            {
                case JObject jObject:
                    return ConvertJObjectToObject(jObject);
                case JArray jArray:
                    return ConvertJArrayToObject(jArray);
                case JValue jValue:
                    return jValue.ToObject<object>();
                default:
                    return value;
            }
        }

        private static object ConvertJObjectToObject(JObject jObject)
        {
            var dictionary = new Dictionary<string, object>();

            foreach (var kvp in jObject)
            {
                dictionary.Add(kvp.Key, ConvertToObject(kvp.Value));
            }

            return dictionary;
        }

        private static object ConvertJArrayToObject(JArray jArray)
        {
            if (jArray.Count == 0) return Array.Empty<object>();

            var array = new object[jArray.Count];

            for (int i = 0; i < jArray.Count; i++)
            {
                array[i] = ConvertToObject(jArray[i]);
            }

            return array;
        }
    }
}
