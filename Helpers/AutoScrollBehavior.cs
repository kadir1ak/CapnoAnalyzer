using System.Windows;
using Microsoft.Xaml.Behaviors;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CapnoAnalyzer.Helpers
{
    public class AutoScrollBehavior : Behavior<ListBox>
    {
        private bool _userScrolling;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded += OnLoaded;
                AssociatedObject.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (AssociatedObject.Items is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && !_userScrolling)
            {
                ScrollToBottom();
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = GetScrollViewer(AssociatedObject);
            if (scrollViewer == null) return;

            // Kullanıcı elle yukarı kaydırdıysa otomatik kaydırmayı devre dışı bırak
            _userScrolling = scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
        }

        private void ScrollToBottom()
        {
            var scrollViewer = GetScrollViewer(AssociatedObject);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer viewer)
                return viewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
