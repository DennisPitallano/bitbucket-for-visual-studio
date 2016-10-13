﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GitClientVS.Contracts;
using GitClientVS.Contracts.Interfaces.Services;
using GitClientVS.Contracts.Interfaces.ViewModels;
using GitClientVS.Contracts.Interfaces.Views;
using GitClientVS.Contracts.Models.GitClientModels;
using GitClientVS.Infrastructure.Extensions;
using GitClientVS.Infrastructure.Utils;
using ReactiveUI;
using WpfControls;
using SuggestionProvider = WpfControls.SuggestionProvider;

namespace GitClientVS.Infrastructure.ViewModels
{
    [Export(typeof(IPullRequestsMainViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class PullRequestsMainViewModel : ViewModelBase, IPullRequestsMainViewModel
    {
        private readonly IGitClientService _gitClientService;
        private readonly IGitService _gitService;
        private readonly IPageNavigationService<IPullRequestsWindow> _pageNavigationService;
        private readonly ICacheService _cacheService;
        private readonly IVsTools _vsTools;
        private ReactiveCommand<Unit> _initializeCommand;
        private ReactiveCommand<object> _goToDetailsCommand;
        private bool _isLoading;
        private ReactiveList<GitPullRequest> _gitPullRequests;
        private ReactiveList<GitPullRequest> _filteredGitPullRequests;
        private string _errorMessage;
        private ReactiveCommand<object> _goToCreateNewPullRequestCommand;

        private List<GitUser> _authors;
        private GitUser _selectedAuthor;
        private GitPullRequestStatus? _selectedStatus;
        private GitPullRequest _selectedPullRequest;
        private GitRepository _currentRepository;

        public ReactiveList<GitPullRequest> GitPullRequests
        {
            get { return _gitPullRequests; }
            set { this.RaiseAndSetIfChanged(ref _gitPullRequests, value); }
        }

        public GitUser SelectedAuthor
        {
            get { return _selectedAuthor; }
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedAuthor, value);
            }
        }

        public GitPullRequestStatus? SelectedStatus
        {
            get { return _selectedStatus; }
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStatus, value);
            }
        }

        public ReactiveList<GitPullRequest> FilteredGitPullRequests
        {
            get { return _filteredGitPullRequests; }
            set { this.RaiseAndSetIfChanged(ref _filteredGitPullRequests, value); }
        }

        public List<GitUser> Authors
        {
            get { return _authors; }
            set { this.RaiseAndSetIfChanged(ref _authors, value); }
        }

        public GitPullRequest SelectedPullRequest
        {
            get { return _selectedPullRequest; }
            set { this.RaiseAndSetIfChanged(ref _selectedPullRequest, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { this.RaiseAndSetIfChanged(ref _errorMessage, value); }
        }

        public IEnumerable<IReactiveCommand> ThrowableCommands => new[] { _initializeCommand };
        public IEnumerable<IReactiveCommand> LoadingCommands => new[] { _initializeCommand };

        public bool IsLoading
        {
            get { return _isLoading; }
            set { this.RaiseAndSetIfChanged(ref _isLoading, value); }
        }

        public string PageTitle { get; } = "Pull Requests";

        public ICommand InitializeCommand => _initializeCommand;
        public ICommand GoToDetailsCommand => _goToDetailsCommand;
        public ICommand GotoCreateNewPullRequestCommand => _goToCreateNewPullRequestCommand;

        public ISuggestionProvider AuthorProvider
        {
            get
            {
                return new SuggestionProvider(x => Authors.Where(y =>
                (y.DisplayName != null && y.DisplayName.Contains(x, StringComparison.InvariantCultureIgnoreCase)) ||
                (y.Username != null && y.Username.Contains(x, StringComparison.InvariantCultureIgnoreCase))));
            }
        }




        [ImportingConstructor]
        public PullRequestsMainViewModel(
            IGitClientServiceFactory gitClientServiceFactory,
            IGitService gitService,
            IPageNavigationService<IPullRequestsWindow> pageNavigationService,
            ICacheService cacheService
            )
        {
            _gitClientService = gitClientServiceFactory.GetService();
            _gitService = gitService;
            _pageNavigationService = pageNavigationService;
            _cacheService = cacheService;
            _currentRepository = _gitService.GetActiveRepository();
            GitPullRequests = new ReactiveList<GitPullRequest>();
            FilteredGitPullRequests = new ReactiveList<GitPullRequest>();
            SetupObservables();
            Authors = new List<GitUser>();

            SelectedStatus = GitPullRequestStatus.Open;
        }

        private void SetupObservables()
        {
            this.WhenAnyValue(x => x.SelectedStatus, x => x.SelectedAuthor).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => Filter());
            this.WhenAnyObservable(x => x.GitPullRequests.Changed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ => Filter());
            this.WhenAnyValue(x => x.SelectedPullRequest).Where(x => x != null).Subscribe(_ => _goToDetailsCommand.Execute(SelectedPullRequest));

        }

        public void InitializeCommands()
        {
            _initializeCommand = ReactiveCommand.CreateAsyncTask(CanLoadPullRequests(), _ => LoadPullRequests());
            _goToCreateNewPullRequestCommand = ReactiveCommand.Create(Observable.Return(true));
            _goToCreateNewPullRequestCommand.Subscribe(_ => { _pageNavigationService.Navigate<ICreatePullRequestsView>(); });

            _goToDetailsCommand = ReactiveCommand.Create(Observable.Return(true));
            _goToDetailsCommand.Subscribe(x => { _pageNavigationService.Navigate<IPullRequestDetailView>(x); });
        }

        private async Task LoadPullRequests()
        {
            GitPullRequests.Clear();
            Authors = (await _gitClientService.GetPullRequestsAuthors(_currentRepository)).ToList();

            var result = _cacheService.Get<IEnumerable<GitPullRequest>>(CacheKeys.PullRequestCacheKey);
            if (result.IsSuccess)
            {
                GitPullRequests.AddRange(result.Data);
                ReloadAllPullRequests();
            }
            else
            {
                await ReloadAllPullRequests();
            }
        }

        private async Task ReloadAllPullRequests()
        {
            var allPullRequests = new List<GitPullRequest>();
            int startPage = 1;
            PageIterator<GitPullRequest> iterator;

            do
            {
                iterator = await _gitClientService.GetPullRequests(_currentRepository, page: startPage);
                allPullRequests.AddRange(iterator.Values);
                startPage = iterator.Page + 1;

            } while (iterator.HasNext());

            GitPullRequests.Clear();
            GitPullRequests.AddRange(allPullRequests);

            _cacheService.Add(CacheKeys.PullRequestCacheKey, GitPullRequests.ToList());
        }

        private bool CanRunFilter()
        {
            return GitPullRequests != null;
        }

        private void Filter()
        {
            if (!CanRunFilter())
                return;

            FilteredGitPullRequests = new ReactiveList<GitPullRequest>(
                GitPullRequests
                .Where(pullRequest => SelectedStatus == null || pullRequest.Status == SelectedStatus)
                .Where(pullRequest => SelectedAuthor == null || (pullRequest.Author != null && pullRequest.Author.Username == SelectedAuthor.Username))
                .OrderByDescending(x => x.Updated));
        }

        private IObservable<bool> CanLoadPullRequests()
        {
            return this.WhenAnyValue(x => x.IsLoading).Select(x => !IsLoading);
        }


    }
}
