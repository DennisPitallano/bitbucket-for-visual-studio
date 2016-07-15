﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using GitClientVS.Contracts.Interfaces;
using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using GitClientVS.Contracts.Interfaces.ViewModels;
using GitClientVS.Contracts.Interfaces.Views;

namespace GitClientVS.Infrastructure.ViewModels
{
    [Export(typeof(IConnectSectionViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ConnectSectionViewModel : ViewModelBase, IConnectSectionViewModel
    {
        private readonly ExportFactory<ILoginDialogView> _loginViewFactory;
        private readonly ReactiveCommand<object> _openConnectCommand;

        [ImportingConstructor]
        public ConnectSectionViewModel(ExportFactory<ILoginDialogView> loginViewFactory)
        {
            this.WhenAnyValue(x => x.Message).Subscribe(x =>
            {
                MessageB = Message + " Hej";
            });
            Message = "Bucket";

            _loginViewFactory = loginViewFactory;

            _openConnectCommand = ReactiveCommand.Create(CanExecute());
            _openConnectCommand.Subscribe(_ => _loginViewFactory.CreateExport().Value.ShowModal());
        }

        private IObservable<bool> CanExecute()
        {
            return Observable.Return(true);
        }

        private string _message;

        public string Message
        {
            get { return _message; }
            set { this.RaiseAndSetIfChanged(ref _message, value); }
        }

        public string MessageB { get; set; }

        public ICommand OpenConnectCommand => _openConnectCommand;
    }
}