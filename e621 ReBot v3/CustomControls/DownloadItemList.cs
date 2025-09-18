using System;
using System.Collections;
using System.Collections.Generic;

namespace e621_ReBot_v3.CustomControls
{
    internal class DownloadItemList : CollectionBase, IList, IEnumerable
    {
        private List<string> MediaURLs = new List<string>();

        internal DownloadItem? this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException($"Index out of range. ({index})");
                return (DownloadItem?)InnerList[index];
            }
            set
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index), "Index out of range.");
                OnValidate(value!);
                object? temp = InnerList[index];
                OnSet(index, temp, value);
                InnerList[index] = value;
                try
                {
                    OnSetComplete(index, temp, value);
                }
                catch
                {
                    InnerList[index] = temp;
                    throw;
                }
            }
        }

        internal int FindIndex(DownloadItem DownloadItemRef)
        {
            for (int i = 0; i < InnerList.Count; i++)
            {
                if (ReferenceEquals((DownloadItem)InnerList[i], DownloadItemRef)) return i;
            }
            return -1;
        }

        internal int FindIndex(string URL2Find)
        {
            for (int i = 0; i < InnerList.Count; i++)
            {
                if (((DownloadItem)InnerList[i]).Grab_MediaURL.Equals(URL2Find)) return i;
            }
            return -1;
        }

        internal void Add(DownloadItem DownloadItemRef)
        {
            MediaURLs.Add(DownloadItemRef.Grab_MediaURL);
            InnerList.Add(DownloadItemRef);
        }

        internal void AddRange(List<DownloadItem> collection)
        {
            foreach (DownloadItem DownloadItemTemp in collection)
            {
                MediaURLs.Add(DownloadItemTemp.Grab_MediaURL);
                InnerList.Add(DownloadItemTemp);
            }
        }

        internal void Remove(DownloadItem DownloadItemRef)
        {
            MediaURLs.Remove(DownloadItemRef.Grab_MediaURL);
            InnerList.Remove(DownloadItemRef);
        }

        internal new void RemoveAt(int index)
        {
            MediaURLs.RemoveAt(index);
            InnerList.RemoveAt(index);
        }

        internal void RemoveURL(string URL2Remove)
        {
            for (int i = InnerList.Count - 1; i >= 0; i--)
            {
                if (((DownloadItem)InnerList[i]).Grab_MediaURL.Equals(URL2Remove))
                {
                    MediaURLs.Remove(URL2Remove);
                    InnerList.RemoveAt(i);
                    break;
                }
            }
        }

        internal List<DownloadItem> GetRange(int index, int count)
        {
            if (index < 0) throw new IndexOutOfRangeException("Index can not be negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(index), "Count can not be negative.");
            if (List.Count - index < count) throw new ArgumentException("Range can not be higher than number of items.");

            List<DownloadItem> list = new List<DownloadItem>(count);
            for (int i = index; i < count + index; i++)
            {
                list.Add(this[i]);
            }
            return list;
        }

        internal bool Contains(DownloadItem DownloadItemRef)
        {
            return InnerList.Contains(DownloadItemRef);
        }

        internal bool ContainsURL(string URL2Find)
        {
            return MediaURLs.Contains(URL2Find);
        }

        internal void Reverse()
        {
            InnerList.Reverse();
        }

        internal void Insert(int index, DownloadItem DownloadItemRef)
        {
            MediaURLs.Insert(index, DownloadItemRef.Grab_MediaURL);
            InnerList.Insert(index, DownloadItemRef);
        }

        internal new void Clear()
        {
            MediaURLs.Clear();
            InnerList.Clear();
        }
    }
}