// 
// SignOutButton.cs
// 
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

using Mono.Unix;
using Gtk;

namespace Banshee.AmazonMp3.Store
{
    public class SignOutButton : Button
    {
        private StoreView store_view;

        public SignOutButton (StoreView storeView) : base (Catalog.GetString ("Sign out of Amazon"))
        {
            store_view = storeView;
            store_view.SignInChanged += (o, e) => UpdateSignInButton ();
            UpdateSignInButton ();
        }

        protected override void OnClicked ()
        {
            base.OnClicked ();
            store_view.SignOut ();
        }

        private void UpdateSignInButton ()
        {
            Visible = store_view.IsSignedIn;
        }
    }
}

