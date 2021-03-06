﻿/*  HaRepacker - WZ extractor and repacker
 * Copyright (C) 2009, 2010 haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/
#if APNG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace SharpApng
{
    public class Frame : IDisposable
    {
        private int m_num;
        private int m_den;
        private Bitmap m_bmp;

        public void Dispose()
        {
            m_bmp.Dispose();
        }

        public Frame(Bitmap bmp, int num, int den)
        {
            this.m_num = num;
            this.m_den = den;
            this.m_bmp = bmp;
        }

        public int DelayNum
        {
            get
            {
                return m_num;
            }
            set
            {
                m_num = value;
            }
        }

        public int DelayDen
        {
            get
            {
                return m_den;
            }
            set
            {
                m_den = value;
            }
        }

        public Bitmap Bitmap
        {
            get
            {
                return m_bmp;
            }
            set
            {
                m_bmp = value;
            }
        }
    }

    internal class Apng : IDisposable
    {
        private List<Frame> m_frames = new List<Frame>();

        public Apng()
        {
        }

        public void Dispose()
        {
            foreach (Frame frame in m_frames)
                frame.Dispose();
            m_frames.Clear();
        }

        public Frame this[int index]
        {
            get
            {
                if (index < m_frames.Count) return m_frames[index];
                else return null;
            }
            set
            {
                if (index < m_frames.Count) m_frames[index] = value;
            }
        }

        public void AddFrame(Frame frame)
        {
            m_frames.Add(frame);
        }

        public void AddFrame(Bitmap bmp, int num, int den)
        {
            m_frames.Add(new Frame(bmp, num, den));
        }

        private static Bitmap ExtendImage(Bitmap source, Size newSize)
        {
            Bitmap result = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImageUnscaled(source, 0, 0);
            }
            return result;
        }

        public void WriteApng(string path, bool firstFrameHidden, bool disposeAfter)
        {
            Size maxSize = new Size(m_frames.Max(f => f.Bitmap.Width), m_frames.Max(f => f.Bitmap.Height));
            for (int i = 0; i < m_frames.Count; i++)
            {
                Frame frame = m_frames[i];
                if (frame.Bitmap.Width != maxSize.Width || frame.Bitmap.Height != maxSize.Height)
                    frame.Bitmap = ExtendImage(frame.Bitmap, maxSize);
                ApngBasicWrapper.CreateFrameManaged(frame.Bitmap, frame.DelayNum, frame.DelayDen, i);
            }
            ApngBasicWrapper.SaveApngManaged(path, m_frames.Count, maxSize.Width, maxSize.Height, firstFrameHidden);
            if (disposeAfter) Dispose();
        }
    }

    internal static class ApngBasicWrapper
    {
        private const int PIXEL_DEPTH = 4;

        private static IntPtr MarshalString(string source)
        {
            byte[] toMarshal = Encoding.ASCII.GetBytes(source);
            int size = Marshal.SizeOf(source[0]) * source.Length;
            IntPtr pnt = Marshal.AllocHGlobal(size + Marshal.SizeOf(source[0]));
            Marshal.Copy(toMarshal, 0, pnt, source.Length);
            Marshal.Copy(new byte[] { 0 }, 0, new IntPtr(pnt.ToInt32() + size), 1);
            return pnt;
        }

        private static IntPtr MarshalByteArray(byte[] source)
        {
            int size = Marshal.SizeOf(source[0]) * source.Length;
            IntPtr pnt = Marshal.AllocHGlobal(size);
            Marshal.Copy(source, 0, pnt, source.Length);
            return pnt;
        }

        private static void ReleaseData(IntPtr ptr)
        {
            Marshal.FreeHGlobal(ptr);
        }

        private static unsafe byte[] TranslateImage(Bitmap image)
        {
            int w = image.Width, h = image.Height;
            byte[] result = new byte[w * h * PIXEL_DEPTH];
            BitmapData data = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte* p = (byte*)data.Scan0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    result[(y * w + x) * PIXEL_DEPTH] = p[x * PIXEL_DEPTH];
                    result[(y * w + x) * PIXEL_DEPTH + 1] = p[x * PIXEL_DEPTH + 1];
                    result[(y * w + x) * PIXEL_DEPTH + 2] = p[x * PIXEL_DEPTH + 2];
                    result[(y * w + x) * PIXEL_DEPTH + 3] = p[x * PIXEL_DEPTH + 3];
                }
                p += data.Stride;
            }
            image.UnlockBits(data);
            return result;
        }

        internal static void CreateFrameManaged(Bitmap source, int num, int den, int i)
        {
            IntPtr ptr = MarshalByteArray(TranslateImage(source));
            CreateFrame(ptr, num, den, i, source.Width * source.Height * PIXEL_DEPTH);
            ReleaseData(ptr);
        }

        internal static void SaveApngManaged(string path, int frameCount, int width, int height, bool firstFrameHidden)
        {
            IntPtr pathPtr = MarshalString(path);
            byte firstFrame = firstFrameHidden ? (byte)1 : (byte)0;
            SaveAPNG(pathPtr, frameCount, width, height, PIXEL_DEPTH, firstFrame);
            ReleaseData(pathPtr);
        }

        private const string apngdll = "apng.dll";

        [DllImport(apngdll)]
        private static extern void CreateFrame(IntPtr pdata, int num, int den, int i, int len);

        [DllImport(apngdll)]
        private static extern void SaveAPNG(IntPtr path, int frameCount, int width, int height, int bytesPerPixel, byte firstFrameHidden);
    }
}
#endif