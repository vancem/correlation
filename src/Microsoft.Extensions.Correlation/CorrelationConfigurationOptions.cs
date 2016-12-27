// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Extensions.Correlation
{
    public class CorrelationConfigurationOptions
    {
        public CorrelationConfigurationOptions()
        {
            InstrumentOutgoingRequests = true;
        }

        public bool InstrumentOutgoingRequests { get; set; }
    }
}

