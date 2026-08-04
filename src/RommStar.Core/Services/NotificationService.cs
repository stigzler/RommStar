using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Services
{
    public class NotificationService
    {
        Assembly _launchBoxAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "LaunchBox");

        private static MethodInfo _sendInfoNotificationMethod;
        private static MethodInfo _sendErrorNotificationMethod;

        public NotificationService()
        {
            if (_launchBoxAssembly != null)
            {
                var notificationCenterType = _launchBoxAssembly.GetType("Unbroken.LaunchBox.Windows.Desktop.Notifications.NotificationCenter");
                _sendInfoNotificationMethod = notificationCenterType?.GetMethod("SendInfoNotification", BindingFlags.Public | BindingFlags.Static);
                _sendErrorNotificationMethod = notificationCenterType?.GetMethod("SendErrorNotification", BindingFlags.Public | BindingFlags.Static);
            }
        }

        public void SendNotification(string message, int duration = 2)
        {
            _sendInfoNotificationMethod?.Invoke(null, new object[] { message, duration });
        }

        public void SendErrorNotification(string message, int duration = 2)
        {
            _sendErrorNotificationMethod?.Invoke(null, new object[] { message, duration });
        }
    }
}
