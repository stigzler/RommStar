using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class SyncStatusToVisibilityConverterInv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SyncStatus syncStatus = (SyncStatus)value;

            if (syncStatus == SyncStatus.Queued || syncStatus == SyncStatus.ProcessingMetadata ||
                syncStatus == SyncStatus.SyncingFiles) return Visibility.Visible;

            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
