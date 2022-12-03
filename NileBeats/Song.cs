using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace NileBeats
{
    public class Song
    {
        private readonly string path;

        public string Path { get { return path; } }

        private TimeSpan duration;

        public TimeSpan Duration { get { return duration; } }

        public string DisplayString
        { 
            get
            {
                return System.IO.Path.GetFileNameWithoutExtension(path) + " " + duration.ToString("%d\\.hh\\:mm\\:ss");
            }
        }

        public string Name
        {
            get
            {
                return System.IO.Path.GetFileNameWithoutExtension(path);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="Throws an exception if reading of meta fails"></exception>
        public Song(string path)
        {
            this.path = path;

            TagLib.File meta = null;

            try
            {
                meta = TagLib.File.Create(path);

                this.duration = meta.Properties.Duration;
            }
            finally
            {
                meta?.Dispose();
            }
        }
    }
}
