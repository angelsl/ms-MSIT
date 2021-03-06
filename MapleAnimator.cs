﻿// This file is part of MSIT.
// 
// MSIT is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// MSIT is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MSIT.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace MSIT
{
    internal static class OffsetAnimator
    {
        // Algorithm stolen from haha01haha01 http://code.google.com/p/hasuite/source/browse/trunk/HaRepackerLib/AnimationBuilder.cs
        public static IEnumerable<Frame> Process(Rectangle padding, Color background, LoopType loop, params List<Frame>[] zframess)
        {
            List<List<Frame>> framess = zframess.Select(aframess => aframess.Select(f => new Frame(f.Number, f.Image, new Point(-f.Offset.X, -f.Offset.Y), f.Delay)).ToList()).ToList();
            framess = PadOffsets(Translate(framess), padding);
            Size fs = GetFrameSize(framess, padding);
            framess = framess.Select(f => f.OrderBy(z => z.Number).ToList()).ToList();
            List<Frame> frames = MergeMultiple(framess, fs, background, loop).OrderBy(z => z.Number).ToList();
            return FinalProcess(frames, fs, background);
        }

        private static List<List<Frame>> Translate(List<List<Frame>> framess)
        {
            int minx = framess.SelectMany(x => x).Min(fy => fy.Offset.X);
            int miny = framess.SelectMany(x => x).Min(fy => fy.Offset.Y);

            return framess.Select(fx => fx.Select(fy => new Frame(fy.Number, fy.Image, new Point(fy.Offset.X - minx, fy.Offset.Y - miny), fy.Delay)).ToList()).ToList();
        }

        private static List<List<Frame>> PadOffsets(List<List<Frame>> framess, Rectangle p)
        {
            return framess.Select(fx => fx.Select(fy => new Frame(fy.Number, fy.Image, new Point(fy.Offset.X + p.X, fy.Offset.Y + p.Y), fy.Delay)).ToList()).ToList();
        }

        private static Size GetFrameSize(List<List<Frame>> framess, Rectangle padding)
        {
            int w = framess.SelectMany(x => x).Max(x => padding.X + x.Offset.X + x.Image.Width + padding.Width);
            int h = framess.SelectMany(x => x).Max(x => padding.Y + x.Offset.Y + x.Image.Height + padding.Height);
            return new Size(w, h);
        }

        private static List<Frame> MergeMultiple(List<List<Frame>> framess, Size fs, Color bg, LoopType looping)
        {
            if (framess.Count() == 1) return framess.First();
            List<Frame> merged = new List<Frame>();
            List<List<Frame>.Enumerator> ers = framess.Select(x => x.GetEnumerator()).Select(x => {
                                                                                                 x.MoveNext();
                                                                                                 return x;
                                                                                             }).ToList();
            int no = 0, origCount = ers.Count, timeLeft = framess.Max(x => x.Sum(y => y.OriginalDelay));
            bool loop = looping == LoopType.LoopEnough || looping == LoopType.FullLoop;
            while (ers.Count > 0) {
                int mindelay = ers.Min(x => x.Current.Delay);
                timeLeft -= mindelay;
                ers.ForEach(f => f.Current.Delay -= mindelay);
                Bitmap b = new Bitmap(fs.Width, fs.Height);
                Graphics g = Graphics.FromImage(b);
                g.FillRectangle(new SolidBrush(bg), 0, 0, b.Width, b.Height);
                ers.ForEach(f => g.DrawImage(f.Current.Image, f.Current.Offset));
                g.Flush(FlushIntention.Sync);
                g.Dispose();
                merged.Add(new Frame(no++, b, new Point(0, 0), mindelay));
                bool shouldLoop = !ers.TrueForAll(e => e.Current.Delay <= 0 && !e.MoveNext()) && loop;
                ers = ers.Where(e => e.Current.Delay > 0 || e.MoveNext() || shouldLoop).Select(e => {
                                                                                             if (e.Current.Delay > 0 || (e.Current.Delay <= 0 && e.MoveNext())) return e;
                                                                                             return Fix(e, looping, ref timeLeft);
                                                                                         }).ToList();
                if (looping == LoopType.CutOnEnd && ers.Count < origCount)
                    break;
            }
            return merged;
        }

        private static IEnumerable<Frame> FinalProcess(List<Frame> frame, Size fs, Color bg)
        {
            return frame.Select(n => {
                                    Bitmap b = new Bitmap(fs.Width, fs.Height);
                                    Graphics g = Graphics.FromImage(b);
                                    g.FillRectangle(new SolidBrush(bg), 0, 0, b.Width, b.Height);
                                    g.DrawImage(n.Image, n.Offset);
                                    g.Flush(FlushIntention.Sync);
                                    g.Dispose();
                                    return new Frame(n.Number, b, new Point(0, 0), n.Delay);
                                }).ToList();
        }

        private static List<Frame>.Enumerator Fix(List<Frame>.Enumerator e, LoopType l, ref int timeLeft)
        {
            IEnumerator<Frame> f = e;
            switch (l) {
                case LoopType.LoopEnough:
                {
                    f.Reset();
                    int tL = timeLeft;
                    while(f.MoveNext() && timeLeft > 0 && f.Current != null) {
                        f.Current.Delay = Math.Min(tL, f.Current.OriginalDelay);
                        tL -= f.Current.Delay;
                    }
                    f.Reset();
                    f.MoveNext();
                    return (List<Frame>.Enumerator)f;
                }
                case LoopType.FullLoop:
                {
                    f.Reset();
                    int tL = 0;
                    while (f.MoveNext() && f.Current != null) {
                        tL += f.Current.OriginalDelay;
                        f.Current.Delay = f.Current.OriginalDelay;
                    }
                    timeLeft = Math.Max(timeLeft, tL);
                    f.Reset();
                    f.MoveNext();
                    return (List<Frame>.Enumerator)f;
                }
                default:
                    throw new InvalidOperationException("OffsetAnimator.Fix called with invalid LoopType!");
            }
        }
    }

    internal enum LoopType
    {
        /// <summary>
        ///   Does not loop any animations; in a multi-animation set, an animation that ends before the longest animation will not be repeated and simply end.
        /// </summary>
        NoLoop = 0,

        /// <summary>
        ///   Ends the entire set when any animation ends.
        /// </summary>
        CutOnEnd,

        /// <summary>
        ///   An animation that ends before the longest animation will be repeated until the longest animation ends.
        /// </summary>
        LoopEnough,

        /// <summary>
        ///   Animations will be repeated such they are not cut before the longest animation ends, and they are not cut midway through a repetition.
        /// </summary>
        FullLoop
    }
}