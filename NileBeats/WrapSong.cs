using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NileBeats
{
    public class WrapSong : INotifyPropertyChanged
    {
        public WrapSong(Song song)
        {
            this.song = song;
        }

        private readonly Song song;

        public Song Song { get { return song; } }

        private bool isPlaying;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsPlaying { get { return isPlaying; } set { isPlaying = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying))); } }
    }
}
