//
// Shuffler.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;

using Mono.Addins;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.PlaybackController;
using System.Collections.Generic;

namespace Banshee.Collection.Database
{
    public class Shuffler
    {
        public static readonly Shuffler Playback = new Shuffler () { Id = "playback", DbId = 0 };

        private static HyenaSqliteCommand add_shuffle_cmd = new HyenaSqliteCommand ("INSERT OR REPLACE INTO CoreShuffles (ShufflerID, TrackID, LastShuffledAt) VALUES (?, ?, ?)");
        private static HyenaSqliteCommand add_discard_cmd = new HyenaSqliteCommand ("INSERT OR REPLACE INTO CoreShuffleDiscards (ShufflerID, TrackID, LastDiscardedAt) VALUES (?, ?, ?)");

        private DateTime random_began_at = DateTime.MinValue;
        private DateTime last_random = DateTime.MinValue;
        private List<RandomBy> random_modes;
        private Dictionary<TypeExtensionNode, RandomBy> node_map = new Dictionary<TypeExtensionNode, RandomBy> ();
        private DatabaseTrackListModel model;

        public string Id { get; private set; }
        public int DbId { get; private set; }

        public Action<RandomBy> RandomModeAdded;
        public Action<RandomBy> RandomModeRemoved;

        public IList<RandomBy> RandomModes { get { return random_modes; } }

        private Shuffler ()
        {
            random_modes = new List<RandomBy> ();
            AddinManager.AddExtensionNodeHandler ("/Banshee/PlaybackController/ShuffleModes", OnExtensionChanged);
        }

        public Shuffler (string id) : this ()
        {
            Id = id;
            LoadOrCreate ();
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            var tnode = (TypeExtensionNode)args.ExtensionNode;
            RandomBy random_by = null;

            if (args.Change == ExtensionChange.Add) {
                lock (random_modes) {
                    try {
                        random_by = (RandomBy) tnode.CreateInstance ();
                        random_by.SetShuffler (this);
                        random_modes.Add (random_by);
                        node_map[tnode] = random_by;
                    } catch (Exception e) {
                        Log.Exception (String.Format ("Failed to load RandomBy extension: {0}", args.Path), e);
                    }
                }

                if (random_by != null) {
                    if (!random_by.GetType ().AssemblyQualifiedName.Contains ("Banshee.Service")) {
                        Log.DebugFormat ("Loaded new mode into {0} shuffler: {1}", this.Id, random_by.Id);
                    }
                    var handler = RandomModeAdded;
                    if (handler != null) {
                        handler (random_by);
                    }
                }
            } else {
                lock (random_modes) {
                    if (node_map.ContainsKey (tnode)) {
                        random_by = node_map[tnode];
                        node_map.Remove (tnode);
                        random_modes.Remove (random_by);
                    }
                }

                if (random_by != null) {
                    Log.DebugFormat ("Removed mode from {0} shuffler: {1}", this.Id, random_by.Id);
                    var handler = RandomModeRemoved;
                    if (handler != null) {
                        handler (random_by);
                    }
                }
            }
        }

        public void SetModelAndCache (DatabaseTrackListModel model, IDatabaseTrackModelCache cache)
        {
            this.model = model;

            foreach (var random in random_modes) {
                random.SetModelAndCache (model, cache);
            }
        }

        public TrackInfo GetRandom (DateTime notPlayedSince, string mode, bool repeat, bool resetSinceTime)
        {
            lock (this) {
                if (this == Playback) {
                    if (model.Count == 0) {
                        return null;
                    }
                } else {
                    if (model.UnfilteredCount == 0) {
                        return null;
                    }
                }

                if (random_began_at < notPlayedSince) {
                    random_began_at = last_random = notPlayedSince;
                }

                TrackInfo track = GetRandomTrack (mode, repeat, resetSinceTime);
                if (track == null && (repeat || mode != "off")) {
                    random_began_at = (random_began_at == last_random) ? DateTime.Now : last_random;
                    track = GetRandomTrack (mode, repeat, true);
                }

                last_random = DateTime.Now;
                return track;
            }
        }

        public void RecordShuffle (DatabaseTrackInfo track)
        {
            if (track != null) {
                ServiceManager.DbConnection.Execute (add_shuffle_cmd, this.DbId, track.TrackId, DateTime.Now);
            }
        }

        public void RecordDiscard (DatabaseTrackInfo track)
        {
            if (track != null) {
                ServiceManager.DbConnection.Execute (add_discard_cmd, this.DbId, track.TrackId, DateTime.Now);
            }
        }

        private TrackInfo GetRandomTrack (string mode, bool repeat, bool resetSinceTime)
        {
            foreach (var r in random_modes) {
                if (resetSinceTime || r.Id != mode) {
                    r.Reset ();
                }
            }

            var random = random_modes.FirstOrDefault (r => r.Id == mode);
            if (random != null) {
                if (!random.IsReady) {
                    if (!random.Next (random_began_at) && repeat) {
                        random_began_at = last_random;
                        random.Next (random_began_at);
                    }
                }

                if (random.IsReady) {
                    return random.GetTrack (random_began_at);
                }
            }

            return null;
        }

        private void LoadOrCreate ()
        {
            var db = ServiceManager.DbConnection;

            int res = db.Query<int> ("SELECT ShufflerID FROM CoreShufflers WHERE ID = ?", Id);
            if (res > 0) {
                DbId = res;
            } else {
                DbId = db.Execute ("INSERT INTO CoreShufflers (ID) VALUES (?)", Id);
            }
        }
    }
}
