using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class InfoBarSeverityToInfoBadgeStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not InfoBarSeverity severity)
                return DependencyProperty.UnsetValue;

            var styleName = severity switch
            {
                InfoBarSeverity.Informational => "InformationalIconInfoBadgeStyle",
                InfoBarSeverity.Success => "SuccessIconInfoBadgeStyle",
                InfoBarSeverity.Warning => "AttentionIconInfoBadgeStyle",
                InfoBarSeverity.Error => "CriticalIconInfoBadgeStyle",
                _ => null
            };

            if (styleName == null)
                return DependencyProperty.UnsetValue;

            return Application.Current.Resources[styleName] 
                ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}