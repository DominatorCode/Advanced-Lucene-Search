using System;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace AdvancedLuceneSearch.ViewModel
{
    public abstract class INPCBase : INotifyPropertyChanged, IDisposable
    {
        private CompositeDisposable compositeDisposable = new CompositeDisposable();

        public void AddDisposable(IDisposable disposable)
        {
            compositeDisposable.Add(disposable);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyChanged(params string[] propertyNames)
        {
            foreach (string name in propertyNames)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(name));
            }
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, e);
            }
        }

        public void Dispose()
        {
            compositeDisposable.Dispose();
        }
    }


}
