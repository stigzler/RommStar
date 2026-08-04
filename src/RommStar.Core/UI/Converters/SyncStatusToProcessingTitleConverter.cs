using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class SyncStatusToProcessingTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((SyncStatus)value)
            {
                case SyncStatus.Queued:
                    return "Sync Job Queued.";
                    break;

                case SyncStatus.ProcessingMetadata:
                    return "Processing Metadata:";
                    break;

                case SyncStatus.SyncingFiles:
                    return "Processing Files:";
                    break;

                case SyncStatus.CompletedWithWarnings:
                    return "Sync Complete - with Warnings";
                    break;
                case SyncStatus.CompletedWithWarningsAndErrors:
                    return "Sync Complete - With Warnings and Errors";
                    break;
                case SyncStatus.CompletedWithErrors:
                    return "Sync Complete - With Errors";
                    break;
                case SyncStatus.Completed:
                    return "Sync Complete - No Warnings or Errors";
                    break;
            }

            return "Sync state indeterminate";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
