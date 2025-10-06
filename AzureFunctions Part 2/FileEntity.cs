using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions_Part_2
{
    internal class FileEntity 
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
