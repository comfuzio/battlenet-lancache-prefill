﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BattleNetPrefill.Structs;
using BattleNetPrefill.Utils;
using BattleNetPrefill.Web;

namespace BattleNetPrefill.Handlers
{
    /// <summary>
    /// https://wowdev.wiki/TACT#Archive_Indexes_.28.index.29
    /// </summary>
    public class ArchiveIndexHandler
    {
        private readonly CdnRequestManager _cdnRequestManager;
        private readonly TactProduct _targetProduct;

        private const int CHUNK_SIZE = 4096;

        // Archives are built out using multiple dictionaries, since in some cases the large number of entries (possibly 3 million) causes performance issues
        // with C#'s Dictionary class.  Building them out in parallel, then doing multiple lookups ends up being faster than having a single Dictionary.
        private readonly List<Dictionary<MD5Hash, IndexEntry>> _indexDictionaries = new List<Dictionary<MD5Hash, IndexEntry>>();

        public ArchiveIndexHandler(CdnRequestManager cdnRequestManager, TactProduct targetProduct)
        {
            _cdnRequestManager = cdnRequestManager;
            _targetProduct = targetProduct;
        }

        /// <summary>
        /// Checks the archive indexes to see if a file (EKey) exists in the archives.  If it does, then an IndexEntry describing
        /// which archive contains the file will be returned.
        ///
        /// <see cref="BuildArchiveIndexesAsync"/> must be called prior to using this method.
        /// </summary>
        /// <param name="eKey">The MD5 hash of the file to lookup.  An EKey is the hash of the file itself. </param>
        /// <returns>An IndexEntry if the file exists in an archive.  Null if it is not an archived file.</returns>
        public IndexEntry? ArchivesContainKey(in MD5Hash eKey)
        {
            foreach (var dict in _indexDictionaries)
            {
                if (dict.TryGetValue(eKey, out IndexEntry returnValue))
                {
                    return returnValue;
                }
            }
            return null;
        }

        /// <summary>
        /// Downloads all archive indexes, and builds the archive index lookup dictionary for the specified product.
        /// </summary>
        public async Task BuildArchiveIndexesAsync(CDNConfigFile cdnConfig)
        {
            // This default performs well for most TactProducts.
            int maxTasks = 3;
            // Overwatch's indexes parse significantly faster when increasing the concurrency.
            if (_targetProduct == TactProduct.Overwatch)
            {
                maxTasks = 6;
            }
            
            // Building the archive index dictionaries in parallel.  Slicing up the work across multiple tasks.
            var tasks = new List<Task<Dictionary<MD5Hash, IndexEntry>>>();
            int sliceAmount = (int)Math.Ceiling((double)cdnConfig.archives.Length / maxTasks);
            
            for (int i = 0; i < maxTasks; i++)
            {
                var lowerLimit = (i * sliceAmount);
                int upperLimit = Math.Min(((i + 1) * sliceAmount) - 1, cdnConfig.archives.Length - 1);

                if (lowerLimit >= cdnConfig.archives.Length)
                {
                    continue;
                }
                tasks.Add(ProcessArchiveAsync(cdnConfig, lowerLimit, upperLimit));
            }
            await Task.WhenAll(tasks);

            // Aggregate the multiple computed dictionaries into a single list
            foreach (var task in tasks)
            {
                _indexDictionaries.Add(await task);
            }
        }
        
        private async Task<Dictionary<MD5Hash, IndexEntry>> ProcessArchiveAsync(CDNConfigFile cdnConfig, int start, int finish)
        {
            var indexDictionary = new Dictionary<MD5Hash, IndexEntry>(Md5HashEqualityComparer.Instance);

            for (int i = start; i <= finish; i++)
            {
                byte[] indexContent = await _cdnRequestManager.GetRequestAsBytesAsync(RootFolder.data, cdnConfig.archives[i].hashIdMd5, isIndex: true);

                using (var stream = new MemoryStream(indexContent))
                using (BinaryReader br = new BinaryReader(stream))
                {
                    var numElements = ValidateArchiveIndexFooter(stream, br);

                    for (int j = 0; j < numElements; j++)
                    {
                        MD5Hash key = br.Read<MD5Hash>();

                        var entry = new IndexEntry
                        {
                            index = (short)i,
                            size = br.ReadUInt32BigEndian(),
                            offset = br.ReadUInt32BigEndian()
                        };
                        indexDictionary.Add(key, entry);

                        // each chunk is 4096 bytes, and zero padding at the end
                        long remaining = CHUNK_SIZE - (stream.Position % CHUNK_SIZE);

                        // skip padding
                        if (remaining < 16 + 4 + 4)
                        {
                            stream.Position += remaining;
                        }
                    }
                }
            }
            return indexDictionary;
        }

        private int ValidateArchiveIndexFooter(MemoryStream stream, BinaryReader br)
        {
            // Footer should always be the last 20 bytes of the file
            stream.Seek(-20, SeekOrigin.End);

            if (br.ReadByte() != 1)
            {
                throw new InvalidDataException("ParseIndex -> version");
            }
            if (br.ReadByte() != 0)
            {
                throw new InvalidDataException("ParseIndex -> unk1");
            }
            if (br.ReadByte() != 0)
            {
                throw new InvalidDataException("ParseIndex -> unk2");
            }
            if (br.ReadByte() != 4)
            {
                throw new InvalidDataException("ParseIndex -> blockSizeKb");
            }

            byte offsetBytes = br.ReadByte();
            if (offsetBytes != 4)
                throw new InvalidDataException("ParseIndex -> offsetBytes");

            byte sizeBytes = br.ReadByte();
            if (sizeBytes != 4)
                throw new InvalidDataException("ParseIndex -> sizeBytes");

            byte keySizeBytes = br.ReadByte();
            if (keySizeBytes != 16)
                throw new InvalidDataException("ParseIndex -> keySizeBytes");

            if (br.ReadByte() != 8)
                throw new InvalidDataException("ParseIndex -> checksumSize");

            int numElements = br.ReadInt32();
            if (numElements * (keySizeBytes + sizeBytes + offsetBytes) > stream.Length)
                throw new Exception("ParseIndex failed");

            stream.Seek(0, SeekOrigin.Begin);
            return numElements;
        }
    }
}