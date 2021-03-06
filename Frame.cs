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

using System.Drawing;

namespace MSIT
{
    internal class Frame
    {
        public readonly int OriginalDelay;
        public int Delay;
        public readonly Bitmap Image;
        public readonly int Number;
        public Point Offset;

        public Frame(int no, Bitmap image, Point offset, int delay)
        {
            Number = no;
            Image = image;
            Offset = offset;
            Delay = OriginalDelay = delay;
        }
    }
}