// SmoothScrolledWindow.cs
//
// Copyright (c) 2008 Scott Peterson <lunchtimemama@gmail.com>
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
//

using System;
using Gdk;
using Gtk;

using Hyena.Gui.Theatrics;

namespace Hyena.Widgets
{
    public class SmoothScrolledWindow : Gtk.ScrolledWindow
    {
        private bool ignore_value_changed;
        private uint timeout;
        private double value;
        private double target_value;
        private double velocity = 0;
        
        protected virtual double MaxVelocity {
            get { return Vadjustment.StepIncrement * 4; }
        }
        
        private double Accelerate (double velocity)
        {
            return Math.Min (AccelerateCore (velocity), MaxVelocity);
        }
        
        private double Decelerate (double velocity)
        {
            return Math.Max (DecelerateCore (velocity), 0);
        }
        
        protected virtual double AccelerateCore (double velocity)
        {
            return velocity + 2;
        }
        
        protected virtual double DecelerateCore (double velocity)
        {
            return velocity - 1;
        }
        
        private double TargetValue {
            get { return target_value; }
            set {
                if (value == target_value) {
                    return;
                }
                
                target_value = value;
                if (timeout == 0) {
                    timeout = GLib.Timeout.Add (20, OnTimeout);
                }
            }
        }
        
        private bool OnTimeout ()
        {
            double delta = target_value - value;
            if (delta == 0) {
                velocity = 0;
                timeout = 0;
                return false;
            }
            
            int sign = Math.Sign (delta);
            delta = Math.Abs (delta);
            
            velocity = TimeToAccelerate (delta) ? Accelerate (velocity) : Decelerate (velocity);
            
            value += Math.Max (velocity, 0.5) * sign;
            ignore_value_changed = true;
            Vadjustment.Value = Math.Round (value);
            ignore_value_changed = false;
            
            return true;
        }
        
        private bool TimeToAccelerate (double delta)
        {
            double hypothetical = delta;
            double v = Accelerate (velocity);
            while (v > 0) {
                hypothetical -= v;
                v = Decelerate (v);
            }
            return hypothetical > 0;
        }
        
        protected override bool OnScrollEvent (Gdk.EventScroll evnt)
        {
            switch (evnt.Direction) {
            case ScrollDirection.Up:
                TargetValue = Math.Max (TargetValue - Vadjustment.StepIncrement, 0);
                break;
            case ScrollDirection.Down:
                TargetValue = Math.Min (TargetValue + Vadjustment.StepIncrement, Vadjustment.Upper - Vadjustment.PageSize);
                break;
            default:
                return base.OnScrollEvent (evnt);
            }
            return true;
        }
        
        protected override void OnRealized ()
        {
            base.OnRealized ();
            Vadjustment.ValueChanged += OnValueChanged;
        }
        
        protected override void OnUnrealized ()
        {
            Vadjustment.ValueChanged -= OnValueChanged;
            base.OnUnrealized ();
        }
        
        private void OnValueChanged (object o, EventArgs args)
        {
            if (!ignore_value_changed) {
                value = target_value = Vadjustment.Value;
            }
        }
    }
}