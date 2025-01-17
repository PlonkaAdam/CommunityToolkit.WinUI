// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls.DataGridInternals;

namespace CommunityToolkit.WinUI.UI.Controls
{
    internal class DataGridColumnCollection : ObservableCollection<DataGridColumn>
    {
        private DataGrid _owningGrid;

        public DataGridColumnCollection(DataGrid owningGrid)
        {
            _owningGrid = owningGrid;
            this.ItemsInternal = new List<DataGridColumn>();
            this.FillerColumn = new DataGridFillerColumn(owningGrid);
            this.DisplayIndexMap = new List<int>();
            this.RowGroupSpacerColumn = new DataGridFillerColumn(owningGrid);
        }

        internal int AutogeneratedColumnCount
        {
            get;
            set;
        }

        internal List<int> DisplayIndexMap
        {
            get;
            set;
        }

        internal DataGridFillerColumn FillerColumn
        {
            get;
            private set;
        }

        internal DataGridColumn FirstColumn
        {
            get
            {
                return GetFirstColumn(null /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
            }
        }

        internal DataGridColumn FirstVisibleColumn
        {
            get
            {
                return GetFirstColumn(true /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
            }
        }

        internal DataGridColumn FirstVisibleNonFillerColumn
        {
            get
            {
                DataGridColumn dataGridColumn = this.FirstVisibleColumn;
                if (dataGridColumn == this.RowGroupSpacerColumn)
                {
                    dataGridColumn = GetNextVisibleColumn(dataGridColumn);
                }

                return dataGridColumn;
            }
        }

        internal DataGridColumn FirstVisibleWritableColumn
        {
            get
            {
                return GetFirstColumn(true /*isVisible*/, null /*isFrozen*/, false /*isReadOnly*/);
            }
        }

        internal DataGridColumn FirstVisibleScrollingColumn
        {
            get
            {
                return GetFirstColumn(true /*isVisible*/, false /*isFrozen*/, null /*isReadOnly*/);
            }
        }

        internal List<DataGridColumn> ItemsInternal
        {
            get;
            private set;
        }

        internal DataGridColumn LastVisibleColumn
        {
            get
            {
                return GetLastColumn(true /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
            }
        }

        internal DataGridColumn LastVisibleScrollingColumn
        {
            get
            {
                return GetLastColumn(true /*isVisible*/, false /*isFrozen*/, null /*isReadOnly*/);
            }
        }

        internal DataGridColumn LastVisibleWritableColumn
        {
            get
            {
                return GetLastColumn(true /*isVisible*/, null /*isFrozen*/, false /*isReadOnly*/);
            }
        }

        internal DataGridFillerColumn RowGroupSpacerColumn
        {
            get;
            private set;
        }

        internal int VisibleColumnCount
        {
            get
            {
                int visibleColumnCount = 0;
                for (int columnIndex = 0; columnIndex < this.ItemsInternal.Count; columnIndex++)
                {
                    if (this.ItemsInternal[columnIndex].IsVisible)
                    {
                        visibleColumnCount++;
                    }
                }

                return visibleColumnCount;
            }
        }

        internal double VisibleEdgedColumnsWidth
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the number of star columns that are currently visible.
        /// NOTE: Requires that EnsureVisibleEdgedColumnsWidth has been called.
        /// </summary>
        internal int VisibleStarColumnCount
        {
            get;
            private set;
        }

        protected override void ClearItems()
        {
            Debug.Assert(_owningGrid != null, "Expected non-null owning DataGrid.");
            try
            {
                _owningGrid.NoCurrentCellChangeCount++;
                if (this.ItemsInternal.Count > 0)
                {
                    if (_owningGrid.InDisplayIndexAdjustments)
                    {
                        // We are within columns display indexes adjustments. We do not allow changing the column collection while adjusting display indexes.
                        throw DataGridError.DataGrid.CannotChangeColumnCollectionWhileAdjustingDisplayIndexes();
                    }

                    _owningGrid.OnClearingColumns();
                    for (int columnIndex = 0; columnIndex < this.ItemsInternal.Count; columnIndex++)
                    {
                        // Detach the column...
                        this.ItemsInternal[columnIndex].OwningGrid = null;
                    }

                    this.ItemsInternal.Clear();
                    this.DisplayIndexMap.Clear();
                    this.AutogeneratedColumnCount = 0;
                    _owningGrid.OnColumnCollectionChanged_PreNotification(false /*columnsGrew*/);
                    base.ClearItems();
                    this.VisibleEdgedColumnsWidth = 0;
                    _owningGrid.OnColumnCollectionChanged_PostNotification(false /*columnsGrew*/);
                }
            }
            finally
            {
                _owningGrid.NoCurrentCellChangeCount--;
            }
        }

        protected override void InsertItem(int columnIndex, DataGridColumn dataGridColumn)
        {
            Debug.Assert(_owningGrid != null, "Expected non-null owning DataGrid.");
            try
            {
                _owningGrid.NoCurrentCellChangeCount++;
                if (_owningGrid.InDisplayIndexAdjustments)
                {
                    // We are within columns display indexes adjustments. We do not allow changing the column collection while adjusting display indexes.
                    throw DataGridError.DataGrid.CannotChangeColumnCollectionWhileAdjustingDisplayIndexes();
                }

                if (dataGridColumn == null)
                {
                    throw new ArgumentNullException("dataGridColumn");
                }

                int columnIndexWithFiller = columnIndex;
                if (dataGridColumn != this.RowGroupSpacerColumn && this.RowGroupSpacerColumn.IsRepresented)
                {
                    columnIndexWithFiller++;
                }

                // get the new current cell coordinates
                DataGridCellCoordinates newCurrentCellCoordinates = _owningGrid.OnInsertingColumn(columnIndex, dataGridColumn);

                // insert the column into our internal list
                this.ItemsInternal.Insert(columnIndexWithFiller, dataGridColumn);
                dataGridColumn.Index = columnIndexWithFiller;
                dataGridColumn.OwningGrid = _owningGrid;
                dataGridColumn.RemoveEditingElement();
                if (dataGridColumn.IsVisible)
                {
                    this.VisibleEdgedColumnsWidth += dataGridColumn.ActualWidth;
                }

                // continue with the base insert
                _owningGrid.OnInsertedColumn_PreNotification(dataGridColumn);
                _owningGrid.OnColumnCollectionChanged_PreNotification(true /*columnsGrew*/);

                if (dataGridColumn != this.RowGroupSpacerColumn)
                {
                    base.InsertItem(columnIndex, dataGridColumn);
                }

                _owningGrid.OnInsertedColumn_PostNotification(newCurrentCellCoordinates, dataGridColumn.DisplayIndex);
                _owningGrid.OnColumnCollectionChanged_PostNotification(true /*columnsGrew*/);
            }
            finally
            {
                _owningGrid.NoCurrentCellChangeCount--;
            }
        }

        protected override void RemoveItem(int columnIndex)
        {
            RemoveItemPrivate(columnIndex, false /*isSpacer*/);
        }

        protected override void SetItem(int columnIndex, DataGridColumn dataGridColumn)
        {
            throw new NotSupportedException();
        }

        internal bool DisplayInOrder(int columnIndex1, int columnIndex2)
        {
            int displayIndex1 = ((DataGridColumn)this.ItemsInternal[columnIndex1]).DisplayIndexWithFiller;
            int displayIndex2 = ((DataGridColumn)this.ItemsInternal[columnIndex2]).DisplayIndexWithFiller;
            return displayIndex1 < displayIndex2;
        }

        internal bool EnsureRowGrouping(bool rowGrouping)
        {
            // The insert below could cause the first column to be added.  That causes a refresh
            // which re-enters this method so instead of checking RowGroupSpacerColumn.IsRepresented,
            // we need to check to see if it's actually in our collection instead.
            bool spacerRepresented = (this.ItemsInternal.Count > 0) && (this.ItemsInternal[0] == this.RowGroupSpacerColumn);
            if (rowGrouping && !spacerRepresented)
            {
                this.Insert(0, this.RowGroupSpacerColumn);
                this.RowGroupSpacerColumn.IsRepresented = true;
                return true;
            }
            else if (!rowGrouping && spacerRepresented)
            {
                Debug.Assert(this.ItemsInternal[0] == this.RowGroupSpacerColumn, "Unexpected RowGroupSpacerColumn value.");

                // We need to set IsRepresented to false before removing the RowGroupSpacerColumn
                // otherwise, we'll remove the column after it
                this.RowGroupSpacerColumn.IsRepresented = false;
                RemoveItemPrivate(0, true /*isSpacer*/);
                Debug.Assert(this.DisplayIndexMap.Count == this.ItemsInternal.Count, "Unexpected DisplayIndexMap.Count value.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// In addition to ensuring that column widths are valid, this method updates the values of
        /// VisibleEdgedColumnsWidth and VisibleStarColumnCount.
        /// </summary>
        internal void EnsureVisibleEdgedColumnsWidth()
        {
            this.VisibleStarColumnCount = 0;
            this.VisibleEdgedColumnsWidth = 0;
            for (int columnIndex = 0; columnIndex < this.ItemsInternal.Count; columnIndex++)
            {
                if (this.ItemsInternal[columnIndex].IsVisible)
                {
                    this.ItemsInternal[columnIndex].EnsureWidth();
                    if (this.ItemsInternal[columnIndex].Width.IsStar)
                    {
                        this.VisibleStarColumnCount++;
                    }

                    this.VisibleEdgedColumnsWidth += this.ItemsInternal[columnIndex].ActualWidth;
                }
            }
        }

        internal DataGridColumn GetColumnAtDisplayIndex(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= this.ItemsInternal.Count || displayIndex >= this.DisplayIndexMap.Count)
            {
                return null;
            }

            int columnIndex = this.DisplayIndexMap[displayIndex];
            return this.ItemsInternal[columnIndex];
        }

        internal int GetColumnCount(bool isVisible, bool isFrozen, int fromColumnIndex, int toColumnIndex)
        {
            Debug.Assert(DisplayInOrder(fromColumnIndex, toColumnIndex), "Unexpected column display order.");
            Debug.Assert(this.ItemsInternal[toColumnIndex].IsVisible == isVisible, "Unexpected column visibility state.");
            Debug.Assert(this.ItemsInternal[toColumnIndex].IsFrozen == isFrozen, "Unexpected column frozen state.");

            int columnCount = 0;
            DataGridColumn dataGridColumn = this.ItemsInternal[fromColumnIndex];

            while (dataGridColumn != this.ItemsInternal[toColumnIndex])
            {
                dataGridColumn = GetNextColumn(dataGridColumn, isVisible, isFrozen, null /*isReadOnly*/);
                Debug.Assert(dataGridColumn != null, "Expected non-null dataGridColumn.");
                columnCount++;
            }

            return columnCount;
        }

        internal IEnumerable<DataGridColumn> GetDisplayedColumns()
        {
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");
            foreach (int columnIndex in this.DisplayIndexMap)
            {
                yield return this.ItemsInternal[columnIndex];
            }
        }

        /// <summary>
        /// Returns an enumeration of all columns that meet the criteria of the filter predicate.
        /// </summary>
        /// <param name="filter">Criteria for inclusion.</param>
        /// <returns>Columns that meet the criteria, in ascending DisplayIndex order.</returns>
        internal IEnumerable<DataGridColumn> GetDisplayedColumns(Predicate<DataGridColumn> filter)
        {
            Debug.Assert(filter != null, "Expected non-null filter.");
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");
            foreach (int columnIndex in this.DisplayIndexMap)
            {
                DataGridColumn column = this.ItemsInternal[columnIndex];
                if (filter(column))
                {
                    yield return column;
                }
            }
        }

        /// <summary>
        /// Returns an enumeration of all columns that meet the criteria of the filter predicate.
        /// The columns are returned in the order specified by the reverse flag.
        /// </summary>
        /// <param name="reverse">Whether or not to return the columns in descending DisplayIndex order.</param>
        /// <param name="filter">Criteria for inclusion.</param>
        /// <returns>Columns that meet the criteria, in the order specified by the reverse flag.</returns>
        internal IEnumerable<DataGridColumn> GetDisplayedColumns(bool reverse, Predicate<DataGridColumn> filter)
        {
            return reverse ? GetDisplayedColumnsReverse(filter) : GetDisplayedColumns(filter);
        }

        /// <summary>
        /// Returns an enumeration of all columns that meet the criteria of the filter predicate.
        /// The columns are returned in descending DisplayIndex order.
        /// </summary>
        /// <param name="filter">Criteria for inclusion.</param>
        /// <returns>Columns that meet the criteria, in descending DisplayIndex order.</returns>
        internal IEnumerable<DataGridColumn> GetDisplayedColumnsReverse(Predicate<DataGridColumn> filter)
        {
            Debug.Assert(filter != null, "Expected non-null filter.");
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");
            for (int displayIndex = this.DisplayIndexMap.Count - 1; displayIndex >= 0; displayIndex--)
            {
                DataGridColumn column = this.ItemsInternal[this.DisplayIndexMap[displayIndex]];
                if (filter(column))
                {
                    yield return column;
                }
            }
        }

        internal DataGridColumn GetFirstColumn(bool? isVisible, bool? isFrozen, bool? isReadOnly)
        {
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");
            int index = 0;
            while (index < this.DisplayIndexMap.Count)
            {
                DataGridColumn dataGridColumn = GetColumnAtDisplayIndex(index);
                if ((isVisible == null || dataGridColumn.IsVisible == isVisible) &&
                    (isFrozen == null || dataGridColumn.IsFrozen == isFrozen) &&
                    (isReadOnly == null || dataGridColumn.IsReadOnly == isReadOnly))
                {
                    return dataGridColumn;
                }

                index++;
            }

            return null;
        }

        internal DataGridColumn GetLastColumn(bool? isVisible, bool? isFrozen, bool? isReadOnly)
        {
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");
            int index = this.DisplayIndexMap.Count - 1;
            while (index >= 0)
            {
                DataGridColumn dataGridColumn = GetColumnAtDisplayIndex(index);
                if ((isVisible == null || dataGridColumn.IsVisible == isVisible) &&
                    (isFrozen == null || dataGridColumn.IsFrozen == isFrozen) &&
                    (isReadOnly == null || dataGridColumn.IsReadOnly == isReadOnly))
                {
                    return dataGridColumn;
                }

                index--;
            }

            return null;
        }

        internal DataGridColumn GetNextColumn(DataGridColumn dataGridColumnStart)
        {
            return GetNextColumn(dataGridColumnStart, null /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
        }

        internal DataGridColumn GetNextColumn(
            DataGridColumn dataGridColumnStart,
            bool? isVisible,
            bool? isFrozen,
            bool? isReadOnly)
        {
            Debug.Assert(dataGridColumnStart != null, "Expected non-null dataGridColumnStart.");
            Debug.Assert(this.ItemsInternal.Contains(dataGridColumnStart), "Expected dataGridColumnStart in ItemsInternal.");
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");

            int index = dataGridColumnStart.DisplayIndexWithFiller + 1;
            while (index < this.DisplayIndexMap.Count)
            {
                DataGridColumn dataGridColumn = GetColumnAtDisplayIndex(index);

                if ((isVisible == null || dataGridColumn.IsVisible == isVisible) &&
                    (isFrozen == null || dataGridColumn.IsFrozen == isFrozen) &&
                    (isReadOnly == null || dataGridColumn.IsReadOnly == isReadOnly))
                {
                    return dataGridColumn;
                }

                index++;
            }

            return null;
        }

        internal DataGridColumn GetNextVisibleColumn(DataGridColumn dataGridColumnStart)
        {
            return GetNextColumn(dataGridColumnStart, true /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
        }

        internal DataGridColumn GetNextVisibleFrozenColumn(DataGridColumn dataGridColumnStart)
        {
            return GetNextColumn(dataGridColumnStart, true /*isVisible*/, true /*isFrozen*/, null /*isReadOnly*/);
        }

        internal DataGridColumn GetNextVisibleWritableColumn(DataGridColumn dataGridColumnStart)
        {
            return GetNextColumn(dataGridColumnStart, true /*isVisible*/, null /*isFrozen*/, false /*isReadOnly*/);
        }

        internal DataGridColumn GetPreviousColumn(
            DataGridColumn dataGridColumnStart,
            bool? isVisible,
            bool? isFrozen,
            bool? isReadOnly)
        {
            Debug.Assert(dataGridColumnStart != null, "Expected non-null dataGridColumnStart.");
            Debug.Assert(this.ItemsInternal.Contains(dataGridColumnStart), "Expected dataGridColumnStart in ItemsInternal.");
            Debug.Assert(this.ItemsInternal.Count == this.DisplayIndexMap.Count, "Unexpected DisplayIndexMap.Count value.");

            int index = dataGridColumnStart.DisplayIndexWithFiller - 1;
            while (index >= 0)
            {
                DataGridColumn dataGridColumn = GetColumnAtDisplayIndex(index);
                if ((isVisible == null || dataGridColumn.IsVisible == isVisible) &&
                    (isFrozen == null || dataGridColumn.IsFrozen == isFrozen) &&
                    (isReadOnly == null || dataGridColumn.IsReadOnly == isReadOnly))
                {
                    return dataGridColumn;
                }

                index--;
            }

            return null;
        }

        internal DataGridColumn GetPreviousVisibleNonFillerColumn(DataGridColumn dataGridColumnStart)
        {
            DataGridColumn column = GetPreviousColumn(dataGridColumnStart, true /*isVisible*/, null /*isFrozen*/, null /*isReadOnly*/);
            return (column is DataGridFillerColumn) ? null : column;
        }

        internal DataGridColumn GetPreviousVisibleScrollingColumn(DataGridColumn dataGridColumnStart)
        {
            return GetPreviousColumn(dataGridColumnStart, true /*isVisible*/, false /*isFrozen*/, null /*isReadOnly*/);
        }

        internal DataGridColumn GetPreviousVisibleWritableColumn(DataGridColumn dataGridColumnStart)
        {
            return GetPreviousColumn(dataGridColumnStart, true /*isVisible*/, null /*isFrozen*/, false /*isReadOnly*/);
        }

        internal int GetVisibleColumnCount(int fromColumnIndex, int toColumnIndex)
        {
            Debug.Assert(DisplayInOrder(fromColumnIndex, toColumnIndex), "Unexpected column display order.");
            Debug.Assert(this.ItemsInternal[toColumnIndex].IsVisible, "Unexpected column visibility state.");

            int columnCount = 0;
            DataGridColumn dataGridColumn = this.ItemsInternal[fromColumnIndex];

            while (dataGridColumn != this.ItemsInternal[toColumnIndex])
            {
                dataGridColumn = GetNextVisibleColumn(dataGridColumn);
                Debug.Assert(dataGridColumn != null, "Expected non-null dataGridColumn.");
                columnCount++;
            }

            return columnCount;
        }

        internal IEnumerable<DataGridColumn> GetVisibleColumns()
        {
            Predicate<DataGridColumn> filter = column => column.IsVisible;
            return GetDisplayedColumns(filter);
        }

        internal IEnumerable<DataGridColumn> GetVisibleFrozenColumns()
        {
            Predicate<DataGridColumn> filter = column => column.IsVisible && column.IsFrozen;
            return GetDisplayedColumns(filter);
        }

        internal double GetVisibleFrozenEdgedColumnsWidth()
        {
            double visibleFrozenColumnsWidth = 0;
            for (int columnIndex = 0; columnIndex < this.ItemsInternal.Count; columnIndex++)
            {
                if (this.ItemsInternal[columnIndex].IsVisible && this.ItemsInternal[columnIndex].IsFrozen)
                {
                    visibleFrozenColumnsWidth += this.ItemsInternal[columnIndex].ActualWidth;
                }
            }

            return visibleFrozenColumnsWidth;
        }

        internal IEnumerable<DataGridColumn> GetVisibleScrollingColumns()
        {
            Predicate<DataGridColumn> filter = column => column.IsVisible && !column.IsFrozen;
            return GetDisplayedColumns(filter);
        }

        private void RemoveItemPrivate(int columnIndex, bool isSpacer)
        {
            Debug.Assert(_owningGrid != null, "Expected non-null owning DataGrid.");
            try
            {
                _owningGrid.NoCurrentCellChangeCount++;

                if (_owningGrid.InDisplayIndexAdjustments)
                {
                    // We are within columns display indexes adjustments. We do not allow changing the column collection while adjusting display indexes.
                    throw DataGridError.DataGrid.CannotChangeColumnCollectionWhileAdjustingDisplayIndexes();
                }

                int columnIndexWithFiller = columnIndex;
                if (!isSpacer && this.RowGroupSpacerColumn.IsRepresented)
                {
                    columnIndexWithFiller++;
                }

                Debug.Assert(columnIndexWithFiller >= 0 && columnIndexWithFiller < this.ItemsInternal.Count, "Unexpected columnIndexWithFiller value.");

                DataGridColumn dataGridColumn = this.ItemsInternal[columnIndexWithFiller];
                DataGridCellCoordinates newCurrentCellCoordinates = _owningGrid.OnRemovingColumn(dataGridColumn);
                this.ItemsInternal.RemoveAt(columnIndexWithFiller);
                if (dataGridColumn.IsVisible)
                {
                    this.VisibleEdgedColumnsWidth -= dataGridColumn.ActualWidth;
                }

                dataGridColumn.OwningGrid = null;
                dataGridColumn.RemoveEditingElement();

                // continue with the base remove
                _owningGrid.OnRemovedColumn_PreNotification(dataGridColumn);
                _owningGrid.OnColumnCollectionChanged_PreNotification(false /*columnsGrew*/);
                if (!isSpacer)
                {
                    base.RemoveItem(columnIndex);
                }

                _owningGrid.OnRemovedColumn_PostNotification(newCurrentCellCoordinates);
                _owningGrid.OnColumnCollectionChanged_PostNotification(false /*columnsGrew*/);
            }
            finally
            {
                _owningGrid.NoCurrentCellChangeCount--;
            }
        }

#if DEBUG
        internal bool Debug_VerifyColumnDisplayIndexes()
        {
            for (int columnDisplayIndex = 0; columnDisplayIndex < this.ItemsInternal.Count; columnDisplayIndex++)
            {
                if (GetColumnAtDisplayIndex(columnDisplayIndex) == null)
                {
                    return false;
                }
            }

            return true;
        }

        internal void Debug_PrintColumns()
        {
            foreach (DataGridColumn column in this.ItemsInternal)
            {
                Debug.WriteLine(string.Format(global::System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2}", column.Header, column.Index, column.DisplayIndex));
            }
        }
#endif
    }
}