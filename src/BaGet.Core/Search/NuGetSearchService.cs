using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Protocol.Models;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace BaGet.Core
{
    public class NuGetSearchService : ISearchService
    {
        private readonly ILogger _logger;
        private readonly Lazy<SourceRepository> _sourceRepository;

        // TODO: try to depend on the BaGet.Protocol NuGet client instead of the official one so that we don't depend on ISettings
        public NuGetSearchService(ILogger logger, ISettings settings, IOptions<MirrorOptions> mirrorOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sourceRepository = new Lazy<SourceRepository>(() =>
            {
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSource = packageSourceProvider.LoadPackageSources().Single(e => e.SourceUri == mirrorOptions.Value.PackageSource);
                return new SourceRepository(packageSource, Repository.Provider.GetCoreV3());
            });
        }

        private SourceRepository SourceRepository => _sourceRepository.Value;

        public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            var packageSearchResource = await SourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);
            var searchFilter = new SearchFilter(includePrerelease: request.IncludePrerelease)
            {
                SupportedFrameworks = request.Framework is null ? Enumerable.Empty<string>() : new[] { request.Framework },
                PackageTypes = request.PackageType is null ? Enumerable.Empty<string>() : new[] { request.PackageType },
            };

            var searchResults = await packageSearchResource.SearchAsync(request.Query, searchFilter, request.Skip, request.Take, _logger, cancellationToken);

            return new SearchResponse
            {
                Data = searchResults.Select(MapSearchResultAsync).ToList(),
            };
        }

        private static SearchResult MapSearchResultAsync(IPackageSearchMetadata packageSearchMetadata)
        {
            var packageId = packageSearchMetadata.Identity.Id;
            var packageVersion = packageSearchMetadata.Identity.Version;
            return new SearchResult
            {
                PackageId = packageId,
                Version = packageVersion.ToString(),
                Description = packageSearchMetadata.Description,
                Authors = packageSearchMetadata.Authors?.Split(',').Select(e => e.Trim()).ToList() ?? new List<string>(), // TODO (multiple separators?)
                IconUrl = packageSearchMetadata.IconUrl?.ToString(),
                LicenseUrl = packageSearchMetadata.LicenseUrl?.ToString(),
                PackageTypes = Array.Empty<SearchResultPackageType>(),//nuspecReader.GetPackageTypes().Select(e => new SearchResultPackageType { Name = e.Name }).ToList(),
                ProjectUrl = null,//nuspecReader.GetProjectUrl(),
                RegistrationIndexUrl = null, // TODO
                Summary = packageSearchMetadata.Summary,
                Tags = packageSearchMetadata.Tags?.Split(',').Select(e => e.Trim()).ToList() ?? new List<string>(), // TODO (multiple separators?)
                Title = packageSearchMetadata.Title,
                TotalDownloads = 0, // TODO
                Versions = Array.Empty<SearchResultVersion>(), // TODO
            };
        }

        public Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<AutocompleteResponse> ListPackageVersionsAsync(VersionsRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
        {
            // TODO => how? We don't have a db :-/
            return Task.FromResult(new DependentsResponse { Data = Array.Empty<PackageDependent>() });
        }
    }
}
