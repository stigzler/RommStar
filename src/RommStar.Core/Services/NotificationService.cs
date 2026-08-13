using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RommStar.Core.Services
{
    public class NotificationService
    {

        LoggingService _loggingService;

        Assembly _launchBoxAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "LaunchBox");

        private static MethodInfo _sendInfoNotificationMethod;
        private static MethodInfo _sendErrorNotificationMethod;
        private static MethodInfo _addPassiveNotification;

        public NotificationService(LoggingService loggingService)
        {
            _loggingService = loggingService;

            if (_launchBoxAssembly != null)
            {
                var notificationCenterType = _launchBoxAssembly.GetType("Unbroken.LaunchBox.Windows.Desktop.Notifications.NotificationCenter");
                _sendInfoNotificationMethod = notificationCenterType?.GetMethod("SendInfoNotification", BindingFlags.Public | BindingFlags.Static);
                _sendErrorNotificationMethod = notificationCenterType?.GetMethod("SendErrorNotification", BindingFlags.Public | BindingFlags.Static);

                _addPassiveNotification = notificationCenterType?.GetMethod("AddPassiveNotification", BindingFlags.Public | BindingFlags.Static,
                                            null,
                                            new Type[] { typeof(string), typeof(bool) },
                                            null);
            }

        }

        public void SendInfoNotification(string message, int duration = 2, bool alsoLog = false)
        {
            _sendInfoNotificationMethod?.Invoke(null, new object[] { message, duration });
            if (alsoLog) _loggingService.Log(message);
        }

        public void SendErrorNotification(string message, int duration = 2, bool alsoLog = false)
        {
            _sendErrorNotificationMethod?.Invoke(null, new object[] { message, duration });
            if (alsoLog) _loggingService.Log(message);

        }

        public void AddPassiveNotification(string message, bool markUnread = true, bool alsoLog = false)
        {
            _addPassiveNotification?.Invoke(null, new object[] { message, markUnread });
            if (alsoLog) _loggingService.Log(message);
        }
    }
}
