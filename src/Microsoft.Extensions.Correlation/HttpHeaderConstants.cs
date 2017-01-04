// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.Correlation
{
    public class HttpHeaderConstants
    {
        public const string CorrelationIdHeaderName = "x-ms-correlation-id";
        public const string ActivityIdHeaderName = "x-ms-request-id";
        public const string BaggageHeaderPrefix = "x-baggage-";
    }
}

