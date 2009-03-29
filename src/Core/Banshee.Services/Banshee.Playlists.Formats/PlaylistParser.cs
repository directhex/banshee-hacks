//
// PlaylistParser.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using System.IO;
using System.Net;
using System.Web;
using System.Collections.Generic;

using Banshee.Base;

namespace Banshee.Playlists.Formats
{   
    public class PlaylistParser
    {
        private static PlaylistFormatDescription [] playlist_formats = new PlaylistFormatDescription [] {
            M3uPlaylistFormat.FormatDescription,
            PlsPlaylistFormat.FormatDescription,
            AsxPlaylistFormat.FormatDescription,
            AsfReferencePlaylistFormat.FormatDescription,
            XspfPlaylistFormat.FormatDescription
        };
        
        private List<Dictionary<string, object>> elements;
        private Uri base_uri = new Uri (Environment.CurrentDirectory);
        private string title = null;
        
        public PlaylistParser ()
        {
        }
        
        public bool Parse (SafeUri uri)
        {
            ThreadAssist.AssertNotInMainThread ();
            lock (this) {
                elements = null;
                Stream stream = null;
                
                if (uri.Scheme == "file") {
                    stream = Banshee.IO.File.OpenRead (uri);
                } else if (uri.Scheme == "http") {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri.AbsoluteUri);
                    request.UserAgent = Banshee.Web.Browser.UserAgent;
                    request.KeepAlive = false;
                    request.Timeout = 5 * 1000;
                    request.AllowAutoRedirect = true;

                    // Parse out and set credentials, if any
                    string user_info = new Uri (uri.AbsoluteUri).UserInfo;
                    if (!String.IsNullOrEmpty (user_info)) {
                        string username = String.Empty;
                        string password = String.Empty;
                        int cIndex = user_info.IndexOf (":");
                        if (cIndex != -1) {
                            username = user_info.Substring (0, cIndex);
                            password = user_info.Substring (cIndex + 1);
                        } else {
                            username = user_info;
                        }
                        request.Credentials = new NetworkCredential (username, password);
                    }
            
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
                    Stream web_stream = response.GetResponseStream ();
                    
                    try {
                        stream = new MemoryStream ();

                        byte [] buffer = new byte[4096];
                        int read;
            
                        // If more than 4KB of data exists on an HTTP playlist, 
                        // it's probably not a playlist. This kind of sucks,
                        // but it should work until someone can prove otherwise
                        
                        read = web_stream.Read (buffer, 0, buffer.Length);
                        if (read >= buffer.Length - 1) {
                            throw new InvalidPlaylistException ();
                        }
                        
                        stream.Write (buffer, 0, read);
                        stream.Position = 0;
                    } finally {
                        web_stream.Close ();
                        response.Close ();
                    }
                } else {
                    Hyena.Log.DebugFormat ("Not able to parse playlist at {0}", uri);
                    return false;
                }
                                  
                PlaylistFormatDescription matching_format = null;

                foreach (PlaylistFormatDescription format in playlist_formats) {
                    stream.Position = 0;
                    
                    StreamReader reader = new StreamReader (stream);
                    if (format.MagicHandler (reader)) {
                        matching_format = format;
                        break;
                    }
                }

                if (matching_format == null) {
                    return false;
                }

                stream.Position = 0;
                IPlaylistFormat playlist = (IPlaylistFormat)Activator.CreateInstance (matching_format.Type);
                playlist.BaseUri = BaseUri;
                playlist.Load (stream, false);
                stream.Dispose ();
                
                elements = playlist.Elements;
                Title = playlist.Title ?? Path.GetFileNameWithoutExtension (uri.LocalPath);
                return true;
            }
        }
        
        public List<Dictionary<string, object>> Elements {
            get { return elements; }
        }
        
        public Uri BaseUri {
            get { return base_uri; }
            set { base_uri = value; }
        }
        
        public string Title {
            get { return title; }
            set { title = value; }
        }
    }
}
