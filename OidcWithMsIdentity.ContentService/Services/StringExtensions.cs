// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System;

public static class StringExtensions
{
    /// <summary>
    /// 转换为驼峰命名法
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}
