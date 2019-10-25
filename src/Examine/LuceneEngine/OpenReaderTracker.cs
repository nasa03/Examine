using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Lucene.Net.Index;
using Lucene.Net.Store;

namespace Examine.LuceneEngine
{
    
    internal class OpenReaderTracker
    {
        private static readonly OpenReaderTracker Instance = new OpenReaderTracker();

        private readonly List<Tuple<IndexReader, DateTime,Directory>> _oldReaders = new List<Tuple<IndexReader, DateTime,Directory>>(); 

        private readonly object _locker = new object();

        public static OpenReaderTracker Current
        {
            get { return Instance; }
        }

        public void AddOpenReader(IndexReader reader, Directory directory)
        {
            lock (_locker)
            {
                //reader.IncRef();
                _oldReaders.Add(new Tuple<IndexReader, DateTime,Directory>(reader, DateTime.Now,directory));
            }
        }

        public int CloseStaleReaders(Directory dir, TimeSpan ts)
        {
            lock (_locker)
            {
                var now = DateTime.Now;

                var readersForDir = _oldReaders.Where(x => x.Item3.GetLockID() == dir.GetLockID()).ToList();
                var newest = readersForDir.OrderByDescending(x => x.Item2).FirstOrDefault();
                readersForDir.Remove(newest);
                var stale = readersForDir.Where(x => now - x.Item2 >= ts).ToArray();

                foreach (var reader in stale)
                {
                    //close reader and remove from list
                    try
                    {
                        reader.Item1.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        //if this happens, more than one instance has decreased referenced, this could occur if this 
                        //somehow gets called in conjuction with the shutdown code or manually, etc...
                    }
                    _oldReaders.Remove(reader);
                }
                return stale.Length;
            }
            
        }

        public int CloseAllReaders(Directory dir)
        {
            lock (_locker)
            {
                var readersForDir = _oldReaders.Where(x => x.Item3.GetLockID() == dir.GetLockID()).ToArray();
                foreach (var reader in readersForDir)
                {
                    //close reader and remove from list
                    try
                    {
                        reader.Item1.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        //if this happens, more than one instance has decreased referenced, this could occur if this 
                        //somehow gets called in conjuction with the shutdown code or manually, etc...
                    }

                    _oldReaders.Remove(reader);
                }
                return readersForDir.Length;
            }
        }

        public int CloseAllReaders()
        {
            lock (_locker)
            {
                var readers = _oldReaders.ToArray();
                foreach (var reader in readers)
                {
                    //close reader and remove from list
                    try
                    {
                        reader.Item1.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        //if this happens, more than one instance has decreased referenced, this could occur if this 
                        //somehow gets called in conjuction with the shutdown code or manually, etc...
                    }

                    _oldReaders.Remove(reader);
                }
                return readers.Length;
            }
        }

    }
}