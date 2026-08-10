using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Extensions
{
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Adds the missing RemoveAll functionality to ObservableCollection.
        /// </summary>
        public static int RemoveAll<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
        {
            if (collection == null || predicate == null) return 0;

            // 1. Snapshot the items that match the condition
            var itemsToRemove = collection.Where(predicate).ToList();

            // 2. Remove them one by one so the UI gets notified properly
            foreach (var item in itemsToRemove)
            {
                collection.Remove(item);
            }

            return itemsToRemove.Count;
        }
    }
}
