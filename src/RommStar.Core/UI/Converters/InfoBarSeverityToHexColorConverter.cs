using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class InfoBarSeverityToHexColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not InfoBarSeverity severity)
                return DependencyProperty.UnsetValue;

            var styleName = severity switch
            {
                InfoBarSeverity.Informational => "#8000ffff",
                InfoBarSeverity.Success => "#8000ff00",
                InfoBarSeverity.Warning => "#80ff8000",
                InfoBarSeverity.Error => "#80ff0000",
                _ => null
            };

            if (styleName == null)
                return DependencyProperty.UnsetValue;

            // parameter should be your MainWindowView
            return styleName.ToString();

            //return DependencyProperty.UnsetValue;

            //                case InfoBarSeverity.Success:
            //    return "#8000ff00"; break;
            //case InfoBarSeverity.Error:
            //    return "#80ff0000"; break;
            //case InfoBarSeverity.Informational:
            //    return "#808080ff"; break;
            //case InfoBarSeverity.Warning:
            //    return
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}