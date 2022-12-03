using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NileBeats
{
    public class AppSettings
    {
        public float Volume { get; set; }

        public string RecentFolder { get; set; }

        /// <summary>
        /// catch the ex if usingthis constructor
        /// </summary>
        /// <param name="path"></param>
        public AppSettings(string path)
        {
            StreamReader reader = null;

            try
            {
                reader = new StreamReader(path);
            
                Volume = float.Parse(reader.ReadLine());

                RecentFolder = reader.ReadLine();
            }
            finally
            {
                reader?.Dispose();
            }
        }

        public AppSettings()
        {
            Volume = 1;
            RecentFolder = null;
        }

        public void SaveSettings(string path)
        {
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(path, false);
            
                writer.WriteLine(Volume);
                writer.WriteLine(RecentFolder);
            }
            finally
            {
                writer?.Dispose();
            }
        }
    }
}
