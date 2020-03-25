// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnsureThat;
using MediatR;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Messages.Search;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Search.Registry
{
    public class SearchParameterStatusManager : IRequireInitializationOnFirstRequest
    {
        private readonly ISearchParameterRegistry _searchParameterRegistry;
        private readonly ISearchParameterDefinitionManager _searchParameterDefinitionManager;
        private readonly IMediator _mediator;

        public SearchParameterStatusManager(
            ISearchParameterRegistry searchParameterRegistry,
            ISearchParameterDefinitionManager searchParameterDefinitionManager,
            IMediator mediator)
        {
            EnsureArg.IsNotNull(searchParameterRegistry, nameof(searchParameterRegistry));
            EnsureArg.IsNotNull(searchParameterDefinitionManager, nameof(searchParameterDefinitionManager));
            EnsureArg.IsNotNull(mediator, nameof(mediator));

            _searchParameterRegistry = searchParameterRegistry;
            _searchParameterDefinitionManager = searchParameterDefinitionManager;
            _mediator = mediator;
        }

        public async Task EnsureInitialized()
        {
            var updated = new List<SearchParameterInfo>();

            var parameters = (await _searchParameterRegistry.GetSearchParameterStatuses())
                .ToDictionary(x => x.Uri);

            // Set states of known parameters
            foreach (var p in _searchParameterDefinitionManager.AllSearchParameters)
            {
                if (parameters.TryGetValue(p.Url, out var result))
                {
                    p.IsSearchable = result.Status == SearchParameterStatus.Enabled;
                    p.IsSupported = result.Status != SearchParameterStatus.Disabled;
                    p.IsPartiallySupported = result.IsPartiallySupported;

                    updated.Add(p);
                }
            }

            // Update registry with additional parameters
            var newParameters = _searchParameterDefinitionManager.AllSearchParameters
                .Select(x => x.Url)
                .Except(parameters.Select(x => x.Key))
                .Select(x => _searchParameterDefinitionManager.GetSearchParameter(x))
                .Select(x => new ResourceSearchParameterStatus
                {
                    Uri = x.Url,
                    LastUpdated = Clock.UtcNow,
                    Status = SearchParameterStatus.Supported,
                })
                .ToArray();

            if (newParameters.Any())
            {
                await _searchParameterRegistry.UpdateStatuses(newParameters);
            }

            await _mediator.Publish(new SearchParametersUpdated(updated));
        }
    }
}
