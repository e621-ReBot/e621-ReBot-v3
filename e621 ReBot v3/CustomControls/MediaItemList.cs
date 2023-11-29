using System;
using System.Collections;
using System.Collections.Generic;

namespace e621_ReBot_v3.CustomControls
{
    internal class MediaItemList : CollectionBase, IList, IEnumerable
    {
        private List<string> MediaURLs = new List<string>();

        internal MediaItem? this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException($"Index ({index}) out of range.");
                return (MediaItem?)InnerList[index];
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

        internal int FindIndex(MediaItem MediaItemRef)
        {
            for (int i = 0; i < InnerList.Count; i++)
            {
                if (ReferenceEquals((MediaItem)InnerList[i], MediaItemRef)) return i;
            }
            return -1;
        }

        internal void Add(MediaItem MediaItemRef)
        {
            MediaURLs.Add(MediaItemRef.Grab_MediaURL);
            InnerList.Add(MediaItemRef);
        }

        internal void AddRange(List<MediaItem> collection)
        {
            foreach (MediaItem MediaItemTemp in collection)
            {
                MediaURLs.Add(MediaItemTemp.Grab_MediaURL);
                InnerList.Add(MediaItemTemp);
            }
        }

        internal void Remove(MediaItem MediaItemRef)
        {
            MediaURLs.Remove(MediaItemRef.Grab_MediaURL);
            InnerList.Remove(MediaItemRef);
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
                if (((MediaItem)InnerList[i]).Grab_MediaURL.Equals(URL2Remove))
                {
                    MediaURLs.Remove(URL2Remove);
                    InnerList.RemoveAt(i);
                    break;
                }
            }
        }

        internal List<MediaItem> GetRange(int index, int count)
        {
            if (index < 0) throw new IndexOutOfRangeException("Index can not be negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(index), "Count can not be negative.");
            if (List.Count - index < count) throw new ArgumentException("Range can not be higher than number of items.");

            List<MediaItem> list = new List<MediaItem>(count);
            for (int i = index; i < count + index; i++)
            {
                list.Add(this[i]);
            }
            return list;
        }

        internal bool Contains(MediaItem MediaItemRef)
        {
            return InnerList.Contains(MediaItemRef);
        }

        internal bool ContainsURL(string URL2Find)
        {
            return MediaURLs.Contains(URL2Find);
        }

        internal void Reverse()
        {
            InnerList.Reverse();
        }

        internal void Insert(int index, MediaItem MediaItemRef)
        {
            MediaURLs.Insert(index, MediaItemRef.Grab_MediaURL);
            InnerList.Insert(index, MediaItemRef);
        }
    }
}