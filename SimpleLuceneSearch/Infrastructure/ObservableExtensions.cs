using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;


namespace AdvancedLuceneSearch
{
    public static class ObservableExtensions
    {
        public static IObservable<TValue> ObserveProperty<T, TValue>(this T source,Expression<Func<T, TValue>> propertyExpression)
            where T : INotifyPropertyChanged
        {
            return source.ObserveProperty(propertyExpression, false);
        }

        public static IObservable<TValue> ObserveProperty<T, TValue>(this T source,Expression<Func<T, TValue>> propertyExpression, 
            bool observeInitialValue) where T : INotifyPropertyChanged
        {
            var memberExpression = (MemberExpression)propertyExpression.Body;

            var getter = propertyExpression.Compile();

            var observable = Observable
                .FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => source.PropertyChanged += h,
                    h => source.PropertyChanged -= h)
                .Where(x => x.PropertyName == memberExpression.Member.Name)
                .Select(_ => getter(source));

            if (observeInitialValue)
                return observable.Merge(Observable.Return(getter(source)));

            return observable;
        }
    }
}
