using System.Collections.ObjectModel;
using AdvancedLuceneSearch.ViewModel.Controller;

namespace AdvancedLuceneSearch.ViewModel
{
    public class MainWindowViewModel : INPCBase
    {
        private string searchTerm;
        private int precisionSearch = 99;
        private int _countSearchResults = 1000;
        private RawRowDefinitionViewModel currentSearchResult;

        public MainWindowViewModel()
        {
            SearchCommand = new ReactiveCommand<object, object>((x) => !IsBusy);
            RawRows = new ObservableCollection<RawRowDefinitionViewModel>();

            // start the controller
            new MainViewModelController(this).Start();
        }

        public ReactiveCommand<object, object> SearchCommand { get; private set; }
        public bool IsBusy { get; set; }

        public ObservableCollection<RawRowDefinitionViewModel> RawRows { get; private set; }

        public string SearchTerm
        {
            get { return searchTerm; }
            set
            {
                if (this.searchTerm != value)
                {
                    this.searchTerm = value;
                    base.NotifyChanged("SearchTerm");
                }
            }
        }

        public int PrecisionSearch
        {
            get { return precisionSearch; }
            set
            {
                if (this.precisionSearch != value )
                {
                    this.precisionSearch = value;
                    base.NotifyChanged("PrecisionSearch");
                }
            }
        }

        public int CountSearchResults
        {
            get { return _countSearchResults; }
            set
            {
                if (this._countSearchResults != value)
                {
                    this._countSearchResults = value;
                    base.NotifyChanged("CountSearchResults");
                }
            }
        }
    }
}
