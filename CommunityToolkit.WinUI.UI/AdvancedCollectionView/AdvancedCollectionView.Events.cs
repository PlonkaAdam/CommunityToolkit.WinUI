// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation.Collections;

namespace CommunityToolkit.WinUI.UI
{
    /// <summary>
    /// A collection view implementation that supports filtering, grouping, sorting and incremental loading
    /// </summary>
    public partial class AdvancedCollectionView : INotifyCollectionChanged
    {
        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Currently selected item changing event
        /// </summary>
        /// <param name="e">event args</param>
        private void OnCurrentChanging(CurrentChangingEventArgs e)
        {
            if (_deferCounter > 0)
            {
                return;
            }

            CurrentChanging?.Invoke(this, e);
        }

        /// <summary>
        /// Currently selected item changed event
        /// </summary>
        /// <param name="e">event args</param>
        private void OnCurrentChanged(object e)
        {
            if (_deferCounter > 0)
            {
                return;
            }

            CurrentChanged?.Invoke(this, e);

            // ReSharper disable once ExplicitCallerInfoArgument
            OnPropertyChanged(nameof(CurrentItem));
        }

        /// <summary>
        /// Vector changed event
        /// </summary>
        /// <param name="e">event args</param>
        private void OnVectorChanged(IVectorChangedEventArgs e)
        {
            if (_deferCounter > 0)
            {
                return;
            }

            VectorChanged?.Invoke(this, e);

            // ReSharper disable once ExplicitCallerInfoArgument
            OnPropertyChanged(nameof(Count));
        }

        /// <summary>
        /// Notify listeners that this View has changed
        /// </summary>
        /// <remarks>
        /// CollectionViews (and sub-classes) should take their filter/sort/grouping
        /// into account before calling this method to forward CollectionChanged events.
        /// </remarks>
        /// <param name="args">
        /// The NotifyCollectionChangedEventArgs to be passed to the EventHandler
        /// </param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            if (_deferCounter > 0)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(args, nameof(args));

            CollectionChanged?.Invoke(this, args);

            // Collection changes change the count unless an item is being
            // replaced or moved within the collection.
            if (args.Action != NotifyCollectionChangedAction.Replace)
            {
                OnPropertyChanged(nameof(Count));
            }

            bool isEmpty = _view.Count == 0;
            if (isEmpty != cachedIsEmpty)
            {
                cachedIsEmpty = isEmpty;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private bool cachedIsEmpty = true;

        /// <summary>
        /// Gets a value indicating whether collection is empty.
        /// </summary>
        public bool IsEmpty { get; private set; }
    }
}