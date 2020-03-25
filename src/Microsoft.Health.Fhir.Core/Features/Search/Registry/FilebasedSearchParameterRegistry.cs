// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Models;
using Newtonsoft.Json;

namespace Microsoft.Health.Fhir.Core.Features.Search.Registry
{
    public class FilebasedSearchParameterRegistry : ISearchParameterRegistry
    {
        private readonly Assembly _resourceAssembly;
        private readonly string _unsupportedParamsEmbeddedResourceName;
        private UnsupportedSearchParameters _unsupportedParams;
        private object sync = new object();

        public FilebasedSearchParameterRegistry(Assembly resourceAssembly, string unsupportedParamsEmbeddedResourceName)
        {
            EnsureArg.IsNotNull(resourceAssembly, nameof(resourceAssembly));
            EnsureArg.IsNotNullOrWhiteSpace(unsupportedParamsEmbeddedResourceName, nameof(unsupportedParamsEmbeddedResourceName));

            _resourceAssembly = resourceAssembly;
            _unsupportedParamsEmbeddedResourceName = unsupportedParamsEmbeddedResourceName;
        }

        public Task<IReadOnlyCollection<ResourceSearchParameterStatus>> GetSearchParameterStatuses()
        {
            if (_unsupportedParams == null)
            {
                lock (sync)
                {
                    if (_unsupportedParams == null)
                    {
                        using Stream stream = _resourceAssembly.GetManifestResourceStream(_unsupportedParamsEmbeddedResourceName);
                        using TextReader reader = new StreamReader(stream);
                        _unsupportedParams = JsonConvert.DeserializeObject<UnsupportedSearchParameters>(reader.ReadToEnd());
                    }
                }
            }

            ResourceSearchParameterStatus[] result = _unsupportedParams.Unsupported
                .Select(x => new ResourceSearchParameterStatus
                {
                    Uri = x,
                    Status = SearchParameterStatus.Disabled,
                    LastUpdated = Clock.UtcNow,
                })
                .Concat(_unsupportedParams.PartialSupport
                    .Select(x => new ResourceSearchParameterStatus
                    {
                        Uri = x,
                        Status = SearchParameterStatus.Enabled,
                        IsPartiallySupported = true,
                        LastUpdated = Clock.UtcNow,
                    }))
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<ResourceSearchParameterStatus>>(result);
        }

        public Task UpdateStatuses(IEnumerable<ResourceSearchParameterStatus> statuses)
        {
            return Task.CompletedTask;
        }
    }
}
