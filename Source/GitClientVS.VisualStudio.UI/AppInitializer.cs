﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using GitClientVS.Contracts.Events;
using GitClientVS.Contracts.Interfaces.Services;
using GitClientVS.Contracts.Interfaces.ViewModels;
using GitClientVS.Contracts.Models;
using GitClientVS.Infrastructure;
using GitClientVS.Infrastructure.Mappings;
using GitClientVS.Infrastructure.Mappings.Bitbucket;
using GitClientVS.Infrastructure.Mappings.Gitlab;
using GitClientVS.UI.Helpers;
using log4net;
using log4net.Repository.Hierarchy;
using Microsoft.TeamFoundation.Common.Internal;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace GitClientVS.VisualStudio.UI
{
    [Export(typeof(IAppInitializer))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class AppInitializer : IAppInitializer
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IStorageService _storageService;
        private readonly IGitClientServiceFactory _gitClientFactory;

        [ImportingConstructor]
        public AppInitializer(
            IStorageService storageService,
            IGitClientServiceFactory gitClientFactory
            )
        {
            _storageService = storageService;
            _gitClientFactory = gitClientFactory;
        }

        public async Task Initialize()
        {
            LoggerConfigurator.Setup();
            var result = _storageService.LoadUserData();

            Mapper.Initialize(cfg =>
            {
                cfg.AddProfile<BitbucketMappingsProfile>();
                cfg.AddProfile<GitlabMappingsProfile>();
                cfg.AddProfile<VisualMappingsProfile>();
            });

            await GitClientLogin(result);
        }

        private async Task GitClientLogin(Result<ConnectionData> result)
        {
            if (result.IsSuccess && result.Data.IsLoggedIn)
            {
                try
                {

                    var gitClient = _gitClientFactory.GetService(result.Data.GitProvider);
                    await gitClient.LoginAsync(result.Data.UserName, result.Data.Password);
                }
                catch (Exception ex)
                {
                    Logger.Warn("Couldn't login user using stored credentials. Credentials must have been changed or there is no internet connection", ex);
                }
            }
        }
    }
}
