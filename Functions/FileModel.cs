using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Functions
{
    internal class FileModel
    {

        public string Name { get; set; }
        public long Size { get; set; }

        public DateTimeOffset? LastModified { get; set; }

        public string DisplaySize
        {

            get
            {

                if (Size >= 1024 * 1024)
                    return $"{Size / 1024 / 1024} MB";
                if (Size >= 1024)
                    return $"{Size / 1024} KB";
                return $"{Size} Bytes";

            }

        }

    }
}
